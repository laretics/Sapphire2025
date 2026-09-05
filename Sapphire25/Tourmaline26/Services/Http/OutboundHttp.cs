using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Tourmaline26.Services.Http
{
	/// <summary>
	/// HTTP de salida pensado para la red del tren: Happy Eyeballs corto
	/// (IPv4 nativa, AAAA, NAT64 sintetizada), HTTP/1.1 (los WAF de TIB/SFM
	/// se llevan mal con HTTP/2) y cabeceras de navegador.
	/// </summary>
	internal static class OutboundHttp
	{
		public const string BrowserUserAgent =
			"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

		internal static readonly TimeSpan HappyEyeballsNat64Delay = TimeSpan.FromMilliseconds(250);
		internal static readonly TimeSpan TcpConnectTimeout = TimeSpan.FromSeconds(4);

		private static readonly ConcurrentDictionary<string, string> mcolLoggedTargets =
			new(StringComparer.OrdinalIgnoreCase);
		private static readonly ConcurrentDictionary<string, OutboundConnectInfo> mcolLastConnect =
			new(StringComparer.OrdinalIgnoreCase);

		private static ILogger? mvarLogger;

		public static void Configure(ILoggerFactory? loggerFactory, IConfiguration? configuration = null)
		{
			ILogger? logger = loggerFactory?.CreateLogger("Tourmaline26.Services.Http.OutboundHttp");
			mvarLogger = logger;
			Nat64PrefixCache.Configure(configuration?["OutboundHttp:Nat64Prefix"], logger);
		}

		public static SocketsHttpHandler CreateHandler()
		{
			return new SocketsHttpHandler
			{
				AutomaticDecompression = DecompressionMethods.All,
				ConnectTimeout = TimeSpan.FromSeconds(20),
				PooledConnectionLifetime = TimeSpan.FromMinutes(2),
				PooledConnectionIdleTimeout = TimeSpan.FromSeconds(45),
				UseCookies = true,
				CookieContainer = new CookieContainer(),
				ConnectCallback = ConnectDualStackOrNat64Async
			};
		}

		public static void ApplyBrowserDefaults(HttpClient client, string? origin)
		{
			client.DefaultRequestVersion = HttpVersion.Version11;
			client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
			client.DefaultRequestHeaders.UserAgent.Clear();
			client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
			client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "ca,es;q=0.9,en;q=0.8");

			if (string.IsNullOrWhiteSpace(origin))
				return;

			string originRoot = origin.TrimEnd('/');
			client.DefaultRequestHeaders.TryAddWithoutValidation("Origin", originRoot);
			if (Uri.TryCreate(originRoot + "/", UriKind.Absolute, out Uri? referrer))
				client.DefaultRequestHeaders.Referrer = referrer;
		}

		internal static bool TryGetLastConnect(string host, out OutboundConnectInfo info) =>
			mcolLastConnect.TryGetValue(host, out info!);

		private static async ValueTask<Stream> ConnectDualStackOrNat64Async(
			SocketsHttpConnectionContext context,
			CancellationToken cancellationToken)
		{
			string host = context.DnsEndPoint.Host;
			int port = context.DnsEndPoint.Port;
			string? sslHost = SslHostName(context);

			if (IPAddress.TryParse(host, out IPAddress? literal))
			{
				IReadOnlyList<Nat64Prefix> literalPrefixes = Socket.OSSupportsIPv6
					? await Nat64PrefixCache.GetPrefixesAsync(cancellationToken).ConfigureAwait(false)
					: Array.Empty<Nat64Prefix>();
				IReadOnlyList<ConnectCandidate> literalCandidates = BuildConnectCandidates(
					[literal],
					literalPrefixes,
					Socket.OSSupportsIPv6);
				return await ConnectCandidatesToStreamAsync(
						host,
						port,
						sslHost,
						literalCandidates,
						cancellationToken)
					.ConfigureAwait(false);
			}

			IPAddress[]? resolved = await TryResolveAsync(host, cancellationToken).ConfigureAwait(false);
			if (resolved is { Length: > 0 })
			{
				IReadOnlyList<Nat64Prefix> prefixes = Socket.OSSupportsIPv6
					? await Nat64PrefixCache.GetPrefixesAsync(cancellationToken).ConfigureAwait(false)
					: Array.Empty<Nat64Prefix>();
				IReadOnlyList<ConnectCandidate> candidates = BuildConnectCandidates(
					resolved,
					prefixes,
					Socket.OSSupportsIPv6);
				if (candidates.Count > 0)
				{
					return await ConnectCandidatesToStreamAsync(
							host,
							port,
							sslHost,
							candidates,
							cancellationToken)
						.ConfigureAwait(false);
				}
			}

			return await ConnectSocketAsync(context.DnsEndPoint, sslHost, cancellationToken).ConfigureAwait(false);
		}

		private static async Task<Stream> ConnectCandidatesToStreamAsync(
			string host,
			int port,
			string? sslHost,
			IReadOnlyList<ConnectCandidate> candidates,
			CancellationToken cancellationToken)
		{
			(ConnectCandidate candidate, Socket socket) = await RaceCandidatesAsync(
					candidates,
					(c, ct) => ConnectTcpAsync(c.Address, port, ct),
					HappyEyeballsNat64Delay,
					lost => lost.Dispose(),
					OnCandidateFailed,
					cancellationToken)
				.ConfigureAwait(false);

			try
			{
				RememberConnect(host, candidate);
				LogConnect(host, port, candidate);
				Stream stream = new NetworkStream(socket, ownsSocket: true);
				socket = null!;
				if (string.IsNullOrEmpty(sslHost) || IPAddress.TryParse(sslHost, out _))
					return stream;
				return await AuthenticateHttpsAsync(stream, sslHost, candidate.Address, cancellationToken)
					.ConfigureAwait(false);
			}
			catch
			{
				socket?.Dispose();
				throw;
			}
		}

		private static void OnCandidateFailed(ConnectCandidate candidate, Exception exception)
		{
			if (candidate.Kind != ConnectCandidateKind.Nat64)
				return;
			mvarLogger?.LogWarning(
				exception,
				"IPv4 unreachable, NAT64 fail, prefix={Prefix} ip={Ip}",
				candidate.Prefix?.ToString() ?? "(ninguno)",
				candidate.Address);
		}

		internal static IReadOnlyList<ConnectCandidate> BuildConnectCandidates(
			IReadOnlyList<IPAddress> addresses,
			IReadOnlyList<Nat64Prefix> prefixes,
			bool ipv6Available)
		{
			var list = new List<ConnectCandidate>();
			var seen = new HashSet<IPAddress>();

			foreach (IPAddress address in addresses)
			{
				if (!IsUsable(address) || address.AddressFamily != AddressFamily.InterNetwork)
					continue;
				if (seen.Add(address))
					list.Add(new ConnectCandidate(address, ConnectCandidateKind.NativeIPv4));
			}

			if (!ipv6Available)
				return list;

			foreach (IPAddress address in addresses)
			{
				if (!IsUsable(address) || address.AddressFamily != AddressFamily.InterNetworkV6)
					continue;
				if (seen.Add(address))
					list.Add(new ConnectCandidate(address, ConnectCandidateKind.NativeIPv6));
			}

			foreach (IPAddress ipv4 in addresses)
			{
				if (!IsUsable(ipv4) || ipv4.AddressFamily != AddressFamily.InterNetwork)
					continue;
				foreach (Nat64Prefix prefix in prefixes)
				{
					IPAddress synthesized = prefix.Synthesize(ipv4);
					if (!seen.Add(synthesized))
						continue;
					list.Add(new ConnectCandidate(
						synthesized,
						ConnectCandidateKind.Nat64,
						ipv4,
						prefix));
				}
			}

			return list;
		}

		internal static async Task<(ConnectCandidate Candidate, T Result)> RaceCandidatesAsync<T>(
			IReadOnlyList<ConnectCandidate> candidates,
			Func<ConnectCandidate, CancellationToken, Task<T>> connectAsync,
			TimeSpan nat64Delay,
			Action<T> disposeIfLost,
			Action<ConnectCandidate, Exception>? onFailure,
			CancellationToken cancellationToken)
		{
			if (candidates.Count == 0)
				throw new SocketException((int)SocketError.HostUnreachable);

			List<ConnectCandidate> native = candidates
				.Where(c => c.Kind != ConnectCandidateKind.Nat64)
				.ToList();
			List<ConnectCandidate> nat64 = candidates
				.Where(c => c.Kind == ConnectCandidateKind.Nat64)
				.ToList();

			using var raceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			var winner = new TaskCompletionSource<(ConnectCandidate, T)>(
				TaskCreationOptions.RunContinuationsAsynchronously);

			int inflight = 0;
			int nat64Gate = nat64.Count > 0 && native.Count > 0 ? 1 : 0;
			int nat64Launched = 0;
			int nativeRemaining = native.Count;
			Exception? lastNative = null;
			Exception? lastNat64 = null;

			void CompleteIfIdle()
			{
				if (Volatile.Read(ref inflight) != 0 || Volatile.Read(ref nat64Gate) != 0)
					return;
				Exception fail = lastNat64 ?? lastNative ?? new SocketException((int)SocketError.HostUnreachable);
				winner.TrySetException(fail);
			}

			void TryLaunchNat64()
			{
				if (winner.Task.IsCompleted)
				{
					Interlocked.Exchange(ref nat64Gate, 0);
					return;
				}

				if (Interlocked.Exchange(ref nat64Launched, 1) != 0)
					return;

				foreach (ConnectCandidate candidate in nat64)
					_ = RunOne(candidate);
				Interlocked.Exchange(ref nat64Gate, 0);
				CompleteIfIdle();
			}

			async Task RunOne(ConnectCandidate candidate)
			{
				Interlocked.Increment(ref inflight);
				try
				{
					T result = await connectAsync(candidate, raceCts.Token).ConfigureAwait(false);
					if (winner.TrySetResult((candidate, result)))
					{
						try
						{
							raceCts.Cancel();
						}
						catch (ObjectDisposedException)
						{
						}
					}
					else
					{
						try
						{
							disposeIfLost(result);
						}
						catch
						{
						}
					}
				}
				catch (OperationCanceledException)
				{
					// Ganador o cancelación del request.
				}
				catch (Exception ex)
				{
					if (candidate.Kind == ConnectCandidateKind.Nat64)
						lastNat64 = ex;
					else
						lastNative = ex;
					try
					{
						onFailure?.Invoke(candidate, ex);
					}
					catch
					{
					}

					if (candidate.Kind == ConnectCandidateKind.NativeIPv4 && IsUnreachable(ex))
						TryLaunchNat64();
				}
				finally
				{
					if (candidate.Kind != ConnectCandidateKind.Nat64
						&& Interlocked.Decrement(ref nativeRemaining) == 0
						&& !winner.Task.IsCompleted)
					{
						TryLaunchNat64();
					}

					Interlocked.Decrement(ref inflight);
					CompleteIfIdle();
				}
			}

			foreach (ConnectCandidate candidate in native)
				_ = RunOne(candidate);

			if (native.Count == 0)
				TryLaunchNat64();
			else if (nat64.Count > 0)
			{
				_ = DelayNat64Async();
			}

			async Task DelayNat64Async()
			{
				try
				{
					await Task.Delay(nat64Delay, raceCts.Token).ConfigureAwait(false);
					TryLaunchNat64();
				}
				catch (OperationCanceledException)
				{
					if (!winner.Task.IsCompleted)
					{
						Interlocked.Exchange(ref nat64Gate, 0);
						CompleteIfIdle();
					}
				}
			}

			try
			{
				return await winner.Task.ConfigureAwait(false);
			}
			catch (Exception) when (cancellationToken.IsCancellationRequested)
			{
				throw new OperationCanceledException(cancellationToken);
			}
		}

		private static async Task<IPAddress[]?> TryResolveAsync(string host, CancellationToken cancellationToken)
		{
			try
			{
				using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				timeout.CancelAfter(TimeSpan.FromSeconds(4));
				IPAddress[] addresses = await Dns.GetHostAddressesAsync(host, timeout.Token).ConfigureAwait(false);
				return addresses.Length == 0 ? null : addresses;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				mvarLogger?.LogWarning(ex, "OutboundHttp: DNS de {Host} falló.", host);
				return null;
			}
		}

		private static string? SslHostName(SocketsHttpConnectionContext context)
		{
			Uri? uri = context.InitialRequestMessage.RequestUri;
			if (uri is not null && uri.Scheme == Uri.UriSchemeHttps)
				return uri.IdnHost;
			return null;
		}

		private static async Task<Socket> ConnectTcpAsync(
			IPAddress address,
			int port,
			CancellationToken cancellationToken)
		{
			Socket socket = CreateSocket(address.AddressFamily);
			try
			{
				using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				timeoutCts.CancelAfter(TcpConnectTimeout);
				await socket.ConnectAsync(new IPEndPoint(address, port), timeoutCts.Token).ConfigureAwait(false);
				return socket;
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				socket.Dispose();
				throw new SocketException((int)SocketError.TimedOut);
			}
			catch
			{
				socket.Dispose();
				throw;
			}
		}

		private static Socket CreateSocket(AddressFamily family)
		{
			var socket = new Socket(family, SocketType.Stream, ProtocolType.Tcp)
			{
				NoDelay = true
			};
			if (family == AddressFamily.InterNetworkV6)
			{
				try
				{
					socket.DualMode = true;
				}
				catch (SocketException)
				{
				}
			}

			try
			{
				socket.DontFragment = false;
			}
			catch (SocketException)
			{
			}
			catch (NotSupportedException)
			{
			}

			return socket;
		}

		private static async Task<Stream> ConnectSocketAsync(
			DnsEndPoint endPoint,
			string? sslHost,
			CancellationToken cancellationToken)
		{
			Socket? socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
			try
			{
				try
				{
					socket.DontFragment = false;
				}
				catch (SocketException)
				{
				}
				catch (NotSupportedException)
				{
				}

				using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				timeoutCts.CancelAfter(TcpConnectTimeout);
				try
				{
					await socket.ConnectAsync(endPoint, timeoutCts.Token).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
				{
					throw new SocketException((int)SocketError.TimedOut);
				}

				IPAddress? remote = (socket.RemoteEndPoint as IPEndPoint)?.Address;
				Stream stream = new NetworkStream(socket, ownsSocket: true);
				socket = null;
				if (string.IsNullOrEmpty(sslHost) || IPAddress.TryParse(sslHost, out _))
					return stream;
				return await AuthenticateHttpsAsync(
						stream,
						sslHost,
						remote ?? IPAddress.None,
						cancellationToken)
					.ConfigureAwait(false);
			}
			catch
			{
				socket?.Dispose();
				throw;
			}
		}

		private static async Task<Stream> AuthenticateHttpsAsync(
			Stream inner,
			string sslHost,
			IPAddress address,
			CancellationToken cancellationToken)
		{
			var ssl = new SslStream(inner, leaveInnerStreamOpen: false);
			try
			{
				var options = new SslClientAuthenticationOptions
				{
					TargetHost = sslHost,
					EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
					ApplicationProtocols = new List<SslApplicationProtocol>
					{
						SslApplicationProtocol.Http11
					},
					RemoteCertificateValidationCallback = (_, cert, chain, errors) =>
						ValidateServerCertificate(sslHost, address, cert, chain, errors)
				};
				await ssl.AuthenticateAsClientAsync(options, cancellationToken).ConfigureAwait(false);
				return ssl;
			}
			catch
			{
				await ssl.DisposeAsync().ConfigureAwait(false);
				throw;
			}
		}

		internal static bool ValidateServerCertificate(
			string expectedHost,
			IPAddress connectedIp,
			X509Certificate? certificate,
			X509Chain? chain,
			SslPolicyErrors errors)
		{
			if (errors == SslPolicyErrors.None)
				return true;

			_ = chain;
			X509Certificate2? cert2 = certificate as X509Certificate2
				?? (certificate is not null ? new X509Certificate2(certificate) : null);

			// Schannel compara a veces contra la IP del socket, no contra el SNI.
			// SFM: CN=*.trensfm.com. TIB: CN=*.consorcidetransports.com con SAN *.tib.org.
			// Ninguno incluye la IP; el SAN sí cubre info.trensfm.com / www.tib.org.
			if (errors == SslPolicyErrors.RemoteCertificateNameMismatch
				&& cert2 is not null
				&& HostNameMatchesCertificate(expectedHost, cert2))
			{
				mvarLogger?.LogInformation(
					"OutboundHttp: NameMismatch ignorado; {Subject} cubre {Host} (IP {Ip})",
					cert2.Subject,
					expectedHost,
					connectedIp);
				return true;
			}

			mvarLogger?.LogWarning(
				"OutboundHttp: TLS rechazado {Host} vía {Ip}: {Errors}. Cert={Subject} SAN={San}",
				expectedHost,
				connectedIp,
				errors,
				cert2?.Subject ?? "(ninguno)",
				FormatSan(cert2));
			return false;
		}

		internal static bool HostNameMatchesCertificate(string host, X509Certificate2 cert)
		{
			if (string.IsNullOrWhiteSpace(host) || IPAddress.TryParse(host, out _))
				return false;

			foreach (string name in EnumerateDnsNames(cert))
			{
				if (MatchesDnsName(host, name))
					return true;
			}
			return false;
		}

		internal static bool MatchesDnsName(string host, string pattern)
		{
			if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(pattern))
				return false;

			host = host.Trim().TrimEnd('.');
			pattern = pattern.Trim().TrimEnd('.');

			if (pattern.StartsWith("*.", StringComparison.Ordinal))
			{
				string suffix = pattern[1..];
				if (host.Length <= suffix.Length)
					return false;
				if (!host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
					return false;
				string remainder = host[..^suffix.Length];
				return remainder.Length > 0 && remainder.IndexOf('.') < 0;
			}

			return host.Equals(pattern, StringComparison.OrdinalIgnoreCase);
		}

		private static IEnumerable<string> EnumerateDnsNames(X509Certificate2 cert)
		{
			foreach (X509Extension extension in cert.Extensions)
			{
				if (extension is not X509SubjectAlternativeNameExtension san)
					continue;
				foreach (string dns in san.EnumerateDnsNames())
				{
					if (!string.IsNullOrWhiteSpace(dns))
						yield return dns;
				}
			}

			string cn = cert.GetNameInfo(X509NameType.DnsName, forIssuer: false);
			if (!string.IsNullOrWhiteSpace(cn))
				yield return cn;
		}

		private static string FormatSan(X509Certificate2? cert)
		{
			if (cert is null)
				return "(ninguno)";
			var names = new List<string>();
			foreach (string name in EnumerateDnsNames(cert))
			{
				if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
					names.Add(name);
			}
			return names.Count == 0 ? "(ninguno)" : string.Join(", ", names);
		}

		private static void RememberConnect(string host, ConnectCandidate candidate)
		{
			mcolLastConnect[host] = new OutboundConnectInfo(
				candidate.Address,
				candidate.Address.AddressFamily,
				candidate.Kind == ConnectCandidateKind.Nat64,
				candidate.Prefix?.ToString());
		}

		private static void LogConnect(string host, int port, ConnectCandidate candidate)
		{
			string target = $"{candidate.Kind}:{candidate.Address}";
			if (mcolLoggedTargets.TryGetValue(host, out string? previous) && previous == target)
				return;
			mcolLoggedTargets[host] = target;

			string path = candidate.Kind switch
			{
				ConnectCandidateKind.NativeIPv4 => "IPv4",
				ConnectCandidateKind.NativeIPv6 => "IPv6",
				ConnectCandidateKind.Nat64 => $"NAT64 prefix={candidate.Prefix}",
				_ => candidate.Kind.ToString()
			};
			mvarLogger?.LogInformation(
				"OutboundHttp: {Host}:{Port} → {Ip} ({Family} {Path})",
				host,
				port,
				candidate.Address,
				candidate.Address.AddressFamily,
				path);
		}

		internal static bool IsUnreachable(Exception ex)
		{
			foreach (Exception inner in Flatten(ex))
			{
				if (inner is not SocketException socket)
					continue;
				switch (socket.SocketErrorCode)
				{
					case SocketError.HostUnreachable:
					case SocketError.NetworkUnreachable:
					case SocketError.TimedOut:
					case SocketError.NetworkDown:
					case SocketError.HostDown:
					case SocketError.AddressNotAvailable:
					case SocketError.TryAgain:
					case SocketError.HostNotFound:
					case SocketError.NoData:
						return true;
				}
			}

			return false;
		}

		internal static bool IsConnectionReset(Exception ex)
		{
			foreach (Exception inner in Flatten(ex))
			{
				if (inner is SocketException socket)
				{
					switch (socket.SocketErrorCode)
					{
						case SocketError.ConnectionReset:
						case SocketError.ConnectionAborted:
						case SocketError.Shutdown:
							return true;
					}
				}

				if (inner is AuthenticationException)
					return true;

				if (inner is IOException)
				{
					string message = inner.Message;
					if (message.Contains("forzada", StringComparison.OrdinalIgnoreCase)
						|| message.Contains("forcibly", StringComparison.OrdinalIgnoreCase)
						|| message.Contains("interrupted", StringComparison.OrdinalIgnoreCase)
						|| message.Contains("connection was reset", StringComparison.OrdinalIgnoreCase)
						|| message.Contains("conexión existente", StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
				}
			}

			return false;
		}

		private static IEnumerable<Exception> Flatten(Exception ex)
		{
			var stack = new Stack<Exception>();
			stack.Push(ex);
			while (stack.Count > 0)
			{
				Exception current = stack.Pop();
				yield return current;
				if (current is AggregateException aggregate)
				{
					foreach (Exception child in aggregate.InnerExceptions)
						stack.Push(child);
				}
				else if (current.InnerException is not null)
				{
					stack.Push(current.InnerException);
				}
			}
		}

		private static bool IsUsable(IPAddress address)
		{
			if (address.AddressFamily is not AddressFamily.InterNetwork and not AddressFamily.InterNetworkV6)
				return false;
			if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal)
				return false;
			return true;
		}
	}

	internal enum ConnectCandidateKind
	{
		NativeIPv4,
		NativeIPv6,
		Nat64
	}

	internal sealed record ConnectCandidate(
		IPAddress Address,
		ConnectCandidateKind Kind,
		IPAddress? SourceIPv4 = null,
		Nat64Prefix? Prefix = null);

	internal sealed record OutboundConnectInfo(
		IPAddress Address,
		AddressFamily Family,
		bool IsNat64,
		string? Nat64Prefix);
}

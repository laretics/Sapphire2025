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
	/// HTTP de salida pensado para la red del tren: IPv4 primero,
	/// HTTP/1.1 (los WAF de TIB/SFM se llevan mal con HTTP/2), cabeceras de navegador
	/// y conexión por la última IP conocida (SFM/TIB/EMT) si DNS no responde.
	/// </summary>
	internal static class OutboundHttp
	{
		public const string BrowserUserAgent =
			"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

		private static readonly object mvarInitLock = new();
		private static readonly ConcurrentDictionary<string, byte> mcolLoggedHosts =
			new(StringComparer.OrdinalIgnoreCase);

		private static ILogger? mvarLogger;
		private static OutboundHostCache? mvarCache;

		public static void Configure(string contentRoot, ILoggerFactory? loggerFactory)
		{
			ILogger? logger = loggerFactory?.CreateLogger("Tourmaline26.Services.Http.OutboundHttp");
			string path = System.IO.Path.Combine(contentRoot, OutboundHostCache.RelativePath);
			lock (mvarInitLock)
			{
				mvarLogger = logger;
				mvarCache = OutboundHostCache.Load(path, logger);
			}
		}

		private static OutboundHostCache Cache
		{
			get
			{
				OutboundHostCache? cache = mvarCache;
				if (cache is not null)
					return cache;
				lock (mvarInitLock)
				{
					mvarCache ??= OutboundHostCache.Load(
						System.IO.Path.Combine(AppContext.BaseDirectory, OutboundHostCache.RelativePath),
						mvarLogger);
					return mvarCache;
				}
			}
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
				ConnectCallback = ConnectPreferIPv4Async
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

		private static async ValueTask<Stream> ConnectPreferIPv4Async(
			SocketsHttpConnectionContext context,
			CancellationToken cancellationToken)
		{
			string host = context.DnsEndPoint.Host;
			int port = context.DnsEndPoint.Port;
			string? sslHost = SslHostName(context);

			if (IPAddress.TryParse(host, out IPAddress? literal))
				return await ConnectAddressAsync(literal, port, sslHost, cancellationToken).ConfigureAwait(false);

			IReadOnlyList<IPAddress> cached = Cache.Candidates(host);
			if (cached.Count > 0)
			{
				QueueDnsRefresh(host);
				mvarLogger?.LogDebug(
					"OutboundHttp: {Host}:{Port} vía caché {Ips}",
					host,
					port,
					string.Join(", ", cached.Select(a => a.ToString())));

				Exception? cacheLast = null;
				var failed = new HashSet<IPAddress>();
				foreach (IPAddress address in cached)
				{
					cancellationToken.ThrowIfCancellationRequested();
					try
					{
						Stream stream = await ConnectAddressAsync(address, port, sslHost, cancellationToken)
							.ConfigureAwait(false);
						Cache.RememberSuccess(host, address);
						LogConnectOnce(host, port, address, fromCache: true);
						return stream;
					}
					catch (Exception ex) when (ex is not OperationCanceledException)
					{
						failed.Add(address);
						cacheLast = ex;
						mvarLogger?.LogWarning(
							ex,
							"OutboundHttp: {Host}:{Port} no usable en {Ip} (caché).",
							host,
							port,
							address);
					}
				}

				IPAddress[]? resolved = await TryResolveAsync(host, cancellationToken).ConfigureAwait(false);
				if (resolved is { Length: > 0 })
				{
					Cache.UpdateFromDns(host, resolved);
					IPAddress[] fresh = resolved.Where(a => !failed.Contains(a)).ToArray();
					if (fresh.Length > 0)
					{
						Stream? dnsStream = await TryConnectListAsync(host, port, sslHost, fresh, cancellationToken)
							.ConfigureAwait(false);
						if (dnsStream is not null)
						{
							mvarLogger?.LogInformation(
								"OutboundHttp: DNS resuelto para {Host}: {Ips}",
								host,
								string.Join(", ", resolved.Select(a => a.ToString())));
							return dnsStream;
						}
					}
				}

				throw cacheLast ?? new SocketException((int)SocketError.HostUnreachable);
			}

			IPAddress[]? firstResolve = await TryResolveAsync(host, cancellationToken).ConfigureAwait(false);
			if (firstResolve is { Length: > 0 })
			{
				Cache.UpdateFromDns(host, firstResolve);
				Stream? stream = await TryConnectListAsync(host, port, sslHost, firstResolve, cancellationToken)
					.ConfigureAwait(false);
				if (stream is not null)
					return stream;
			}

			return await ConnectSocketAsync(context.DnsEndPoint, sslHost, cancellationToken).ConfigureAwait(false);
		}

		private static async Task<Stream?> TryConnectListAsync(
			string host,
			int port,
			string? sslHost,
			IReadOnlyList<IPAddress> addresses,
			CancellationToken cancellationToken)
		{
			IEnumerable<IPAddress> ordered = addresses
				.Where(IsUsable)
				.Where(a => a.AddressFamily == AddressFamily.InterNetwork)
				.Concat(addresses.Where(IsUsable).Where(a => a.AddressFamily == AddressFamily.InterNetworkV6));

			Exception? last = null;
			var seen = new HashSet<IPAddress>();
			foreach (IPAddress address in ordered)
			{
				if (!seen.Add(address))
					continue;
				cancellationToken.ThrowIfCancellationRequested();
				try
				{
					Stream stream = await ConnectAddressAsync(address, port, sslHost, cancellationToken)
						.ConfigureAwait(false);
					Cache.RememberSuccess(host, address);
					LogConnectOnce(host, port, address, fromCache: false);
					return stream;
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					last = ex;
				}
			}

			if (last is not null)
				mvarLogger?.LogDebug(last, "OutboundHttp: no se pudo conectar a {Host} por las IPs DNS.", host);
			return null;
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
				mvarLogger?.LogWarning(ex, "OutboundHttp: DNS de {Host} falló; se mantiene la caché.", host);
				return null;
			}
		}

		private static void QueueDnsRefresh(string host)
		{
			if (!Cache.TryBeginDnsRefresh(host))
				return;

			_ = Task.Run(async () =>
			{
				try
				{
					using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
					IPAddress[] addresses = await Dns.GetHostAddressesAsync(host, timeout.Token)
						.ConfigureAwait(false);
					if (addresses.Length > 0)
						Cache.UpdateFromDns(host, addresses);
				}
				catch (OperationCanceledException)
				{
					// Timeout de refresco: se sigue con la IP persistida.
				}
				catch (Exception ex)
				{
					mvarLogger?.LogDebug(ex, "OutboundHttp: refresco DNS de {Host} omitido.", host);
				}
				finally
				{
					Cache.EndDnsRefresh(host);
				}
			});
		}

		private static string? SslHostName(SocketsHttpConnectionContext context)
		{
			Uri? uri = context.InitialRequestMessage.RequestUri;
			if (uri is not null && uri.Scheme == Uri.UriSchemeHttps)
				return uri.IdnHost;
			return null;
		}

		/// <summary>
		/// TCP a la IP y, si el destino es HTTPS, TLS aquí mismo con SNI = hostname
		/// (nunca la IP). Si solo devolvemos el socket, Schannel en algunos equipos
		/// valida el certificado contra 213.99.47.36 → RemoteCertificateNameMismatch
		/// aunque el cert sea *.trensfm.com.
		/// </summary>
		private static async Task<Stream> ConnectAddressAsync(
			IPAddress address,
			int port,
			string? sslHost,
			CancellationToken cancellationToken)
		{
			Socket? socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
			{
				NoDelay = true
			};
			try
			{
				await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken).ConfigureAwait(false);
				Stream stream = new NetworkStream(socket, ownsSocket: true);
				socket = null;
				if (string.IsNullOrEmpty(sslHost) || IPAddress.TryParse(sslHost, out _))
					return stream;
				return await AuthenticateHttpsAsync(stream, sslHost, address, cancellationToken)
					.ConfigureAwait(false);
			}
			catch
			{
				socket?.Dispose();
				throw;
			}
		}

		private static async Task<Stream> ConnectSocketAsync(
			DnsEndPoint endPoint,
			string? sslHost,
			CancellationToken cancellationToken)
		{
			Socket? socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
			try
			{
				await socket.ConnectAsync(endPoint, cancellationToken).ConfigureAwait(false);
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

		private static void LogConnectOnce(string host, int port, IPAddress address, bool fromCache)
		{
			if (!mcolLoggedHosts.TryAdd(host, 0))
				return;
			mvarLogger?.LogInformation(
				fromCache
					? "OutboundHttp: {Host}:{Port} → {Ip} (caché persistida, DNS no bloquea)"
					: "OutboundHttp: {Host}:{Port} → {Ip} (DNS)",
				host,
				port,
				address);
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
}

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

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
				ConnectTimeout = TimeSpan.FromSeconds(12),
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

			if (IPAddress.TryParse(host, out IPAddress? literal))
				return await ConnectAddressAsync(literal, port, cancellationToken).ConfigureAwait(false);

			IReadOnlyList<IPAddress> cached = Cache.Candidates(host);
			if (cached.Count > 0)
			{
				QueueDnsRefresh(host);
				Exception? cacheLast = null;
				var failed = new HashSet<IPAddress>();
				foreach (IPAddress address in cached)
				{
					cancellationToken.ThrowIfCancellationRequested();
					try
					{
						Stream stream = await ConnectAddressAsync(address, port, cancellationToken)
							.ConfigureAwait(false);
						Cache.RememberSuccess(host, address);
						LogConnectOnce(host, port, address, fromCache: true);
						return stream;
					}
					catch (Exception ex) when (ex is not OperationCanceledException)
					{
						failed.Add(address);
						cacheLast = ex;
					}
				}

				IPAddress[]? resolved = await TryResolveAsync(host, cancellationToken).ConfigureAwait(false);
				if (resolved is { Length: > 0 })
				{
					Cache.UpdateFromDns(host, resolved);
					IPAddress[] fresh = resolved.Where(a => !failed.Contains(a)).ToArray();
					if (fresh.Length > 0)
					{
						Stream? dnsStream = await TryConnectListAsync(host, port, fresh, cancellationToken)
							.ConfigureAwait(false);
						if (dnsStream is not null)
							return dnsStream;
					}
				}

				throw cacheLast ?? new SocketException((int)SocketError.HostUnreachable);
			}

			IPAddress[]? firstResolve = await TryResolveAsync(host, cancellationToken).ConfigureAwait(false);
			if (firstResolve is { Length: > 0 })
			{
				Cache.UpdateFromDns(host, firstResolve);
				Stream? stream = await TryConnectListAsync(host, port, firstResolve, cancellationToken)
					.ConfigureAwait(false);
				if (stream is not null)
					return stream;
			}

			return await ConnectSocketAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false);
		}

		private static async Task<Stream?> TryConnectListAsync(
			string host,
			int port,
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
					Stream stream = await ConnectAddressAsync(address, port, cancellationToken)
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

		private static async Task<Stream> ConnectAddressAsync(
			IPAddress address,
			int port,
			CancellationToken cancellationToken)
		{
			var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
			{
				NoDelay = true
			};
			try
			{
				await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken).ConfigureAwait(false);
				return new NetworkStream(socket, ownsSocket: true);
			}
			catch
			{
				socket.Dispose();
				throw;
			}
		}

		private static async Task<Stream> ConnectSocketAsync(
			DnsEndPoint endPoint,
			CancellationToken cancellationToken)
		{
			var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
			try
			{
				await socket.ConnectAsync(endPoint, cancellationToken).ConfigureAwait(false);
				return new NetworkStream(socket, ownsSocket: true);
			}
			catch
			{
				socket.Dispose();
				throw;
			}
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

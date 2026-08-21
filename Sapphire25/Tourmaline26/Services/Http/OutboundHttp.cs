using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace Tourmaline26.Services.Http
{
	/// <summary>
	/// HTTP de salida pensado para la red del tren: IPv4 primero,
	/// HTTP/1.1 (los WAF de TIB/SFM se llevan mal con HTTP/2) y cabeceras de navegador.
	/// </summary>
	internal static class OutboundHttp
	{
		public const string BrowserUserAgent =
			"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

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
			IPAddress[] addresses;
			try
			{
				addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken)
					.ConfigureAwait(false);
			}
			catch (Exception)
			{
				return await ConnectSocketAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false);
			}

			IEnumerable<IPAddress> ordered = addresses
				.Where(a => a.AddressFamily == AddressFamily.InterNetwork)
				.Concat(addresses.Where(a => a.AddressFamily == AddressFamily.InterNetworkV6));

			Exception? last = null;
			foreach (IPAddress address in ordered)
			{
				var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
				{
					NoDelay = true
				};
				try
				{
					await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken)
						.ConfigureAwait(false);
					return new NetworkStream(socket, ownsSocket: true);
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					last = ex;
					socket.Dispose();
				}
			}

			throw last ?? new SocketException((int)SocketError.HostUnreachable);
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
	}
}

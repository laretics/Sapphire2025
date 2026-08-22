using Tourmaline26.Services.Http;

namespace Tourmaline26.Tests;

public sealed class OutboundHttpLiveTests
{
	[Fact]
	public async Task SocketIo_handshake_through_cached_ip_and_sni()
	{
		using var handler = OutboundHttp.CreateHandler();
		using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(25) };
		OutboundHttp.ApplyBrowserDefaults(client, "https://info.trensfm.com");
		string url =
			$"https://info.trensfm.com/socket.io/?EIO=4&transport=polling&t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
		using HttpResponseMessage resp = await client.GetAsync(url);
		string body = await resp.Content.ReadAsStringAsync();
		Assert.True(resp.IsSuccessStatusCode, $"{resp.StatusCode}: {body}");
		Assert.StartsWith("0{", body);
	}

	[Fact]
	public async Task Tib_departures_through_cached_ip_and_sni()
	{
		using var handler = OutboundHttp.CreateHandler();
		using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(25) };
		client.DefaultRequestHeaders.Accept.Add(
			new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
		client.DefaultRequestHeaders.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
		OutboundHttp.ApplyBrowserDefaults(client, "https://www.tib.org");
		const string url =
			"https://www.tib.org/o/manager/stop-code/40036/departures/ctmr4?res=20&groupId=20124";
		using HttpResponseMessage resp = await client.GetAsync(url);
		string body = await resp.Content.ReadAsStringAsync();
		Assert.True(resp.IsSuccessStatusCode, $"{resp.StatusCode}: {body}");
		Assert.StartsWith("[", body.TrimStart());
		Assert.Contains("trip_id", body, StringComparison.Ordinal);
	}
}

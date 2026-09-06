using System.Net;
using System.Net.Sockets;
using Tourmaline26.Services.Http;

namespace Tourmaline26.Tests;

public sealed class Nat64Tests
{
	[Fact]
	public void Synthesize_well_known_prefix_embeds_tib_ipv4()
	{
		IPAddress synthesized = Nat64Prefix.WellKnown.Synthesize(IPAddress.Parse("85.62.90.188"));
		Assert.Equal(IPAddress.Parse("64:ff9b::553e:5abc"), synthesized);
	}

	[Theory]
	[InlineData("64:ff9b::/96", 96)]
	[InlineData("64:ff9b::", 96)]
	[InlineData("2001:db8:64::/64", 64)]
	public void TryParse_rfc6052_lengths(string text, int length)
	{
		Assert.True(Nat64Prefix.TryParse(text, out Nat64Prefix prefix));
		Assert.Equal(length, prefix.PrefixLength);
	}

	[Fact]
	public void TryExtractPref64_well_known_ipv4only_arpa()
	{
		// 192.0.0.170 = c000:00aa → 64:ff9b::c000:aa
		IPAddress aaaa = IPAddress.Parse("64:ff9b::c000:aa");
		Assert.True(Nat64.TryExtractPref64(aaaa, out Nat64Prefix prefix));
		Assert.Equal(96, prefix.PrefixLength);
		Assert.Equal(IPAddress.Parse("64:ff9b::"), prefix.Network);
		Assert.Equal(aaaa, prefix.Synthesize(IPAddress.Parse("192.0.0.170")));
	}

	[Fact]
	public void TryExtractPref64_rejects_unrelated_aaaa()
	{
		Assert.False(Nat64.TryExtractPref64(IPAddress.Parse("2001:db8::1"), out _));
	}

	[Fact]
	public void IsUnreachable_socket_10065_10051_10060()
	{
		Assert.True(OutboundHttp.IsUnreachable(new SocketException((int)SocketError.HostUnreachable)));
		Assert.True(OutboundHttp.IsUnreachable(new SocketException((int)SocketError.NetworkUnreachable)));
		Assert.True(OutboundHttp.IsUnreachable(new SocketException((int)SocketError.TimedOut)));
		Assert.False(OutboundHttp.IsUnreachable(new SocketException((int)SocketError.ConnectionReset)));
	}

	[Fact]
	public void IsConnectionReset_socket_10054_and_ssl_text()
	{
		Assert.True(OutboundHttp.IsConnectionReset(new SocketException((int)SocketError.ConnectionReset)));
		Assert.True(OutboundHttp.IsConnectionReset(
			new HttpRequestException(
				"The SSL connection could not be established",
				new IOException("An existing connection was forcibly closed by the remote host."))));
		Assert.True(OutboundHttp.IsConnectionReset(
			new IOException("La conexión existente fue forzada a interrumpirse")));
		Assert.False(OutboundHttp.IsConnectionReset(new SocketException((int)SocketError.HostUnreachable)));
	}
}

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Tourmaline26.Services.Http;

namespace Tourmaline26.Tests;

public sealed class OutboundConnectTests
{
	private static readonly IPAddress TibV4 = IPAddress.Parse("85.62.90.188");
	private static readonly IPAddress TibNat64 = IPAddress.Parse("64:ff9b::553e:5abc");
	private static readonly IPAddress ExampleAaaa = IPAddress.Parse("2001:db8::1");

	[Fact]
	public void BuildConnectCandidates_a_only_with_ipv6_synthesizes_nat64()
	{
		IReadOnlyList<ConnectCandidate> candidates = OutboundHttp.BuildConnectCandidates(
			[TibV4],
			[Nat64Prefix.WellKnown],
			ipv6Available: true);

		Assert.Equal(2, candidates.Count);
		Assert.Equal(ConnectCandidateKind.NativeIPv4, candidates[0].Kind);
		Assert.Equal(TibV4, candidates[0].Address);
		Assert.Equal(ConnectCandidateKind.Nat64, candidates[1].Kind);
		Assert.Equal(TibNat64, candidates[1].Address);
		Assert.Equal(TibV4, candidates[1].SourceIPv4);
	}

	[Fact]
	public void BuildConnectCandidates_without_ipv6_is_ipv4_only()
	{
		IReadOnlyList<ConnectCandidate> candidates = OutboundHttp.BuildConnectCandidates(
			[TibV4, ExampleAaaa],
			[Nat64Prefix.WellKnown],
			ipv6Available: false);

		Assert.Single(candidates);
		Assert.Equal(ConnectCandidateKind.NativeIPv4, candidates[0].Kind);
		Assert.Equal(TibV4, candidates[0].Address);
	}

	[Fact]
	public void BuildConnectCandidates_aaaa_is_native_v6_not_only_nat64()
	{
		IReadOnlyList<ConnectCandidate> candidates = OutboundHttp.BuildConnectCandidates(
			[TibV4, ExampleAaaa],
			[Nat64Prefix.WellKnown],
			ipv6Available: true);

		Assert.Contains(candidates, c => c.Kind == ConnectCandidateKind.NativeIPv4 && c.Address.Equals(TibV4));
		Assert.Contains(candidates, c => c.Kind == ConnectCandidateKind.NativeIPv6 && c.Address.Equals(ExampleAaaa));
		Assert.Contains(candidates, c => c.Kind == ConnectCandidateKind.Nat64 && c.Address.Equals(TibNat64));
	}

	[Fact]
	public async Task Race_ipv4_10065_tries_nat64_immediately()
	{
		IReadOnlyList<ConnectCandidate> candidates = OutboundHttp.BuildConnectCandidates(
			[TibV4],
			[Nat64Prefix.WellKnown],
			ipv6Available: true);

		var attempted = new ConcurrentBag<ConnectCandidateKind>();
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

		(ConnectCandidate winner, _) = await OutboundHttp.RaceCandidatesAsync(
			candidates,
			(c, ct) =>
			{
				attempted.Add(c.Kind);
				if (c.Kind == ConnectCandidateKind.NativeIPv4)
					throw new SocketException((int)SocketError.HostUnreachable);
				if (c.Kind == ConnectCandidateKind.Nat64)
					return Task.FromResult(c);
				throw new InvalidOperationException(c.Kind.ToString());
			},
			TimeSpan.FromMilliseconds(250),
			_ => { },
			onFailure: null,
			timeout.Token);

		Assert.Equal(ConnectCandidateKind.Nat64, winner.Kind);
		Assert.Equal(TibNat64, winner.Address);
		Assert.Contains(ConnectCandidateKind.NativeIPv4, attempted);
		Assert.Contains(ConnectCandidateKind.Nat64, attempted);
	}

	[Fact]
	public async Task Race_aaaa_wins_before_nat64_is_launched()
	{
		IReadOnlyList<ConnectCandidate> candidates = OutboundHttp.BuildConnectCandidates(
			[TibV4, ExampleAaaa],
			[Nat64Prefix.WellKnown],
			ipv6Available: true);

		int nat64Attempts = 0;
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

		(ConnectCandidate winner, _) = await OutboundHttp.RaceCandidatesAsync(
			candidates,
			async (c, ct) =>
			{
				if (c.Kind == ConnectCandidateKind.NativeIPv4)
				{
					await Task.Delay(TimeSpan.FromSeconds(5), ct);
					return c;
				}

				if (c.Kind == ConnectCandidateKind.NativeIPv6)
					return c;

				Interlocked.Increment(ref nat64Attempts);
				return c;
			},
			TimeSpan.FromMilliseconds(250),
			_ => { },
			onFailure: null,
			timeout.Token);

		Assert.Equal(ConnectCandidateKind.NativeIPv6, winner.Kind);
		Assert.Equal(ExampleAaaa, winner.Address);
		Assert.Equal(0, nat64Attempts);
	}

	[Fact]
	public async Task Race_without_ipv6_does_not_synthesize_and_surfaces_10065()
	{
		IReadOnlyList<ConnectCandidate> candidates = OutboundHttp.BuildConnectCandidates(
			[TibV4],
			[Nat64Prefix.WellKnown],
			ipv6Available: false);

		Assert.DoesNotContain(candidates, c => c.Kind == ConnectCandidateKind.Nat64);

		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
		SocketException ex = await Assert.ThrowsAsync<SocketException>(() =>
			OutboundHttp.RaceCandidatesAsync(
				candidates,
				(_, _) => Task.FromException<ConnectCandidate>(
					new SocketException((int)SocketError.HostUnreachable)),
				TimeSpan.FromMilliseconds(250),
				_ => { },
				onFailure: null,
				timeout.Token));

		Assert.Equal(SocketError.HostUnreachable, ex.SocketErrorCode);
	}
}

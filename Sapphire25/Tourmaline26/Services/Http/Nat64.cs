using System.Net;
using System.Net.Sockets;

namespace Tourmaline26.Services.Http
{
	/// <summary>
	/// Prefijo NAT64 (RFC 6052) y descubrimiento PREF64 (RFC 7050, ipv4only.arpa).
	/// </summary>
	internal readonly struct Nat64Prefix : IEquatable<Nat64Prefix>
	{
		public static readonly Nat64Prefix WellKnown = Parse("64:ff9b::/96");

		public IPAddress Network { get; }
		public int PrefixLength { get; }

		public Nat64Prefix(IPAddress network, int prefixLength)
		{
			if (network.AddressFamily != AddressFamily.InterNetworkV6)
				throw new ArgumentException("El prefijo NAT64 debe ser IPv6.", nameof(network));
			if (prefixLength is not (32 or 40 or 48 or 56 or 64 or 96))
				throw new ArgumentOutOfRangeException(nameof(prefixLength), prefixLength, "RFC 6052: 32, 40, 48, 56, 64 o 96.");

			byte[] bytes = network.GetAddressBytes();
			Nat64.ZeroBitsAfter(bytes, prefixLength);
			if (prefixLength <= 64)
				bytes[8] = 0;
			Network = new IPAddress(bytes);
			PrefixLength = prefixLength;
		}

		public IPAddress Synthesize(IPAddress ipv4) => Nat64.Synthesize(ipv4, this);

		public override string ToString() => $"{Network}/{PrefixLength}";

		public bool Equals(Nat64Prefix other) =>
			PrefixLength == other.PrefixLength && Network.Equals(other.Network);

		public override bool Equals(object? obj) => obj is Nat64Prefix other && Equals(other);

		public override int GetHashCode() => HashCode.Combine(Network, PrefixLength);

		public static bool operator ==(Nat64Prefix left, Nat64Prefix right) => left.Equals(right);

		public static bool operator !=(Nat64Prefix left, Nat64Prefix right) => !left.Equals(right);

		public static Nat64Prefix Parse(string text)
		{
			if (!TryParse(text, out Nat64Prefix prefix))
				throw new FormatException($"Prefijo NAT64 no válido: '{text}'.");
			return prefix;
		}

		public static bool TryParse(string? text, out Nat64Prefix prefix)
		{
			prefix = default;
			if (string.IsNullOrWhiteSpace(text))
				return false;

			text = text.Trim();
			int slash = text.LastIndexOf('/');
			string addrPart = slash >= 0 ? text[..slash] : text;
			int length = 96;
			if (slash >= 0)
			{
				if (!int.TryParse(text[(slash + 1)..], out length))
					return false;
			}

			if (!IPAddress.TryParse(addrPart, out IPAddress? network)
				|| network.AddressFamily != AddressFamily.InterNetworkV6)
			{
				return false;
			}

			if (length is not (32 or 40 or 48 or 56 or 64 or 96))
				return false;

			prefix = new Nat64Prefix(network, length);
			return true;
		}
	}

	internal static class Nat64
	{
		public static readonly IPAddress Ipv4OnlyArpa170 = IPAddress.Parse("192.0.0.170");
		public static readonly IPAddress Ipv4OnlyArpa171 = IPAddress.Parse("192.0.0.171");
		public const string Ipv4OnlyArpaHost = "ipv4only.arpa";

		private static readonly int[] PrefixLengths = [96, 64, 56, 48, 40, 32];

		public static IPAddress Synthesize(IPAddress ipv4, Nat64Prefix prefix)
		{
			if (ipv4.AddressFamily != AddressFamily.InterNetwork)
				throw new ArgumentException("NAT64 sintetiza a partir de IPv4.", nameof(ipv4));

			byte[] v4 = ipv4.GetAddressBytes();
			byte[] v6 = new byte[16];
			byte[] pref = prefix.Network.GetAddressBytes();
			CopyBits(v6, 0, pref, 0, prefix.PrefixLength);

			switch (prefix.PrefixLength)
			{
				case 32:
					CopyBits(v6, 32, v4, 0, 32);
					CopyBits(v6, 72, pref, 72, 56);
					break;
				case 40:
					CopyBits(v6, 40, v4, 0, 24);
					CopyBits(v6, 72, v4, 24, 8);
					CopyBits(v6, 80, pref, 80, 48);
					break;
				case 48:
					CopyBits(v6, 48, v4, 0, 16);
					CopyBits(v6, 72, v4, 16, 16);
					CopyBits(v6, 88, pref, 88, 40);
					break;
				case 56:
					CopyBits(v6, 56, v4, 0, 8);
					CopyBits(v6, 72, v4, 8, 24);
					CopyBits(v6, 96, pref, 96, 32);
					break;
				case 64:
					CopyBits(v6, 72, v4, 0, 32);
					CopyBits(v6, 104, pref, 104, 24);
					break;
				case 96:
					CopyBits(v6, 96, v4, 0, 32);
					break;
			}

			if (prefix.PrefixLength <= 64)
				v6[8] = 0;

			return new IPAddress(v6);
		}

		public static bool TryExtractPref64(IPAddress aaaa, out Nat64Prefix prefix)
		{
			prefix = default;
			if (aaaa.AddressFamily != AddressFamily.InterNetworkV6)
				return false;

			byte[] v6 = aaaa.GetAddressBytes();
			foreach (int length in PrefixLengths)
			{
				byte[] extracted = ExtractIpv4(v6, length);
				if (!IsWellKnownIpv4Only(extracted))
					continue;

				byte[] network = new byte[16];
				Buffer.BlockCopy(v6, 0, network, 0, 16);
				ZeroBitsAfter(network, length);
				if (length <= 64)
					network[8] = 0;
				prefix = new Nat64Prefix(new IPAddress(network), length);
				return true;
			}

			return false;
		}

		public static bool IsWellKnownIpv4Only(IPAddress ipv4)
		{
			return ipv4.Equals(Ipv4OnlyArpa170) || ipv4.Equals(Ipv4OnlyArpa171);
		}

		private static bool IsWellKnownIpv4Only(byte[] v4)
		{
			return Matches(v4, Ipv4OnlyArpa170) || Matches(v4, Ipv4OnlyArpa171);
		}

		private static bool Matches(byte[] v4, IPAddress known)
		{
			byte[] k = known.GetAddressBytes();
			return v4[0] == k[0] && v4[1] == k[1] && v4[2] == k[2] && v4[3] == k[3];
		}

		private static byte[] ExtractIpv4(byte[] v6, int prefixLength)
		{
			byte[] v4 = new byte[4];
			switch (prefixLength)
			{
				case 32:
					CopyBits(v4, 0, v6, 32, 32);
					break;
				case 40:
					CopyBits(v4, 0, v6, 40, 24);
					CopyBits(v4, 24, v6, 72, 8);
					break;
				case 48:
					CopyBits(v4, 0, v6, 48, 16);
					CopyBits(v4, 16, v6, 72, 16);
					break;
				case 56:
					CopyBits(v4, 0, v6, 56, 8);
					CopyBits(v4, 8, v6, 72, 24);
					break;
				case 64:
					CopyBits(v4, 0, v6, 72, 32);
					break;
				default:
					CopyBits(v4, 0, v6, 96, 32);
					break;
			}

			return v4;
		}

		internal static void ZeroBitsAfter(byte[] bytes, int prefixLength)
		{
			int fullBytes = prefixLength / 8;
			int rem = prefixLength % 8;
			if (rem != 0 && fullBytes < bytes.Length)
			{
				byte mask = (byte)(0xFF << (8 - rem));
				bytes[fullBytes] &= mask;
				fullBytes++;
			}

			for (int i = fullBytes; i < bytes.Length; i++)
				bytes[i] = 0;
		}

		internal static void CopyBits(byte[] dest, int destBitOffset, byte[] src, int srcBitOffset, int bitCount)
		{
			for (int i = 0; i < bitCount; i++)
			{
				int sBit = srcBitOffset + i;
				int dBit = destBitOffset + i;
				int sByte = sBit / 8;
				int dByte = dBit / 8;
				if ((uint)sByte >= (uint)src.Length || (uint)dByte >= (uint)dest.Length)
					return;

				int sOff = 7 - (sBit % 8);
				int dOff = 7 - (dBit % 8);
				int bit = (src[sByte] >> sOff) & 1;
				if (bit == 1)
					dest[dByte] |= (byte)(1 << dOff);
				else
					dest[dByte] &= (byte)~(1 << dOff);
			}
		}
	}

	/// <summary>
	/// PREF64 descubierto (RFC 7050) + prefijo de appsettings, cache 8 min.
	/// Prioridad: descubierto, configurado, 64:ff9b::/96.
	/// </summary>
	internal static class Nat64PrefixCache
	{
		public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(8);
		private static readonly TimeSpan DiscoverTimeout = TimeSpan.FromSeconds(1.5);

		private static readonly object mvarLock = new();
		private static ILogger? mvarLogger;
		private static Nat64Prefix? mvarConfigured;
		private static Nat64Prefix? mvarDiscovered;
		private static DateTime mvarAttemptUtc;
		private static Task<Nat64Prefix?>? mvarInFlight;

		public static void Configure(string? configuredPrefix, ILogger? logger)
		{
			lock (mvarLock)
			{
				mvarLogger = logger;
				if (string.IsNullOrWhiteSpace(configuredPrefix))
				{
					mvarConfigured = null;
					return;
				}

				if (Nat64Prefix.TryParse(configuredPrefix, out Nat64Prefix prefix))
				{
					mvarConfigured = prefix;
					mvarLogger?.LogInformation("OutboundHttp: NAT64 prefijo forzado {Prefix}", prefix);
				}
				else
				{
					mvarConfigured = null;
					mvarLogger?.LogWarning(
						"OutboundHttp: OutboundHttp:Nat64Prefix '{Value}' no es un prefijo RFC 6052; se ignora.",
						configuredPrefix);
				}
			}
		}

		public static async ValueTask<IReadOnlyList<Nat64Prefix>> GetPrefixesAsync(CancellationToken cancellationToken)
		{
			Nat64Prefix? discovered = GetCachedDiscovered();
			if (discovered is null)
				discovered = await DiscoverOnceAsync(cancellationToken).ConfigureAwait(false);

			Nat64Prefix? configured;
			lock (mvarLock)
				configured = mvarConfigured;

			var list = new List<Nat64Prefix>(3);
			void Add(Nat64Prefix prefix)
			{
				for (int i = 0; i < list.Count; i++)
				{
					if (list[i] == prefix)
						return;
				}

				list.Add(prefix);
			}

			if (discovered is not null)
				Add(discovered.Value);
			if (configured is not null)
				Add(configured.Value);
			Add(Nat64Prefix.WellKnown);
			return list;
		}

		private static Nat64Prefix? GetCachedDiscovered()
		{
			lock (mvarLock)
			{
				if (mvarAttemptUtc != default && DateTime.UtcNow - mvarAttemptUtc < Ttl)
					return mvarDiscovered;
			}

			return null;
		}

		private static bool DiscoveryFresh()
		{
			lock (mvarLock)
				return mvarAttemptUtc != default && DateTime.UtcNow - mvarAttemptUtc < Ttl;
		}

		private static async Task<Nat64Prefix?> DiscoverOnceAsync(CancellationToken cancellationToken)
		{
			if (DiscoveryFresh())
				return GetCachedDiscovered();

			Task<Nat64Prefix?> task;
			lock (mvarLock)
			{
				if (mvarAttemptUtc != default && DateTime.UtcNow - mvarAttemptUtc < Ttl)
					return mvarDiscovered;
				mvarInFlight ??= DiscoverCoreAsync();
				task = mvarInFlight;
			}

			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(DiscoverTimeout);
			try
			{
				return await task.WaitAsync(timeout.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				return null;
			}
		}

		private static async Task<Nat64Prefix?> DiscoverCoreAsync()
		{
			try
			{
				using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
				IPAddress[] addresses = await Dns
					.GetHostAddressesAsync(Nat64.Ipv4OnlyArpaHost, AddressFamily.InterNetworkV6, timeout.Token)
					.ConfigureAwait(false);

				foreach (IPAddress aaaa in addresses)
				{
					if (!Nat64.TryExtractPref64(aaaa, out Nat64Prefix prefix))
						continue;

					lock (mvarLock)
					{
						mvarDiscovered = prefix;
						mvarAttemptUtc = DateTime.UtcNow;
						mvarInFlight = null;
					}

					mvarLogger?.LogInformation(
						"OutboundHttp: PREF64 RFC 7050 {Prefix} (ipv4only.arpa → {Aaaa})",
						prefix,
						aaaa);
					return prefix;
				}
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				mvarLogger?.LogDebug(ex, "OutboundHttp: PREF64 ipv4only.arpa no disponible; se usa 64:ff9b::/96.");
			}
			catch (OperationCanceledException)
			{
				mvarLogger?.LogDebug("OutboundHttp: PREF64 ipv4only.arpa timeout; se usa 64:ff9b::/96.");
			}

			lock (mvarLock)
			{
				mvarAttemptUtc = DateTime.UtcNow;
				mvarInFlight = null;
			}
			return null;
		}

		internal static void ResetForTests()
		{
			lock (mvarLock)
			{
				mvarConfigured = null;
				mvarDiscovered = null;
				mvarAttemptUtc = default;
				mvarInFlight = null;
			}
		}

		internal static void SetDiscoveredForTests(Nat64Prefix? prefix)
		{
			lock (mvarLock)
			{
				mvarDiscovered = prefix;
				mvarAttemptUtc = prefix is null ? default : DateTime.UtcNow;
			}
		}
	}
}

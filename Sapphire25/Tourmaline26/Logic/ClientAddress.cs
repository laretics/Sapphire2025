using System.Net;

namespace Tourmaline26.Logic
{
	/// <summary>
	/// Comparación de IP de cliente (HMI/TFT) frente a Devices[].Address.
	/// Kestrel en dual-stack entrega a menudo IPv4 mapeada en IPv6 (::ffff:x.x.x.x).
	/// </summary>
	public static class ClientAddress
	{
		public static IPAddress? Normalize(IPAddress? ip)
		{
			if (ip is null)
				return null;
			if (IPAddress.IPv6Loopback.Equals(ip))
				return IPAddress.Loopback;
			if (ip.IsIPv4MappedToIPv6)
				return ip.MapToIPv4();
			return ip;
		}

		public static string ToDisplay(IPAddress? ip)
		{
			IPAddress? n = Normalize(ip);
			return n is null ? string.Empty : n.ToString();
		}

		public static bool TryParse(string? text, out IPAddress? ip)
		{
			ip = null;
			if (string.IsNullOrWhiteSpace(text))
				return false;
			if (!IPAddress.TryParse(text.Trim(), out IPAddress? parsed) || parsed is null)
				return false;
			ip = Normalize(parsed);
			return ip is not null;
		}

		public static bool Equals(IPAddress? a, IPAddress? b)
		{
			IPAddress? na = Normalize(a);
			IPAddress? nb = Normalize(b);
			if (na is null || nb is null)
				return false;
			return na.Equals(nb);
		}

		public static bool Equals(IPAddress? a, string? b)
		{
			if (!TryParse(b, out IPAddress? nb))
				return false;
			return Equals(a, nb);
		}
	}
}

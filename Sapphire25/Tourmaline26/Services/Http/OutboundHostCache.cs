using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tourmaline26.Services.Http
{
	/// <summary>
	/// IPs vistas con éxito para hosts de salida (SFM/TIB/EMT).
	/// Sobrevive a DNS caído en el tren: se carga de disco y no se borra si la resolución falla.
	/// </summary>
	internal sealed class OutboundHostCache
	{
		public const string RelativePath = "cache/outbound-hosts.json";
		public static readonly TimeSpan DnsRefreshInterval = TimeSpan.FromMinutes(5);

		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			WriteIndented = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
		};

		/// <summary>
		/// Semilla si el fichero no existe todavía (instalación nueva en el tren).
		/// DNS las sustituye en cuanto hay una resolución real.
		/// </summary>
		private static readonly Dictionary<string, string[]> Bootstrap = new(StringComparer.OrdinalIgnoreCase)
		{
			["info.trensfm.com"] = ["213.99.47.36"],
			["www.tib.org"] = ["85.62.90.188"],
			["tib.org"] = ["85.62.90.188"],
			["www.emtpalma.cat"] = ["15.237.152.252"],
			["emtpalma.cat"] = ["15.237.152.252"]
		};

		private readonly string mvarPath;
		private readonly ILogger? mvarLogger;
		private readonly object mvarLock = new();
		private readonly Dictionary<string, HostRecord> mcolHosts = new(StringComparer.OrdinalIgnoreCase);
		private readonly ConcurrentDictionary<string, byte> mcolDnsInFlight = new(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, DateTime> mcolLastDnsAttemptUtc = new(StringComparer.OrdinalIgnoreCase);

		private OutboundHostCache(string path, ILogger? logger)
		{
			mvarPath = path;
			mvarLogger = logger;
		}

		public string Path => mvarPath;

		public static OutboundHostCache Load(string path, ILogger? logger)
		{
			var cache = new OutboundHostCache(path, logger);
			cache.LoadFromDisk();
			return cache;
		}

		public IReadOnlyList<IPAddress> Candidates(string host)
		{
			if (string.IsNullOrWhiteSpace(host))
				return Array.Empty<IPAddress>();

			lock (mvarLock)
			{
				if (!mcolHosts.TryGetValue(host, out HostRecord? record))
					return Array.Empty<IPAddress>();
				return Order(record);
			}
		}

		public bool TryBeginDnsRefresh(string host)
		{
			if (string.IsNullOrWhiteSpace(host) || IPAddress.TryParse(host, out _))
				return false;

			DateTime now = DateTime.UtcNow;
			lock (mvarLock)
			{
				if (mcolLastDnsAttemptUtc.TryGetValue(host, out DateTime last)
					&& now - last < DnsRefreshInterval)
				{
					return false;
				}

				if (!mcolDnsInFlight.TryAdd(host, 0))
					return false;

				mcolLastDnsAttemptUtc[host] = now;
				return true;
			}
		}

		public void EndDnsRefresh(string host)
		{
			mcolDnsInFlight.TryRemove(host, out _);
		}

		public void UpdateFromDns(string host, IReadOnlyList<IPAddress> addresses)
		{
			if (string.IsNullOrWhiteSpace(host) || addresses.Count == 0)
				return;

			IPAddress[] usable = UniqueUsable(addresses);
			if (usable.Length == 0)
				return;

			bool changed;
			lock (mvarLock)
			{
				if (!mcolHosts.TryGetValue(host, out HostRecord? record))
				{
					record = new HostRecord();
					mcolHosts[host] = record;
				}

				var next = new List<IPAddress>(usable);
				changed = !SameSet(record.Addresses, next);
				record.Addresses = next;
				if (record.LastSuccess is null || !Contains(next, record.LastSuccess))
					record.LastSuccess = PreferIPv4(next);
				record.UpdatedUtc = DateTime.UtcNow;
			}

			if (changed)
			{
				Persist();
				mvarLogger?.LogInformation(
					"OutboundHttp: DNS {Host} → {Ips} (caché persistida)",
					host,
					string.Join(", ", usable.Select(a => a.ToString())));
			}
		}

		public void RememberSuccess(string host, IPAddress address)
		{
			if (string.IsNullOrWhiteSpace(host) || !IsUsable(address))
				return;

			bool persist;
			lock (mvarLock)
			{
				if (!mcolHosts.TryGetValue(host, out HostRecord? record))
				{
					record = new HostRecord();
					mcolHosts[host] = record;
				}

				bool changed = record.LastSuccess is null || !record.LastSuccess.Equals(address);
				if (!Contains(record.Addresses, address))
				{
					record.Addresses.Add(address);
					changed = true;
				}

				record.LastSuccess = address;
				bool firstConfirmed = record.UpdatedUtc == DateTime.MinValue;
				persist = changed || firstConfirmed;
				if (persist)
					record.UpdatedUtc = DateTime.UtcNow;
			}

			if (persist)
				Persist();
		}

		private void LoadFromDisk()
		{
			bool persistBootstrap = false;
			lock (mvarLock)
			{
				ApplyBootstrap();
				if (File.Exists(mvarPath))
				{
					try
					{
						string json = File.ReadAllText(mvarPath);
						CacheFileDto? dto = JsonSerializer.Deserialize<CacheFileDto>(json, JsonOptions);
						if (dto?.Hosts is { Count: > 0 })
						{
							foreach (KeyValuePair<string, HostEntryDto> pair in dto.Hosts)
								MergeFromDto(pair.Key, pair.Value);
						}
					}
					catch (Exception ex)
					{
						mvarLogger?.LogWarning(ex, "OutboundHttp: no se pudo leer {Path}; se usa semilla.", mvarPath);
						persistBootstrap = true;
					}
				}
				else
				{
					persistBootstrap = true;
				}

				if (!persistBootstrap
					&& mcolHosts.Values.Any(r => r.UpdatedUtc == DateTime.MinValue))
				{
					persistBootstrap = true;
				}
			}

			if (persistBootstrap)
				Persist();

			int hosts;
			lock (mvarLock)
				hosts = mcolHosts.Count;
			mvarLogger?.LogInformation("OutboundHttp: caché de {Count} host(s) en {Path}", hosts, mvarPath);
		}

		private void ApplyBootstrap()
		{
			foreach (KeyValuePair<string, string[]> pair in Bootstrap)
			{
				if (mcolHosts.ContainsKey(pair.Key))
					continue;
				var addresses = new List<IPAddress>();
				foreach (string text in pair.Value)
				{
					if (IPAddress.TryParse(text, out IPAddress? ip) && IsUsable(ip))
						addresses.Add(ip);
				}
				if (addresses.Count == 0)
					continue;
				mcolHosts[pair.Key] = new HostRecord
				{
					Addresses = addresses,
					LastSuccess = PreferIPv4(addresses),
					UpdatedUtc = DateTime.MinValue
				};
			}
		}

		private void MergeFromDto(string host, HostEntryDto dto)
		{
			if (string.IsNullOrWhiteSpace(host) || dto.Addresses is null)
				return;

			var addresses = new List<IPAddress>();
			foreach (string text in dto.Addresses)
			{
				if (IPAddress.TryParse(text, out IPAddress? ip) && IsUsable(ip))
					addresses.Add(ip);
			}
			if (addresses.Count == 0)
				return;

			IPAddress? last = null;
			if (!string.IsNullOrWhiteSpace(dto.LastSuccess)
				&& IPAddress.TryParse(dto.LastSuccess, out IPAddress? parsed)
				&& IsUsable(parsed))
			{
				last = parsed;
				if (!Contains(addresses, last))
					addresses.Insert(0, last);
			}

			mcolHosts[host] = new HostRecord
			{
				Addresses = addresses,
				LastSuccess = last ?? PreferIPv4(addresses),
				UpdatedUtc = dto.UpdatedUtc
			};
		}

		private void Persist()
		{
			CacheFileDto dto;
			lock (mvarLock)
			{
				dto = new CacheFileDto();
				foreach (KeyValuePair<string, HostRecord> pair in mcolHosts)
				{
					dto.Hosts[pair.Key] = new HostEntryDto
					{
						Addresses = pair.Value.Addresses.Select(a => a.ToString()).ToList(),
						LastSuccess = pair.Value.LastSuccess?.ToString(),
						UpdatedUtc = pair.Value.UpdatedUtc == DateTime.MinValue
							? DateTime.UtcNow
							: pair.Value.UpdatedUtc
					};
				}
			}

			try
			{
				string? dir = System.IO.Path.GetDirectoryName(mvarPath);
				if (!string.IsNullOrEmpty(dir))
					Directory.CreateDirectory(dir);

				string tmp = mvarPath + ".tmp";
				string json = JsonSerializer.Serialize(dto, JsonOptions);
				File.WriteAllText(tmp, json);
				File.Move(tmp, mvarPath, overwrite: true);
			}
			catch (Exception ex)
			{
				mvarLogger?.LogWarning(ex, "OutboundHttp: no se pudo persistir {Path}", mvarPath);
			}
		}

		private static List<IPAddress> Order(HostRecord record)
		{
			var list = new List<IPAddress>(record.Addresses.Count + 1);
			if (record.LastSuccess is not null && IsUsable(record.LastSuccess))
				list.Add(record.LastSuccess);
			foreach (IPAddress ip in record.Addresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork))
			{
				if (!Contains(list, ip))
					list.Add(ip);
			}
			foreach (IPAddress ip in record.Addresses.Where(a => a.AddressFamily == AddressFamily.InterNetworkV6))
			{
				if (!Contains(list, ip))
					list.Add(ip);
			}
			return list;
		}

		private static IPAddress[] UniqueUsable(IReadOnlyList<IPAddress> addresses)
		{
			var list = new List<IPAddress>();
			foreach (IPAddress ip in addresses)
			{
				if (IsUsable(ip) && !Contains(list, ip))
					list.Add(ip);
			}
			return list.ToArray();
		}

		private static bool SameSet(List<IPAddress> left, List<IPAddress> right)
		{
			if (left.Count != right.Count)
				return false;
			foreach (IPAddress ip in left)
			{
				if (!Contains(right, ip))
					return false;
			}
			return true;
		}

		private static bool Contains(IReadOnlyList<IPAddress> list, IPAddress address)
		{
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i].Equals(address))
					return true;
			}
			return false;
		}

		private static IPAddress? PreferIPv4(IReadOnlyList<IPAddress> addresses)
		{
			foreach (IPAddress ip in addresses)
			{
				if (ip.AddressFamily == AddressFamily.InterNetwork)
					return ip;
			}
			return addresses.Count > 0 ? addresses[0] : null;
		}

		private static bool IsUsable(IPAddress address)
		{
			if (address.AddressFamily is not AddressFamily.InterNetwork and not AddressFamily.InterNetworkV6)
				return false;
			if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6Multicast)
				return false;
			if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.None))
				return false;
			return true;
		}

		private sealed class HostRecord
		{
			public List<IPAddress> Addresses { get; set; } = new();
			public IPAddress? LastSuccess { get; set; }
			public DateTime UpdatedUtc { get; set; }
		}

		private sealed class CacheFileDto
		{
			public Dictionary<string, HostEntryDto> Hosts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
		}

		private sealed class HostEntryDto
		{
			public List<string> Addresses { get; set; } = new();
			public string? LastSuccess { get; set; }
			public DateTime UpdatedUtc { get; set; }
		}
	}
}

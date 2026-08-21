using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;

namespace Tourmaline26.Services.Correspondence
{
	/// <summary>
	/// Próximas salidas TIB por código de parada (poll REST).
	/// </summary>
	public sealed class TibDeparturesService : BackgroundService
	{
		public const string HttpClientName = "TibManager";

		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true
		};

		private readonly IHttpClientFactory mvarHttpClientFactory;
		private readonly ILogger<TibDeparturesService> mvarLogger;
		private readonly string mvarEntity;
		private readonly int mvarGroupId;
		private readonly TimeSpan mvarPollInterval;
		private readonly object mvarLock = new();
		private readonly Dictionary<string, IReadOnlyList<TibDeparture>> mcolByStop =
			new(StringComparer.OrdinalIgnoreCase);

		private string[] mcolRequestedStops = Array.Empty<string>();
		private int mvarStopsVersion;
		private string mvarLastError = string.Empty;
		private DateTime mvarUpdatedUtc = DateTime.MinValue;
		private CancellationTokenSource? mvarWake;

		public event EventHandler? Updated;

		public bool HasSnapshot
		{
			get { lock (mvarLock) return mcolByStop.Count > 0; }
		}

		public string LastError
		{
			get { lock (mvarLock) return mvarLastError; }
		}

		public DateTime UpdatedUtc
		{
			get { lock (mvarLock) return mvarUpdatedUtc; }
		}

		public TibDeparturesService(
			IHttpClientFactory httpClientFactory,
			ILogger<TibDeparturesService> logger,
			IConfiguration config)
		{
			mvarHttpClientFactory = httpClientFactory;
			mvarLogger = logger;
			mvarEntity = config["SystemConfiguration:TibEntity"] ?? "ctmr4";
			if (!int.TryParse(config["SystemConfiguration:TibGroupId"], NumberStyles.Integer, CultureInfo.InvariantCulture, out mvarGroupId))
				mvarGroupId = 20124;

			int seconds = 30;
			if (int.TryParse(config["SystemConfiguration:TibPollSeconds"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
				&& parsed >= 10)
			{
				seconds = parsed;
			}
			mvarPollInterval = TimeSpan.FromSeconds(seconds);

			mvarLogger.LogInformation(
				"TibDeparturesService entity={Entity} group={Group} poll={Poll}s",
				mvarEntity,
				mvarGroupId,
				seconds);
		}

		/// <summary>Paradas TIB a vigilar. Vacío = no se pide nada.</summary>
		public void SetStops(IEnumerable<string>? stopCodes)
		{
			string[] next = (stopCodes ?? Array.Empty<string>())
				.Where(s => !string.IsNullOrWhiteSpace(s))
				.Select(s => s.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
				.ToArray();

			bool changed;
			lock (mvarLock)
			{
				changed = mcolRequestedStops.Length != next.Length
					|| !mcolRequestedStops.SequenceEqual(next, StringComparer.OrdinalIgnoreCase);
				if (changed)
				{
					mcolRequestedStops = next;
					mvarStopsVersion++;
					foreach (string key in mcolByStop.Keys.ToList())
					{
						if (!next.Contains(key, StringComparer.OrdinalIgnoreCase))
							mcolByStop.Remove(key);
					}
				}
			}

			if (!changed)
				return;

			mvarLogger.LogInformation("TibDepartures: paradas {Stops}", next.Length == 0 ? "(ninguna)" : string.Join(",", next));
			try
			{
				mvarWake?.Cancel();
			}
			catch (ObjectDisposedException)
			{
				// El bucle recicla el CTS; una cancelación tardía no es error.
			}
		}

		public IReadOnlyList<TibDeparture> GetDepartures(string stopCode)
		{
			if (string.IsNullOrWhiteSpace(stopCode))
				return Array.Empty<TibDeparture>();
			lock (mvarLock)
			{
				return mcolByStop.TryGetValue(stopCode.Trim(), out IReadOnlyList<TibDeparture>? list)
					? list
					: Array.Empty<TibDeparture>();
			}
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			mvarLogger.LogInformation("TibDeparturesService bucle iniciado.");
			while (!stoppingToken.IsCancellationRequested)
			{
				int version;
				lock (mvarLock)
					version = mvarStopsVersion;

				try
				{
					await PollAsync(stoppingToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
				{
					break;
				}
				catch (Exception ex)
				{
					lock (mvarLock)
						mvarLastError = ex.Message;
					mvarLogger.LogWarning(ex, "TibDepartures: error de poll.");
					RaiseUpdated();
				}

				using var wake = new CancellationTokenSource();
				lock (mvarLock)
					mvarWake = wake;
				try
				{
					using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, wake.Token);
					while (!linked.Token.IsCancellationRequested)
					{
						lock (mvarLock)
						{
							if (mvarStopsVersion != version)
								break;
						}

						try
						{
							await Task.Delay(mvarPollInterval, linked.Token).ConfigureAwait(false);
							break;
						}
						catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
						{
							break;
						}
					}
				}
				finally
				{
					lock (mvarLock)
					{
						if (ReferenceEquals(mvarWake, wake))
							mvarWake = null;
					}
				}
			}

			mvarLogger.LogInformation("TibDeparturesService detenido.");
		}

		private async Task PollAsync(CancellationToken cancellationToken)
		{
			string[] stops;
			lock (mvarLock)
				stops = mcolRequestedStops;

			if (stops.Length == 0)
				return;

			HttpClient http = mvarHttpClientFactory.CreateClient(HttpClientName);
			var errors = new List<string>();

			foreach (string stop in stops)
			{
				string url = $"o/manager/stop-code/{Uri.EscapeDataString(stop)}/departures/{Uri.EscapeDataString(mvarEntity)}?res=20&groupId={mvarGroupId}";
				try
				{
					List<TibDeparture> mapped = await FetchStopAsync(http, stop, url, cancellationToken)
						.ConfigureAwait(false);

					lock (mvarLock)
					{
						mcolByStop[stop] = mapped;
						mvarUpdatedUtc = DateTime.UtcNow;
					}

					mvarLogger.LogInformation("TibDepartures: {Stop} → {Count} salidas", stop, mapped.Count);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					throw;
				}
				catch (Exception ex)
				{
					errors.Add($"{stop}: {ex.Message}");
					mvarLogger.LogWarning(ex, "TibDepartures: fallo parada {Stop}", stop);
				}
			}

			lock (mvarLock)
				mvarLastError = errors.Count == 0 ? string.Empty : string.Join("; ", errors);

			RaiseUpdated();
		}

		private async Task<List<TibDeparture>> FetchStopAsync(
			HttpClient http,
			string stop,
			string url,
			CancellationToken cancellationToken)
		{
			const int maxAttempts = 3;
			Exception? last = null;

			for (int attempt = 1; attempt <= maxAttempts; attempt++)
			{
				try
				{
					using var req = new HttpRequestMessage(HttpMethod.Get, url);
					req.Version = HttpVersion.Version11;
					req.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;

					using HttpResponseMessage resp = await http
						.SendAsync(req, cancellationToken)
						.ConfigureAwait(false);
					string body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

					if (resp.StatusCode == HttpStatusCode.Forbidden
						|| resp.StatusCode == HttpStatusCode.Unauthorized
						|| (int)resp.StatusCode == 429
						|| (int)resp.StatusCode >= 500)
					{
						mvarLogger.LogWarning(
							"TibDepartures: {Stop} HTTP {Status} intento {Attempt}/{Max}: {Body}",
							stop,
							(int)resp.StatusCode,
							attempt,
							maxAttempts,
							Truncate(body));
						last = new HttpRequestException($"HTTP {(int)resp.StatusCode} en parada {stop}");
						if (attempt < maxAttempts)
						{
							await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken).ConfigureAwait(false);
							continue;
						}

						throw last;
					}

					if (!resp.IsSuccessStatusCode)
					{
						throw new HttpRequestException($"HTTP {(int)resp.StatusCode} en parada {stop}: {Truncate(body)}");
					}

					List<TibDepartureDto>? dto;
					try
					{
						dto = JsonSerializer.Deserialize<List<TibDepartureDto>>(body, JsonOptions);
					}
					catch (JsonException ex)
					{
						throw new InvalidOperationException(
							$"TIB no devolvió JSON en {stop}: {Truncate(body)}",
							ex);
					}

					var mapped = new List<TibDeparture>();
					int unparsed = 0;
					if (dto is not null)
					{
						foreach (TibDepartureDto item in dto)
						{
							TibDeparture row = Map(stop, item);
							if (row.DepartureTimeLocal == DateTime.MinValue)
								unparsed++;
							mapped.Add(row);
						}
					}

					if (unparsed > 0)
					{
						mvarLogger.LogWarning(
							"TibDepartures: {Stop} {Unparsed}/{Total} salidas sin hora parseable.",
							stop,
							unparsed,
							mapped.Count);
					}

					return mapped;
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					throw;
				}
				catch (Exception ex) when (attempt < maxAttempts && ex is not InvalidOperationException)
				{
					last = ex;
					mvarLogger.LogWarning(
						ex,
						"TibDepartures: {Stop} intento {Attempt}/{Max} falló, se reintenta.",
						stop,
						attempt,
						maxAttempts);
					await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken).ConfigureAwait(false);
				}
			}

			throw last ?? new InvalidOperationException($"TIB: sin respuesta para {stop}.");
		}

		private static string Truncate(string? value, int max = 180)
		{
			if (string.IsNullOrEmpty(value))
				return string.Empty;
			string flat = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
			return flat.Length <= max ? flat : flat[..max] + "…";
		}

		private static TibDeparture Map(string stopCode, TibDepartureDto dto)
		{
			string dest = FirstNonEmpty(dto.Ni, dto.Etn, dto.Snam);
			return new TibDeparture
			{
				StopCode = stopCode,
				TripId = dto.TripId,
				LineCode = (dto.Lcod ?? string.Empty).Trim(),
				DestinationName = dest,
				RouteName = (dto.Snam ?? dto.DepartureRoute ?? string.Empty).Trim(),
				DepartureTimeLocal = ParseLocal(dto.Aet),
				LineColorHex = NormalizeHex(dto.LineColor),
				HasRealtime = dto.RealTrip.HasValue && dto.RealTrip.Value.ValueKind is JsonValueKind.Object
			};
		}

		private static string FirstNonEmpty(params string?[] values)
		{
			foreach (string? v in values)
			{
				if (!string.IsNullOrWhiteSpace(v))
					return v.Trim();
			}
			return string.Empty;
		}

		private static DateTime ParseLocal(string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return DateTime.MinValue;

			string text = value.Trim();
			var styles = DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal;
			if (DateTime.TryParse(text, CultureInfo.InvariantCulture, styles, out DateTime invariant))
				return EnsureLocal(invariant);
			if (DateTime.TryParse(text, CultureInfo.GetCultureInfo("es-ES"), styles, out DateTime spanish))
				return EnsureLocal(spanish);
			if (DateTime.TryParse(text, CultureInfo.CurrentCulture, styles, out DateTime current))
				return EnsureLocal(current);
			return DateTime.MinValue;
		}

		private static DateTime EnsureLocal(DateTime value)
		{
			return value.Kind switch
			{
				DateTimeKind.Utc => value.ToLocalTime(),
				DateTimeKind.Local => value,
				_ => DateTime.SpecifyKind(value, DateTimeKind.Local)
			};
		}

		private static string NormalizeHex(string? color)
		{
			if (string.IsNullOrWhiteSpace(color))
				return "#CE0000";
			string c = color.Trim();
			if (!c.StartsWith('#'))
				c = "#" + c;
			return c;
		}

		private void RaiseUpdated()
		{
			try
			{
				Updated?.Invoke(this, EventArgs.Empty);
			}
			catch (Exception ex)
			{
				mvarLogger.LogDebug(ex, "TibDepartures: error en suscriptor Updated.");
			}
		}

		internal sealed class TibDepartureDto
		{
			[JsonPropertyName("trip_id")]
			public long TripId { get; set; }

			[JsonPropertyName("lcod")]
			public string? Lcod { get; set; }

			[JsonPropertyName("etn")]
			public string? Etn { get; set; }

			[JsonPropertyName("ni")]
			public string? Ni { get; set; }

			[JsonPropertyName("snam")]
			public string? Snam { get; set; }

			[JsonPropertyName("departureRoute")]
			public string? DepartureRoute { get; set; }

			[JsonPropertyName("aet")]
			public string? Aet { get; set; }

			[JsonPropertyName("lineColor")]
			public string? LineColor { get; set; }

			[JsonPropertyName("realTrip")]
			public JsonElement? RealTrip { get; set; }
		}
	}

	public sealed class TibDeparture
	{
		public string StopCode { get; init; } = string.Empty;
		public long TripId { get; init; }
		public string LineCode { get; init; } = string.Empty;
		public string DestinationName { get; init; } = string.Empty;
		public string RouteName { get; init; } = string.Empty;
		public DateTime DepartureTimeLocal { get; init; }
		public string LineColorHex { get; init; } = "#CE0000";
		public bool HasRealtime { get; init; }
	}
}

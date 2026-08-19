using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;

namespace Tourmaline26.Services.Correspondence
{
	/// <summary>
	/// Próximas llegadas EMT Palma por código de parada (MaaS /timestr).
	/// La hora de panel es ahora + seconds.
	/// </summary>
	public sealed class EmtDeparturesService : BackgroundService
	{
		public const string HttpClientName = "EmtMaas";

		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true
		};

		private readonly IHttpClientFactory mvarHttpClientFactory;
		private readonly ILogger<EmtDeparturesService> mvarLogger;
		private readonly TimeSpan mvarPollInterval;
		private readonly object mvarLock = new();
		private readonly Dictionary<string, IReadOnlyList<EmtDeparture>> mcolByStop =
			new(StringComparer.OrdinalIgnoreCase);

		private string[] mcolRequestedStops = Array.Empty<string>();
		private int mvarStopsVersion;
		private string mvarLastError = string.Empty;
		private DateTime mvarUpdatedUtc = DateTime.MinValue;
		private CancellationTokenSource? mvarWake;
		private string mvarBearer = string.Empty;

		public event EventHandler? Updated;

		public string LastError
		{
			get { lock (mvarLock) return mvarLastError; }
		}

		public DateTime UpdatedUtc
		{
			get { lock (mvarLock) return mvarUpdatedUtc; }
		}

		public EmtDeparturesService(
			IHttpClientFactory httpClientFactory,
			ILogger<EmtDeparturesService> logger,
			IConfiguration config)
		{
			mvarHttpClientFactory = httpClientFactory;
			mvarLogger = logger;

			int seconds = 20;
			if (int.TryParse(config["SystemConfiguration:EmtPollSeconds"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
				&& parsed >= 10)
			{
				seconds = parsed;
			}
			mvarPollInterval = TimeSpan.FromSeconds(seconds);

			mvarLogger.LogInformation("EmtDeparturesService poll={Poll}s", seconds);
		}

		/// <summary>Paradas EMT a vigilar. Vacío = no se pide nada.</summary>
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

			mvarLogger.LogInformation("EmtDepartures: paradas {Stops}", next.Length == 0 ? "(ninguna)" : string.Join(",", next));
			try
			{
				mvarWake?.Cancel();
			}
			catch (ObjectDisposedException)
			{
				// El bucle recicla el CTS; una cancelación tardía no es error.
			}
		}

		public IReadOnlyList<EmtDeparture> GetDepartures(string stopCode)
		{
			if (string.IsNullOrWhiteSpace(stopCode))
				return Array.Empty<EmtDeparture>();
			lock (mvarLock)
			{
				return mcolByStop.TryGetValue(stopCode.Trim(), out IReadOnlyList<EmtDeparture>? list)
					? list
					: Array.Empty<EmtDeparture>();
			}
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			mvarLogger.LogInformation("EmtDeparturesService bucle iniciado.");
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
					mvarLogger.LogWarning(ex, "EmtDepartures: error de poll.");
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

			mvarLogger.LogInformation("EmtDeparturesService detenido.");
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
				try
				{
					List<EmtDeparture> mapped = await FetchStopAsync(http, stop, cancellationToken).ConfigureAwait(false);
					lock (mvarLock)
					{
						mcolByStop[stop] = mapped;
						mvarUpdatedUtc = DateTime.UtcNow;
					}

					mvarLogger.LogInformation("EmtDepartures: {Stop} → {Count} salidas", stop, mapped.Count);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					throw;
				}
				catch (Exception ex)
				{
					errors.Add($"{stop}: {ex.Message}");
					mvarLogger.LogWarning(ex, "EmtDepartures: fallo parada {Stop}", stop);
				}
			}

			lock (mvarLock)
				mvarLastError = errors.Count == 0 ? string.Empty : string.Join("; ", errors);

			RaiseUpdated();
		}

		private async Task<List<EmtDeparture>> FetchStopAsync(
			HttpClient http,
			string stop,
			CancellationToken cancellationToken)
		{
			HttpResponseMessage resp = await SendTimestrAsync(http, stop, forceNewToken: false, cancellationToken).ConfigureAwait(false);
			if (resp.StatusCode == HttpStatusCode.Unauthorized)
			{
				resp.Dispose();
				lock (mvarLock)
					mvarBearer = string.Empty;
				resp = await SendTimestrAsync(http, stop, forceNewToken: true, cancellationToken).ConfigureAwait(false);
			}

			using (resp)
			{
				if (resp.StatusCode == HttpStatusCode.NotFound)
				{
					mvarLogger.LogInformation("EmtDepartures: {Stop} sin timestr (404).", stop);
					return new List<EmtDeparture>();
				}

				resp.EnsureSuccessStatusCode();
				await using Stream stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
				using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
				return MapTimestr(stop, doc.RootElement, DateTime.Now);
			}
		}

		private async Task<HttpResponseMessage> SendTimestrAsync(
			HttpClient http,
			string stop,
			bool forceNewToken,
			CancellationToken cancellationToken)
		{
			string token = await EnsureTokenAsync(http, forceNewToken, cancellationToken).ConfigureAwait(false);
			string path = $"agency/stops/{Uri.EscapeDataString(stop)}/timestr";
			using var req = new HttpRequestMessage(HttpMethod.Get, path);
			req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
			return await http.SendAsync(req, cancellationToken).ConfigureAwait(false);
		}

		private async Task<string> EnsureTokenAsync(
			HttpClient http,
			bool forceNew,
			CancellationToken cancellationToken)
		{
			lock (mvarLock)
			{
				if (!forceNew && mvarBearer.Length > 0)
					return mvarBearer;
			}

			using var req = new HttpRequestMessage(HttpMethod.Post, "user/create-user-anonimous");
			req.Content = new StringContent(
				"{\"userTypeId\":1,\"languageId\":1,\"deviceTypeId\":3,\"tokenDevice\":\"\"}",
				Encoding.UTF8,
				"application/json");

			using HttpResponseMessage resp = await http.SendAsync(req, cancellationToken).ConfigureAwait(false);
			resp.EnsureSuccessStatusCode();

			EmtAnonUserDto? dto = await resp.Content.ReadFromJsonAsync<EmtAnonUserDto>(JsonOptions, cancellationToken).ConfigureAwait(false);
			string token = (dto?.BearerToken ?? string.Empty).Trim();
			if (token.Length == 0)
				throw new InvalidOperationException("EMT no devolvió bearerToken.");

			lock (mvarLock)
				mvarBearer = token;

			mvarLogger.LogInformation("EmtDepartures: token anónimo renovado.");
			return token;
		}

		internal static List<EmtDeparture> MapTimestr(string stopCode, JsonElement root, DateTime nowLocal)
		{
			var mapped = new List<EmtDeparture>();
			if (root.ValueKind == JsonValueKind.Null || root.ValueKind == JsonValueKind.Undefined)
				return mapped;

			if (root.ValueKind == JsonValueKind.Object
				&& root.TryGetProperty("estado", out JsonElement estado)
				&& estado.ValueKind == JsonValueKind.String
				&& !string.Equals(estado.GetString(), "OK", StringComparison.OrdinalIgnoreCase))
			{
				return mapped;
			}

			if (root.ValueKind == JsonValueKind.Array)
			{
				foreach (JsonElement item in root.EnumerateArray())
					AppendLine(mapped, stopCode, item, nowLocal);
			}
			else if (root.ValueKind == JsonValueKind.Object)
			{
				AppendLine(mapped, stopCode, root, nowLocal);
			}

			return mapped;
		}

		private static void AppendLine(
			List<EmtDeparture> mapped,
			string stopCode,
			JsonElement lineEl,
			DateTime nowLocal)
		{
			if (lineEl.ValueKind != JsonValueKind.Object)
				return;

			string line = ReadString(lineEl, "lineCode");
			if (line.Length == 0)
				return;

			if (!lineEl.TryGetProperty("vehicles", out JsonElement vehicles)
				|| vehicles.ValueKind != JsonValueKind.Array)
			{
				return;
			}

			string color = LineColor(line);
			foreach (JsonElement veh in vehicles.EnumerateArray())
			{
				if (veh.ValueKind != JsonValueKind.Object)
					continue;

				string dest = ReadString(veh, "destination");
				if (dest.Length == 0)
					continue;

				int seconds = 0;
				if (veh.TryGetProperty("seconds", out JsonElement secEl))
				{
					if (secEl.ValueKind == JsonValueKind.Number && secEl.TryGetInt32(out int n))
						seconds = n;
					else if (secEl.ValueKind == JsonValueKind.String
						&& int.TryParse(secEl.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
					{
						seconds = parsed;
					}
				}

				if (seconds < 0)
					seconds = 0;

				bool atStop = veh.TryGetProperty("atStop", out JsonElement atEl)
					&& atEl.ValueKind == JsonValueKind.True;

				DateTime eta = nowLocal.AddSeconds(seconds);
				if (eta.Kind == DateTimeKind.Unspecified)
					eta = DateTime.SpecifyKind(eta, DateTimeKind.Local);

				mapped.Add(new EmtDeparture
				{
					StopCode = stopCode,
					LineCode = line,
					DestinationName = dest,
					EstimatedTimeLocal = eta,
					LineColorHex = color,
					Seconds = seconds,
					AtStop = atStop
				});
			}
		}

		private static string ReadString(JsonElement el, string name)
		{
			if (!el.TryGetProperty(name, out JsonElement value))
				return string.Empty;
			return value.ValueKind == JsonValueKind.String
				? (value.GetString() ?? string.Empty).Trim()
				: value.ToString().Trim();
		}

		private static string LineColor(string lineCode)
		{
			return lineCode.Trim().ToUpperInvariant() switch
			{
				"A1" => "#00AEEF",
				"A2" => "#7D3C98",
				"1" => "#E30613",
				"2" => "#F39200",
				"3" => "#00A651",
				"4" => "#F7C331",
				"5" => "#8B5E3C",
				"6" => "#EC008C",
				"7" => "#00A0AF",
				"8" => "#8DC63F",
				"N1" => "#1D1D1B",
				"N2" => "#1D1D1B",
				"N3" => "#1D1D1B",
				"N4" => "#1D1D1B",
				_ => "#0074C7"
			};
		}

		private void RaiseUpdated()
		{
			try
			{
				Updated?.Invoke(this, EventArgs.Empty);
			}
			catch (Exception ex)
			{
				mvarLogger.LogDebug(ex, "EmtDepartures: error en suscriptor Updated.");
			}
		}

		internal sealed class EmtAnonUserDto
		{
			[JsonPropertyName("bearerToken")]
			public string? BearerToken { get; set; }
		}
	}

	public sealed class EmtDeparture
	{
		public string StopCode { get; init; } = string.Empty;
		public string LineCode { get; init; } = string.Empty;
		public string DestinationName { get; init; } = string.Empty;
		public DateTime EstimatedTimeLocal { get; init; }
		public string LineColorHex { get; init; } = "#0074C7";
		public int Seconds { get; init; }
		public bool AtStop { get; init; }
	}
}

using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Hosting;

namespace Tourmaline26.Services.SfmInfo
{
    /// <summary>
    /// Servicio de salidas en vivo del panel SFM (info.trensfm.com).
    /// Catálogo REST + suscripción Socket.IO al evento <c>panel</c> de una estación.
    /// </summary>
    public sealed class SfmDeparturesService : BackgroundService
    {
        public const string HttpClientName = "SfmPanel";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IHttpClientFactory mvarHttpClientFactory;
        private readonly ILogger<SfmDeparturesService> mvarLogger;
        private readonly string mvarBaseUrl;
        private readonly int? mvarDefaultStation;
        private readonly object mvarLock = new();
        private readonly ConcurrentDictionary<int, SfmStation> mvarStations = new();
        private readonly ConcurrentDictionary<int, SfmLine> mvarLinesByCode = new();

        private int? mvarRequestedStation;
        private int? mvarActiveStation;
        private SfmPanelSnapshot mvarSnapshot = new();
        private bool mvarCatalogLoaded;
        private string mvarLastError = string.Empty;
        private CancellationTokenSource? mvarSessionWake;
        private int mvarBackoffSeconds = 5;

        /// <summary>Se dispara cuando cambian salidas, reloj o estado del panel.</summary>
        public event EventHandler? Updated;

        public bool IsConnected { get; private set; }
        public bool IsCatalogLoaded => mvarCatalogLoaded;
        public string LastError
        {
            get { lock (mvarLock) return mvarLastError; }
            private set { lock (mvarLock) mvarLastError = value ?? string.Empty; }
        }

        public int? SubscribedStationCode
        {
            get { lock (mvarLock) return mvarRequestedStation; }
        }

        public SfmPanelSnapshot Snapshot
        {
            get { lock (mvarLock) return mvarSnapshot; }
        }

        public IReadOnlyList<SfmDeparture> Departures => Snapshot.Departures;

        public IReadOnlyList<SfmStation> Stations =>
            mvarStations.Values.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

        public IReadOnlyList<SfmLine> Lines =>
            mvarLinesByCode.Values
                .GroupBy(l => l.LineCode)
                .Select(g => g.First())
                .OrderBy(l => l.Symbol)
                .ToList();

        public SfmDeparturesService(
            IHttpClientFactory httpClientFactory,
            ILogger<SfmDeparturesService> logger,
            IConfiguration config)
        {
            mvarHttpClientFactory = httpClientFactory;
            mvarLogger = logger;

            mvarBaseUrl = config["SystemConfiguration:SfmPanelUrl"]
                ?? config["SystemConfiguration:SfmDeparturesUrl"]
                ?? "https://info.trensfm.com";
            mvarBaseUrl = mvarBaseUrl.TrimEnd('/');

            string? defaultStation = config["SystemConfiguration:SfmPanelDefaultStation"];
            if (int.TryParse(defaultStation, NumberStyles.Integer, CultureInfo.InvariantCulture, out int code) && code > 0)
                mvarDefaultStation = code;

            mvarLogger.LogInformation(
                "SfmDeparturesService configurado. Base={Base} DefaultStation={Station}",
                mvarBaseUrl,
                mvarDefaultStation?.ToString() ?? "(ninguna)");
        }

        /// <summary>
        /// Suscribe el panel en vivo a la estación indicada (<c>cod_ubicacion</c>).
        /// </summary>
        public void SetStation(int stationCode)
        {
            if (stationCode <= 0)
                throw new ArgumentOutOfRangeException(nameof(stationCode));

            lock (mvarLock)
            {
                if (mvarRequestedStation == stationCode)
                    return;
                mvarRequestedStation = stationCode;
            }

            mvarLogger.LogInformation("SfmDepartures: estación solicitada {Station}", stationCode);
            try
            {
                mvarSessionWake?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // El long-poll recicla el CTS.
            }
        }

        /// <summary>Deja de pedir salidas (la conexión puede seguir viva para re-suscribir).</summary>
        public void ClearStation()
        {
            lock (mvarLock)
            {
                mvarRequestedStation = null;
                mvarActiveStation = null;
                mvarSnapshot = new SfmPanelSnapshot { UpdatedUtc = DateTime.UtcNow };
            }
            RaiseUpdated();
        }

        public async Task RefreshCatalogAsync(CancellationToken cancellationToken = default)
        {
            HttpClient http = mvarHttpClientFactory.CreateClient(HttpClientName);
            using var req = new HttpRequestMessage(HttpMethod.Get, "sapi/ivi_ubicacion");
            req.Version = HttpVersion.Version11;
            req.Headers.Accept.ParseAdd("application/json");
            using HttpResponseMessage resp = await http.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                string body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new HttpRequestException(
                    $"Catálogo SFM HTTP {(int)resp.StatusCode}: {Truncate(body)}");
            }

            List<SfmUbicacionDto>? ubicaciones = await resp.Content
                .ReadFromJsonAsync<List<SfmUbicacionDto>>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            if (ubicaciones is null)
            {
                mvarLogger.LogWarning("SfmDepartures: catálogo de ubicaciones vacío.");
                return;
            }

            mvarStations.Clear();
            foreach (SfmUbicacionDto u in ubicaciones)
            {
                mvarStations[u.CodUbicacion] = new SfmStation
                {
                    Code = u.CodUbicacion,
                    Name = u.Descripcion?.Trim() ?? string.Empty,
                    Abbreviation = u.Abreviatura?.Trim() ?? string.Empty,
                    Nomenclature = u.Nomenclatura?.Trim() ?? string.Empty,
                    Tracks = u.Vias,
                    Latitude = u.Posicion?.X,
                    Longitude = u.Posicion?.Y
                };
            }

            mvarCatalogLoaded = true;
            mvarLogger.LogInformation("SfmDepartures: {Count} estaciones cargadas.", mvarStations.Count);
            RaiseUpdated();
        }

        public SfmStation? FindStation(int code) =>
            mvarStations.TryGetValue(code, out SfmStation? s) ? s : null;

        public SfmStation? FindStationByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;
            return mvarStations.Values.FirstOrDefault(s =>
                s.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase) ||
                s.Abbreviation.Equals(name, StringComparison.CurrentCultureIgnoreCase));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            mvarLogger.LogInformation("SfmDeparturesService bucle iniciado.");

            if (mvarDefaultStation is int def)
                SetStation(def);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!mvarCatalogLoaded)
                    {
                        try
                        {
                            await RefreshCatalogAsync(stoppingToken).ConfigureAwait(false);
                        }
                        catch (Exception catalogEx) when (catalogEx is not OperationCanceledException)
                        {
                            mvarLogger.LogWarning(
                                catalogEx,
                                "SfmDepartures: catálogo REST no disponible; se sigue con Socket.IO.");
                        }
                    }

                    await RunSessionAsync(stoppingToken).ConfigureAwait(false);
                    mvarBackoffSeconds = 5;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    IsConnected = false;
                    LastError = ex.Message;
                    int delay = mvarBackoffSeconds;
                    mvarBackoffSeconds = Math.Min(30, mvarBackoffSeconds * 2);
                    mvarLogger.LogWarning(ex, "SfmDepartures: sesión interrumpida. Reintento en {Delay} s.", delay);
                    RaiseUpdated();
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(delay), stoppingToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }

            IsConnected = false;
            mvarLogger.LogInformation("SfmDeparturesService detenido.");
        }

        private async Task RunSessionAsync(CancellationToken stoppingToken)
        {
            HttpClient http = mvarHttpClientFactory.CreateClient(HttpClientName);
            var baseUri = new Uri(mvarBaseUrl.EndsWith('/') ? mvarBaseUrl : mvarBaseUrl + "/");

            await using var socket = new SfmSocketIoClient(http, baseUri, mvarLogger);
            await socket.ConnectAsync(stoppingToken).ConfigureAwait(false);
            IsConnected = true;
            LastError = string.Empty;
            mvarLogger.LogInformation("SfmDepartures: Socket.IO conectado a {Base}", mvarBaseUrl);
            RaiseUpdated();

            mvarActiveStation = null;

            while (!stoppingToken.IsCancellationRequested && socket.IsConnected)
            {
                int? requested;
                lock (mvarLock) requested = mvarRequestedStation;

                if (requested is int station && mvarActiveStation != station)
                {
                    await socket.EmitAsync(
                        "tipo",
                        stoppingToken,
                        "panel",
                        new { estacion = station, clase = "LCD" }).ConfigureAwait(false);

                    mvarActiveStation = station;
                    mvarLogger.LogInformation("SfmDepartures: suscrito a estación {Station}", station);
                }

                using var wake = new CancellationTokenSource();
                lock (mvarLock)
                    mvarSessionWake = wake;
                IReadOnlyList<SfmSocketEvent> events;
                try
                {
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, wake.Token);
                    events = await socket.PollAsync(linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    continue;
                }
                finally
                {
                    lock (mvarLock)
                    {
                        if (ReferenceEquals(mvarSessionWake, wake))
                            mvarSessionWake = null;
                    }
                }

                foreach (SfmSocketEvent ev in events)
                    HandleEvent(ev);
            }

            IsConnected = false;
        }

        private void HandleEvent(SfmSocketEvent ev)
        {
            if (ev.Data is null)
                return;

            try
            {
                switch (ev.Name)
                {
                    case "base":
                        ApplyBase(ev.Data.Value);
                        break;
                    case "panel":
                        ApplyPanel(ev.Data.Value);
                        break;
                    // "datos", "incidencia", "base_linea" se ignoran (no son el LCD de estación)
                }
            }
            catch (Exception ex)
            {
                mvarLogger.LogDebug(ex, "SfmDepartures: error procesando evento {Event}", ev.Name);
            }
        }

        private void ApplyBase(JsonElement data)
        {
            SfmBaseEventDto? dto = data.Deserialize<SfmBaseEventDto>(JsonOptions);
            if (dto is null)
                return;

            if (dto.Ubicacion is { Count: > 0 })
            {
                foreach (SfmUbicacionDto u in dto.Ubicacion)
                {
                    mvarStations[u.CodUbicacion] = new SfmStation
                    {
                        Code = u.CodUbicacion,
                        Name = (u.Descripcion ?? string.Empty).Trim(),
                        Abbreviation = u.Abreviatura?.Trim() ?? string.Empty,
                        Nomenclature = u.Nomenclatura?.Trim() ?? string.Empty,
                        Tracks = u.Vias,
                        Latitude = u.Posicion?.X,
                        Longitude = u.Posicion?.Y
                    };
                }
                mvarCatalogLoaded = true;
            }

            if (dto.Linea is { Count: > 0 })
            {
                mvarLinesByCode.Clear();
                foreach (SfmLineaDto line in dto.Linea)
                {
                    string hex = ColorToHex(line.Color);
                    var info = new SfmLine
                    {
                        MarchCode = line.CodMarcha,
                        LineCode = line.CodLinea,
                        Symbol = line.Simbolo?.Trim() ?? string.Empty,
                        Description = line.Observacion?.Trim() ?? string.Empty,
                        TypeCode = line.CodTipo,
                        ColorArgb = line.Color,
                        ColorHex = hex
                    };
                    // Preferimos la primera marcha de cada cod_linea para lookup por línea del panel.
                    mvarLinesByCode.TryAdd(line.CodLinea, info);
                    // También indexamos por marcha por si hiciera falta.
                    mvarLinesByCode.TryAdd(line.CodMarcha + 10_000, info);
                }
            }
        }

        private void ApplyPanel(JsonElement data)
        {
            SfmPanelEventDto? dto = data.Deserialize<SfmPanelEventDto>(JsonOptions);
            if (dto is null)
                return;

            int stationCode;
            lock (mvarLock)
                stationCode = mvarActiveStation ?? mvarRequestedStation ?? 0;

            string stationName = mvarStations.TryGetValue(stationCode, out SfmStation? st)
                ? st.Name
                : string.Empty;

            var departures = new List<SfmDeparture>();
            if (dto.Info is not null)
            {
                foreach (SfmPanelInfoDto info in dto.Info)
                    departures.Add(MapDeparture(info));
            }

            var snapshot = new SfmPanelSnapshot
            {
                StationCode = stationCode,
                StationName = stationName,
                ServerClockLocal = FromEpochMs(dto.Fecha),
                PanelState = dto.Estado,
                UpdatedUtc = DateTime.UtcNow,
                Departures = departures
            };

            lock (mvarLock)
                mvarSnapshot = snapshot;

            LastError = string.Empty;
            RaiseUpdated();
        }

        private SfmDeparture MapDeparture(SfmPanelInfoDto info)
        {
            string origin = ResolveStationName(info.CodOrigen);
            string destination = ResolveStationName(info.CodDestino);

            string symbol = string.Empty;
            string lineDesc = string.Empty;
            string colorHex = "#888888";
            if (mvarLinesByCode.TryGetValue(info.Linea, out SfmLine? line))
            {
                symbol = line.Symbol;
                lineDesc = line.Description;
                colorHex = line.ColorHex;
            }

            var messages = new List<SfmLocalizedText>();
            if (info.TextoInfo is not null)
            {
                foreach (List<SfmTextoInfoDto> group in info.TextoInfo)
                {
                    foreach (SfmTextoInfoDto t in group)
                    {
                        if (string.IsNullOrWhiteSpace(t.Descripcion))
                            continue;
                        messages.Add(new SfmLocalizedText
                        {
                            LanguageCode = t.Idioma,
                            Text = t.Descripcion.Trim()
                        });
                    }
                }
            }

            IReadOnlyList<int> stopCodes = info.Estaciones ?? (IReadOnlyList<int>)Array.Empty<int>();
            var stopNames = new List<string>(stopCodes.Count);
            foreach (int code in stopCodes)
                stopNames.Add(ResolveStationName(code));

            return new SfmDeparture
            {
                ServicePlanCode = info.CodPlanServicio,
                ServiceName = info.Nombre?.Trim() ?? string.Empty,
                DepartureTimeLocal = FromEpochMs(info.Hora) ?? DateTime.MinValue,
                EstimatedTimeLocal = FromEpochMs(info.Estimado) ?? DateTime.MinValue,
                OriginCode = info.CodOrigen,
                OriginName = origin,
                DestinationCode = info.CodDestino,
                DestinationName = destination,
                LineCode = info.Linea,
                LineSymbol = symbol,
                LineDescription = lineDesc,
                LineColorHex = colorHex,
                Platform = info.Via,
                OriginalPlatform = info.ViaOriginal,
                Status = info.Estado,
                InfoMessages = messages,
                StopCodes = stopCodes,
                StopNames = stopNames
            };
        }

        private string ResolveStationName(int code)
        {
            if (mvarStations.TryGetValue(code, out SfmStation? s) && !string.IsNullOrEmpty(s.Name))
                return s.Name;
            return code.ToString(CultureInfo.InvariantCulture);
        }

        private void RaiseUpdated()
        {
            try
            {
                Updated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                mvarLogger.LogDebug(ex, "SfmDepartures: error en suscriptor Updated.");
            }
        }

        private static DateTime? FromEpochMs(long epochMs)
        {
            if (epochMs <= 0)
                return null;
            return DateTimeOffset.FromUnixTimeMilliseconds(epochMs).LocalDateTime;
        }

        private static string Truncate(string? value, int max = 180)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            string flat = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return flat.Length <= max ? flat : flat[..max] + "…";
        }

        private static string ColorToHex(int packedRgb)
        {
            // El panel usa un entero 0xRRGGBB (sin alpha).
            int r = (packedRgb >> 16) & 0xFF;
            int g = (packedRgb >> 8) & 0xFF;
            int b = packedRgb & 0xFF;
            return $"#{r:X2}{g:X2}{b:X2}";
        }
    }
}

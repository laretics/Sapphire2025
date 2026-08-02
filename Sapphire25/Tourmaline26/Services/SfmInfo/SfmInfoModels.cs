using System.Text.Json.Serialization;

namespace Tourmaline26.Services.SfmInfo
{
    /// <summary>Estación del catálogo SFM (sapi/ivi_ubicacion).</summary>
    public sealed class SfmStation
    {
        public int Code { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Abbreviation { get; init; } = string.Empty;
        public string Nomenclature { get; init; } = string.Empty;
        public int Tracks { get; init; }
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
    }

    /// <summary>Línea/marcha del evento Socket.IO <c>base</c>.</summary>
    public sealed class SfmLine
    {
        public int MarchCode { get; init; }
        public int LineCode { get; init; }
        public string Symbol { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public int TypeCode { get; init; }
        /// <summary>Color RGB empaquetado del panel (entero 0xRRGGBB).</summary>
        public int ColorArgb { get; init; }
        public string ColorHex { get; init; } = "#888888";
    }

    /// <summary>Salida anunciada en el panel LCD de una estación.</summary>
    public sealed class SfmDeparture
    {
        public long ServicePlanCode { get; init; }
        /// <summary>Identificador de servicio (p. ej. "4431").</summary>
        public string ServiceName { get; init; } = string.Empty;
        public DateTime DepartureTimeLocal { get; init; }
        public DateTime EstimatedTimeLocal { get; init; }
        public int OriginCode { get; init; }
        public string OriginName { get; init; } = string.Empty;
        public int DestinationCode { get; init; }
        public string DestinationName { get; init; } = string.Empty;
        public int LineCode { get; init; }
        public string LineSymbol { get; init; } = string.Empty;
        public string LineDescription { get; init; } = string.Empty;
        public string LineColorHex { get; init; } = "#888888";
        public int? Platform { get; init; }
        public int? OriginalPlatform { get; init; }
        public bool PlatformChanged =>
            OriginalPlatform.HasValue && Platform.HasValue && OriginalPlatform.Value != Platform.Value;
        /// <summary>Estado del servicio en el panel (null / 0 / 1… según SFM).</summary>
        public int? Status { get; init; }
        /// <summary>Avisos (ES / CA / EN) aplanados.</summary>
        public IReadOnlyList<SfmLocalizedText> InfoMessages { get; init; } = Array.Empty<SfmLocalizedText>();
        /// <summary>Códigos de parada del trayecto (sin origen; incluye destino).</summary>
        public IReadOnlyList<int> StopCodes { get; init; } = Array.Empty<int>();
        public IReadOnlyList<string> StopNames { get; init; } = Array.Empty<string>();
    }

    public sealed class SfmLocalizedText
    {
        /// <summary>600=ES, 601=CA, 602=EN (convención del panel SFM).</summary>
        public int LanguageCode { get; init; }
        public string Text { get; init; } = string.Empty;
    }

    public sealed class SfmPanelSnapshot
    {
        public int StationCode { get; init; }
        public string StationName { get; init; } = string.Empty;
        public DateTime? ServerClockLocal { get; init; }
        /// <summary>0 verde … 3 rojo (semáforo del panel).</summary>
        public int PanelState { get; init; }
        public DateTime UpdatedUtc { get; init; }
        public IReadOnlyList<SfmDeparture> Departures { get; init; } = Array.Empty<SfmDeparture>();
    }

    // ── DTOs JSON crudos (deserialización) ───────────────────────────────────

    internal sealed class SfmUbicacionDto
    {
        [JsonPropertyName("cod_ubicacion")]
        public int CodUbicacion { get; set; }

        [JsonPropertyName("descripcion")]
        public string? Descripcion { get; set; }

        [JsonPropertyName("abreviatura")]
        public string? Abreviatura { get; set; }

        [JsonPropertyName("nomenclatura")]
        public string? Nomenclatura { get; set; }

        [JsonPropertyName("vias")]
        public int Vias { get; set; }

        [JsonPropertyName("posicion")]
        public SfmPosicionDto? Posicion { get; set; }
    }

    internal sealed class SfmPosicionDto
    {
        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }
    }

    internal sealed class SfmLineaDto
    {
        [JsonPropertyName("cod_marcha")]
        public int CodMarcha { get; set; }

        [JsonPropertyName("cod_linea")]
        public int CodLinea { get; set; }

        [JsonPropertyName("simbolo")]
        public string? Simbolo { get; set; }

        [JsonPropertyName("observacion")]
        public string? Observacion { get; set; }

        [JsonPropertyName("cod_tipo")]
        public int CodTipo { get; set; }

        [JsonPropertyName("color")]
        public int Color { get; set; }
    }

    internal sealed class SfmBaseEventDto
    {
        [JsonPropertyName("linea")]
        public List<SfmLineaDto>? Linea { get; set; }

        [JsonPropertyName("ubicacion")]
        public List<SfmUbicacionDto>? Ubicacion { get; set; }
    }

    internal sealed class SfmPanelEventDto
    {
        [JsonPropertyName("fecha")]
        public long Fecha { get; set; }

        [JsonPropertyName("estado")]
        public int Estado { get; set; }

        [JsonPropertyName("info")]
        public List<SfmPanelInfoDto>? Info { get; set; }
    }

    internal sealed class SfmPanelInfoDto
    {
        [JsonPropertyName("cod_plan_servicio")]
        public long CodPlanServicio { get; set; }

        [JsonPropertyName("nombre")]
        public string? Nombre { get; set; }

        [JsonPropertyName("hora")]
        public long Hora { get; set; }

        [JsonPropertyName("estimado")]
        public long Estimado { get; set; }

        [JsonPropertyName("cod_origen")]
        public int CodOrigen { get; set; }

        [JsonPropertyName("cod_destino")]
        public int CodDestino { get; set; }

        [JsonPropertyName("linea")]
        public int Linea { get; set; }

        [JsonPropertyName("via")]
        public int? Via { get; set; }

        [JsonPropertyName("via_original")]
        public int? ViaOriginal { get; set; }

        [JsonPropertyName("estado")]
        public int? Estado { get; set; }

        [JsonPropertyName("texto_info")]
        public List<List<SfmTextoInfoDto>>? TextoInfo { get; set; }

        [JsonPropertyName("estaciones")]
        public List<int>? Estaciones { get; set; }
    }

    internal sealed class SfmTextoInfoDto
    {
        [JsonPropertyName("idioma")]
        public int Idioma { get; set; }

        [JsonPropertyName("descripcion")]
        public string? Descripcion { get; set; }
    }
}

using System.Text.Json.Serialization;

namespace Tourmaline26.Services.OpenMeteo
{
    public class WeatherValue
    {
        [JsonPropertyName("time")]
        public string Time { get; set; } = string.Empty;

        [JsonPropertyName("temperature_2m")]
        public double Temperature2m { get; set; }

        [JsonPropertyName("relative_humidity_2m")]
        public double RelativeHumidity2m { get; set; }

        [JsonPropertyName("wind_speed_10m")]
        public double WindSpeed10m { get; set; }

        [JsonPropertyName("rain")]
        public double Rain { get; set; }                    // mm en la última hora

        [JsonPropertyName("visibility")]
        public double Visibility { get; set; }              // metros

        [JsonPropertyName("cloud_cover")]
        public double CloudCover { get; set; }              // % 

        [JsonPropertyName("weather_code")]
        public int WeatherCode { get; set; }                // Código WMO
    }
}

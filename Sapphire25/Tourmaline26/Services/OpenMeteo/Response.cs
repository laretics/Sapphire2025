using System.Text.Json.Serialization;

namespace Tourmaline26.Services.OpenMeteo
{
    public class Response
    {
        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("current")]
        public WeatherValue? Current { get; set; }
    }
}

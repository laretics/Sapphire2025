using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Tourmaline26.Services.OpenMeteo
{
    public class OpenMeteoClient
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private string mvarUrl;
        public OpenMeteoClient(string url)
        {
            mvarUrl = url;
        }
        public OpenMeteoClient()
        {
            mvarUrl = "https://api.open-meteo.com/v1/";
        }
        public async Task<Response?> GetCurrentWeatherAsync(double latitude, double longitude)
        {
            string url = $"{mvarUrl}forecast?" +
                         $"latitude={latitude.ToString(CultureInfo.InvariantCulture)}&longitude={longitude.ToString(CultureInfo.InvariantCulture)}" +
                         $"&current=temperature_2m,relative_humidity_2m,wind_speed_10m,rain,visibility,cloud_cover,weather_code" +
                         $"&timezone=auto";

            try
            {
                var response = await _httpClient.GetFromJsonAsync<Response>(url);
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al consultar Open-Meteo: {ex.Message}");
                return null;
            }
        }
        public static string GetWeatherIconUrl(int weatherCode)
        {
            string iconCode = weatherCode switch
            {
                0 => "01",      // clear sky
                1 or 2 => "02", // mainly clear / partly cloudy
                3 => "06",      // overcast
                45 or 48 => "11", // fog
                51 or 53 or 55 or 61 or 63 or 65 => "13", // rain / drizzle
                71 or 73 or 75 => "19", // snow
                80 or 81 or 82 => "12", // showers
                95 or 96 or 99 => "15", // thunderstorm
                _ => "01"
            };

            return $"/img/meteo/HH_WEATHER_{iconCode}.png";
        }
    }
}
using System.Globalization;
using System.Threading.Tasks;
using Tourmaline26.Services.OpenMeteo;
namespace Tourmaline26.Services
{
    public class MeteoService
    {
        private OpenMeteoClient mvarClient;
        private ILogger<MeteoService> mvarLogger;
        private WeatherValue? mvarWeatherValue;
        public WeatherValue? WeatherValue { get => mvarWeatherValue; }
        private TourmalineService mvarTourmaline;
        private double mvarDefaultLatitude = double.NaN;
        private double mvarDefaultLongitude = double.NaN;
        public MeteoService(
            ILogger<MeteoService> logger,
            IConfiguration config,
            TourmalineService tourmalineService)
        {
            mvarLogger = logger;
            //TODO: Leer valores desde config.
            mvarClient = new OpenMeteoClient();
            mvarTourmaline = tourmalineService;
            if(mvarTourmaline.SystemConfig.DefaultLocation.Length>0)
            {
                string[] coordinates = mvarTourmaline.SystemConfig.DefaultLocation.Split(',');
                if(coordinates.Length>1)
                {
                    double.TryParse(coordinates[0],CultureInfo.InvariantCulture, out mvarDefaultLatitude);
                    double.TryParse(coordinates[1],CultureInfo.InvariantCulture, out mvarDefaultLongitude);
                }
            }
        }

        public async Task<bool> ReadLoop()
        {
            try
            {
                Logic.GPSData? auxGps = mvarTourmaline.SessionConfig.CurrentGPSData;
                Response? auxRespuesta = null;
                if (null != auxGps)
                    auxRespuesta = await mvarClient.GetCurrentWeatherAsync(auxGps.Latitude, auxGps.Longitude);
                else if (!double.IsNaN(mvarDefaultLatitude))
                    auxRespuesta = await mvarClient.GetCurrentWeatherAsync(mvarDefaultLatitude, mvarDefaultLongitude);

                if (null != auxRespuesta)
                    mvarTourmaline.SessionConfig.CurrentWeather = auxRespuesta.Current;
            }
            catch(Exception ex)
            {
                mvarLogger.LogError($"Error in MeteoService:{ex.Message}");
                return false;
            }
            return true;
        }
        
    }
}

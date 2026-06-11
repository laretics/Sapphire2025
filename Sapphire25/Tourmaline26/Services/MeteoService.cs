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
        public MeteoService(
            ILogger<MeteoService> logger,
            IConfiguration config,
            TourmalineService tourmalineService)
        {
            mvarLogger = logger;
            //TODO: Leer valores desde config.
            mvarClient = new OpenMeteoClient();
            mvarTourmaline = tourmalineService;
        }

        public async Task<bool> ReadLoop()
        {
            try
            {
                Logic.GPSData? auxGps = mvarTourmaline.SessionConfig.CurrentGPSData;
                if(null!=auxGps)
                {
                    Response? auxRespuesta = await mvarClient.GetCurrentWeatherAsync(auxGps.Latitude, auxGps.Longitude);
                    if(null!=auxRespuesta)
                        mvarTourmaline.SessionConfig.CurrentWeather = auxRespuesta.Current;
                }
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

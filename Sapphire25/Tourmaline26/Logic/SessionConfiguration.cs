using Google.Protobuf.WellKnownTypes;
using Sapphire2025Models.Authentication;
using TimeNet2026.Production;
using TimeNet2026.Topo;
using Tourmaline26.Services.Armandito;
using Tourmaline26.Services.OpenMeteo;
namespace Tourmaline26.Logic
{
    /// <summary>
    /// Estos valores pertenecen a la sesión actual del tren.
    /// Es un contenedor que los engloba a todos.
    /// </summary>
    public class SessionConfiguration
    {
        public bool Initialized { get; set; } = false; //Indica si el sistema ha terminado de cargar los datos.
        /// <summary>
        /// Mensaje del último error de arranque (p. ej. BD local corrupta/desfasada).
        /// Vacío si la inicialización terminó sin incidencias.
        /// </summary>
        public string InitError { get; set; } = string.Empty;

        public MVBData? CurrentMVBData { get; set; }//Último paquete de datos recibido del bus MVB.
        public WeatherValue? CurrentWeather { get; set; } //Estado meteorológico actual
        public string MVBError { get; set; } = ""; //Mensaje de error relacionado con el bus MVB, si lo hubiera.
        public DateTime MVBLastUpdate { get; set; } = DateTime.MinValue; //Última vez que se recibió una actualización del bus MVB.
        public bool GPSEnabled { get; set; } = true; //Módulo de posicionamiento       
        public GPSData? CurrentGPSData{ get; set; } //Última lectura del GPS
        public bool GPSOK { get; set; } = true; //Indica si el GPS está recibiendo señal.
        public DateTime GPSLastUpdate { get; set; } = DateTime.MinValue; //Última vez que se recibió una actualización del GPS.
        public bool InternetEnabled { get; set; } = true; //Indica si el sistema tiene habilitada la conexión a internet.        
        public bool InternetOK { get; set; } = true; //Indica si el sistema tiene conexión a internet.
        public bool SpeakersAnnouncing { get; set; } = false; //Los altavoces de sala están haciendo un anuncio a los viajeros.
        public IReadOnlyList<ArmanditoMessage> EarthMessages { get; set; } = Array.Empty<ArmanditoMessage>(); //Lista de mensajes recibidos de tierra
        public FeatureSwitches MainSwitches { get; } = new FeatureSwitches();
        public ServiceMode ServiceMode { get; } = new ServiceMode();

        #region Telemetria
        /// <summary>
        /// Velocidad actual simulada en DemoMode. TourmalineBackground la acerca
        /// progresivamente a <see cref="CurrentNeutralSpeed"/> y la envía al simulador.
        /// </summary>
        public int SimulatedSpeed { get; set; } = 0;

        public int CurrentSpeed //Velocidad actual, leída de GPS o MVB
        { 
            get
            {
                // En demo la aguja de velocidad es la simulada (rampa hacia el objetivo).
                if (ServiceMode.DemoMode)
                    return SimulatedSpeed;

                //Prioridad MVB                
                if ((ServiceMode.MVBEnabled|| ServiceMode.MVBDummy) && null != CurrentMVBData)
                    return (int)CurrentMVBData.Speed;
                if ((GPSEnabled || ServiceMode.GPSDummy) && null != CurrentGPSData)
                    return (int)CurrentGPSData.SpeedKmh;

                return 0; //Cuando todo lo demás falla, devuelve cero.
            }
        }        
        public int CurrentLimitSpeed { get; set; } = 100; //Velocidad máxima del tren en este tramo
        public int CurrentNeutralSpeed { get; set; } = 0; //Velocidad objetivo (Ónice / consigna de demo)
        public LinearLocation LinearLocation { get; set; } = new LinearLocation(); //Ubicación lineal de este tren (si la puedo sacar)

		#endregion Telemetria

		public Enums.InformationLevel InformationLevel { get; set; } = Enums.InformationLevel.Route; //Nivel de información actual.
        public Enums.PassengerInformationMode InformationMode { get; set; } = Enums.PassengerInformationMode.Default; //Contenido de la info.

        /// <summary>
        /// Si es true, el anuncio emergente (<see cref="PassengerAnnouncement"/>) se representa en los TFT.
        /// </summary>
        public bool PassengerAnnouncementEnabled { get; set; } = false;

        /// <summary>
        /// Mensaje de anuncio seleccionado para difundir a los viajeros (popup).
        /// Null = ningún mensaje elegido aún.
        /// </summary>
        public PassengerInformation? PassengerAnnouncement { get; set; }

        public SessionModel? Session { get; set; } = null; //Información sobre el usuario actual
        public Dictionary<Guid, UserModelBase>? ColUsers{ get; set; } = null; //Colección de usuarios del tren, con su Guid de Zafiro como clave.
        public TimeNetEnvironment? TNEnvironment { get; set; }= null; //Todo lo que necesita el programa para mostrar una circulación
    }
}

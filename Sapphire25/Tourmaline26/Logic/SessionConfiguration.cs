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

        /// <summary>
        /// Velocidad del tren para HMI (velocímetro, demos, paneles).
        /// Fuente: Demo (rampa) o MVB / emulación MVB. La velocidad GPS no se usa aquí
        /// (queda en <see cref="CurrentGPSData"/> para usos posteriores).
        /// </summary>
        public int CurrentSpeed
        {
            get
            {
                // Demo: rampa hacia la consigna (también se refleja en el MVB dummy).
                if (ServiceMode.DemoMode)
                    return SimulatedSpeed;

                // Bus real o emulación MVB.
                if ((ServiceMode.MVBEnabled || ServiceMode.MVBDummy) && null != CurrentMVBData)
                    return CurrentMVBData.Speed;

                return 0;
            }
        }

        /// <summary>
        /// True si el inversor está en marcha adelante o atrás (no neutro / túnel / maniobra).
        /// Condición de visibilidad del velocímetro con datos MVB.
        /// </summary>
        public bool SpeedometerDriveSelected
        {
            get
            {
                if (null == CurrentMVBData)
                    return false;
                return CurrentMVBData.Inverter is MVBData.InverterPosition.Forward
                    or MVBData.InverterPosition.Reverse;
            }
        }

        public int CurrentLimitSpeed { get; set; } = 100; //Velocidad máxima del tren en este tramo
        public int CurrentNeutralSpeed { get; set; } = 0; //Velocidad objetivo (Ónice / consigna de demo)
        public LinearLocation LinearLocation { get; set; } = new LinearLocation(); //Ubicación lineal de este tren (si la puedo sacar)

        /// <summary>
        /// Localización lineal del tren de Tourmaline Experience (mismo algoritmo GPS→PK).
        /// </summary>
        public LinearLocation SimulatedLinearLocation { get; set; } = new LinearLocation();

        /// <summary>Último desfase PK a lo largo de la marcha (m). Positivo = simulado retrasado respecto al real.</summary>
        public long ExperiencePkLagMeters { get; set; }

        /// <summary>Última velocidad objetivo enviada al simulador (km/h), tras corrección por desfase.</summary>
        public int ExperienceCommandedSpeed { get; set; }

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
        private PassengerInformation? mvarPassengerAnnouncement;
        public PassengerInformation? PassengerAnnouncement 
        { 
            get => mvarPassengerAnnouncement; 
            set
            {
                mvarPassengerAnnouncement = value;
                mvarPassengerAnnouncementLanguage = 0;
            }
        }
        private byte mvarPassengerAnnouncementLanguage = 0;
        public byte PassengerAnnouncementLanguage { get => mvarPassengerAnnouncementLanguage;} //Idioma actual en el que se muestran los mensajes.
        public void IncLanguage()
        {
            mvarPassengerAnnouncementLanguage++;
            if (mvarPassengerAnnouncementLanguage > 2)
                mvarPassengerAnnouncementLanguage = 0;
        }

        public SessionModel? Session { get; set; } = null; //Información sobre el usuario actual
        public Dictionary<Guid, UserModelBase>? ColUsers{ get; set; } = null; //Colección de usuarios del tren, con su Guid de Zafiro como clave.
        public TimeNetEnvironment? TNEnvironment { get; set; }= null; //Todo lo que necesita el programa para mostrar una circulación
    }
}

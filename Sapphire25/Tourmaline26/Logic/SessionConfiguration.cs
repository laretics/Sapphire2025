using Diamond.Cabin;
using Diamond.Project;
using Sapphire2025Models.Authentication;
using Tourmaline26.Services.Armandito;
using Tourmaline26.Services.OpenMeteo;

namespace Tourmaline26.Logic
{
	/// <summary>
	/// Valores de la sesión actual del tren.
	/// </summary>
	public class SessionConfiguration
	{
		public bool Initialized { get; set; } = false;

		/// <summary>
		/// Mensaje del último error de arranque. Vacío si ok.
		/// </summary>
		public string InitError { get; set; } = string.Empty;

		public MVBData? CurrentMVBData { get; set; }
		public WeatherValue? CurrentWeather { get; set; }
		public string MVBError { get; set; } = "";
		public DateTime MVBLastUpdate { get; set; } = DateTime.MinValue;
		public bool GPSEnabled { get; set; } = true;
		public GPSData? CurrentGPSData { get; set; }
		public bool GPSOK { get; set; } = true;
		public DateTime GPSLastUpdate { get; set; } = DateTime.MinValue;
		public bool InternetEnabled { get; set; } = true;
		public bool InternetOK { get; set; } = true;
		public bool SpeakersAnnouncing { get; set; } = false;
		public IReadOnlyList<ArmanditoMessage> EarthMessages { get; set; } = Array.Empty<ArmanditoMessage>();
		public FeatureSwitches MainSwitches { get; } = new FeatureSwitches();
		public ServiceMode ServiceMode { get; } = new ServiceMode();

		/// <summary>
		/// Última opción del menú lateral.
		/// </summary>
		public string LastSideMenuOptionId { get; set; } = "pass";

		#region Telemetria

		public int SimulatedSpeed { get; set; } = 0;

		public int CurrentSpeed
		{
			get
			{
				if (ServiceMode.DemoMode || ServiceMode.RouteSimulation)
					return SimulatedSpeed;

				if ((ServiceMode.MVBEnabled || ServiceMode.MVBDummy) && null != CurrentMVBData)
					return CurrentMVBData.Speed;

				return 0;
			}
		}

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

		public int CurrentLimitSpeed { get; set; } = 100;
		public int CurrentNeutralSpeed { get; set; } = 0;

		/// <summary>
		/// Localización lineal del tren real (GPS → PK). Vive en <see cref="Cabin"/>.
		/// </summary>
		public LinearLocation LinearLocation
		{
			get
			{
				if (Cabin is not null)
					return Cabin.LinearLocation;
				return mvarFallbackLocation;
			}
		}

		private readonly LinearLocation mvarFallbackLocation = new LinearLocation();

		/// <summary>
		/// Localización lineal del tren de Tourmaline Experience.
		/// </summary>
		public LinearLocation SimulatedLinearLocation { get; set; } = new LinearLocation();

		public long ExperiencePkLagMeters { get; set; }

		public int ExperienceCommandedSpeed { get; set; }

		#endregion Telemetria

		public Enums.InformationLevel InformationLevel { get; set; } = Enums.InformationLevel.Route;
		public Enums.PassengerInformationMode InformationMode { get; set; } = Enums.PassengerInformationMode.Default;

		/// <summary>
		/// Estación forzada para previsualizar anuncio de llegada (menú de servicio).
		/// Prioridad sobre <see cref="CabinEnvironment.CurrentStation"/>.
		/// </summary>
		public StationInfo? PreviewArrivalStation { get; set; }

		public bool PassengerAnnouncementEnabled { get; set; } = false;

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
		public byte PassengerAnnouncementLanguage { get => mvarPassengerAnnouncementLanguage; }

		public void IncLanguage()
		{
			mvarPassengerAnnouncementLanguage++;
			if (mvarPassengerAnnouncementLanguage > 2)
				mvarPassengerAnnouncementLanguage = 0;
		}

		public SessionModel? Session { get; set; } = null;
		public Dictionary<Guid, UserModelBase>? ColUsers { get; set; } = null;

		/// <summary>Nombre del turno grafiado hoy (solo si el usuario es maquinista con asignación).</summary>
		public string? DriverShiftName { get; set; }

		/// <summary>Números de tren del turno de hoy (tokens del gráfico).</summary>
		public List<string> DriverShiftTrainTokens { get; } = new List<string>();

		public bool DriverShiftLoaded { get; set; }

		public bool HasDriverShiftToday => DriverShiftTrainTokens.Count > 0;

		public void ClearDriverShift()
		{
			DriverShiftName = null;
			DriverShiftTrainTokens.Clear();
			DriverShiftLoaded = false;
		}

		/// <summary>
		/// Entorno Diamond de cabina (topo + plan publicado + misión).
		/// </summary>
		public CabinEnvironment? Cabin { get; set; } = null;
	}
}

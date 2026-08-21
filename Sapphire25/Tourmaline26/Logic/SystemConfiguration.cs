using Sapphire2025Models.Aeneas;
namespace Tourmaline26.Logic
{
	/// <summary>
	/// Este elemento contiene toda la información necesaria sobre la unidad de tren.
	/// </summary>
	public class SystemConfiguration
	{
		public string Version  => "V1.13B"; //Versión de este programa.
		public DateTime Release => new DateTime(2026,8,21);
		public Guid TrainId { get; set; } //Guid de este material móvil según Zafiro.
        public string Name { get; set; } = "Tren";
		public DateTime LastRelease { get; set; } //Fecha de la última instalación de hardware.
		public List<CameraInfo> Cameras { get; set; } = new List<CameraInfo>(); //Cámaras del tren.
		public string ToniCruz { get; set; } = "right";
        public string SapphireUrl { get; set; } = "https://material.trensfm.com:5031"; //Url para hacer peticiones al servidor REST de Zafiro
		public string MVBUrl { get; set; } = "http://172.16.20.11:8000/data";
		public string TExperienceUrl { get; set; } = "http://172.16.20.12:5005/server"; //Servidor API REST de Tourmaline Experiencwe
		public string TExperienceStr { get; set; } = "http://192.168.0.9:5005/stream"; //Streamer de Tourmaline Experience
		public string SfmInfoUrl { get; set; } = "https://info.trensfm.com:8084"; //Url del proveedor Armandito
		public string DiamondTopologyId { get; set; } = "70062022-f986-4e2f-ab38-e5034621fac3"; // Id de la topología de Diamond. Es un Guid, pero aquí lo leemos como string.
		public string SfmInfoToken { get; set; } = "SFM2026"; //Token para Armandito
		/// <summary>Base del panel de información al viajero (salidas por estación, Socket.IO).</summary>
		public string SfmPanelUrl { get; set; } = "https://info.trensfm.com";
		/// <summary>Estación inicial opcional (<c>cod_ubicacion</c>) para el panel de salidas.</summary>
		public int? SfmPanelDefaultStation { get; set; }
		/// <summary>Portal TIB (salidas de bus de enlace).</summary>
		public string TibBaseUrl { get; set; } = "https://www.tib.org";
		public string TibEntity { get; set; } = "ctmr4";
		public int TibGroupId { get; set; } = 20124;
		public int TibPollSeconds { get; set; } = 30;
		/// <summary>API MaaS EMT Palma (salidas urbanas de enlace).</summary>
		public string EmtBaseUrl { get; set; } = "https://www.emtpalma.cat/maas/api/v1";
		public int EmtPollSeconds { get; set; } = 20;
		public string DefaultLocation { get; set; } = "39.57634918310896,2.6546805667305313"; //Ubicación por defecto (para extraer Meteo).

		/// <summary>
		/// Distancia recorrida desde el PK de origen (m) durante la cual se muestra
		/// la pantalla de bienvenida. Por defecto 150.
		/// </summary>
		public int WelcomeDistanceMeters { get; set; } = 150;

		/// <summary>
		/// Antelación base (m) para el cartel de correspondencias / próxima estación.
		/// </summary>
		public int CorrespondenceBaseMeters { get; set; } = 300;

		/// <summary>
		/// Metros extra de antelación por cada bus anunciado en la tabla.
		/// </summary>
		public int CorrespondenceMetersPerBus { get; set; } = 100;

	}
}

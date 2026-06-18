using Sapphire2025Models.Aeneas;
namespace Tourmaline26.Logic
{
	/// <summary>
	/// Este elemento contiene toda la información necesaria sobre la unidad de tren.
	/// </summary>
	public class SystemConfiguration
	{
		public string Version  => "V1.1 (Beta)";
		public DateTime Release => new DateTime(2026,6,18);
		public Guid TrainId { get; set; } //Guid de este material móvil según Zafiro.
        public string Name { get; set; } = "Tren";
		public DateTime LastRelease { get; set; } //Fecha de la última instalación de hardware.
		public List<CameraInfo> Cameras { get; set; } = new List<CameraInfo>(); //Cámaras del tren.
		public string ToniCruz { get; set; } = "right";
        public string SapphireUrl { get; set; } = "https://material.trensfm.com:5031"; //Url para hacer peticiones al servidor REST de Zafiro
		public string MVBUrl { get; set; } = "http://172.16.20.11:8000/data";
		public string TExperienceUrl { get; set; } = "http://172.16.20.12:5005/server"; //Servidor API REST de Tourmaline Experiencwe
		public string TExperienceStr { get; set; } = "http://192.168.0.9:5005/stream"; //Streamer de Tourmaline Experience
	}
}

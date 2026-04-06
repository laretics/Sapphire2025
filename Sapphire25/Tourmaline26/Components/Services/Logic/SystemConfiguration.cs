using Sapphire2025Models.Aeneas;
namespace Tourmaline26.Components.Services.Logic
{
	/// <summary>
	/// Este elemento contiene toda la información necesaria sobre la unidad de tren.
	/// </summary>
	public class SystemConfiguration
	{
		public Guid TrainId { get; set; } //Guid de este material móvil según Zafiro.
        public string Name { get; set; } = "Tren";
		public DateTime LastRelease { get; set; } //Fecha de la última instalación de hardware.
		public List<CameraInfo> Cameras { get; set; } = new List<CameraInfo>(); //Cámaras del tren.
        public string SapphireUrl { get; set; } = "http://192.168.0.1"; //Url para hacer peticiones al servidor REST de Zafiro
	}
}

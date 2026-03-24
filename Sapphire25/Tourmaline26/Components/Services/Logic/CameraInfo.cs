using System.Net;

namespace Tourmaline26.Components.Services.Logic
{
	/// <summary>
	/// Este objeto contiene información sobre una de las cámaras del tren
	/// </summary>
	public class CameraInfo
	{
		public int Id { get; set; } = -1; //Numeración única de esta cámara.
		public string Name { get; set; } = "Camera";
		public int CoachId { get; set; } = 0; //Número de coche en que está situada esta cámara.
		public bool Essential { get; set; } = false; //¿Es necesaria esta cámara en la visualización reducida?
		public IPAddress Address{ get; set; } = IPAddress.None; //Dirección de la cámara
		public string SAddress
		{
			get => Address.ToString();
			set 
			{
				IPAddress? auxAddress = IPAddress.None;
				if (IPAddress.TryParse(value, out auxAddress)) 
					Address = auxAddress;
			} 			
		}

		public Enums.CameraType CameraType { get; set; } = Enums.CameraType.None; //Tipo de la cámara.
		public Enums.CameraCodec Codec { get; set; } = Enums.CameraCodec.R2P; //Forma de abrir el streaming.
	}
}

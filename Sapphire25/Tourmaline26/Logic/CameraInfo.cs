using System.Net;

namespace Tourmaline26.Logic
{
	/// <summary>
	/// Este objeto contiene información sobre una de las cámaras del tren
	/// </summary>
	public class CameraInfo
	{
		protected IPAddress mvarAddress = IPAddress.None;
		public int Id { get; set; } = -1; //Numeración única de esta cámara.
		public string Name { get; set; } = "Camera";
		public int CoachId { get; set; } = 0; //Número de coche en que está situada esta cámara.
		public bool Essential { get; set; } = false; //¿Es necesaria esta cámara en la visualización reducida?
		public string Address //Formato string para deserialización.
		{
			get => mvarAddress.ToString();
			set
			{
                IPAddress? auxAddress = IPAddress.None;
                if (IPAddress.TryParse(value, out auxAddress))
                    mvarAddress = auxAddress;
            }
		}
		public IPAddress IpAddress
		{
			get => mvarAddress;
			set => mvarAddress = value;
		}

		public Enums.CameraType CameraType { get; set; } = Enums.CameraType.None; //Tipo de la cámara.
		public Enums.CameraCodec Codec { get; set; } = Enums.CameraCodec.R2P; //Forma de abrir el streaming.

		/// <summary>Puerto RTSP (por defecto 554).</summary>
		public int RtspPort { get; set; } = 554;

		/// <summary>Path RTSP (por defecto /v2, como MediaMTX usaba).</summary>
		public string RtspPath { get; set; } = "/v2";

		/// <summary>URL RTSP completa opcional. Si está vacía se construye con Address/Port/Path.</summary>
		public string? StreamUrl { get; set; }

		/// <summary>Usuario RTSP opcional (vacío = sin autenticación).</summary>
		public string? Username { get; set; }

		/// <summary>Contraseña RTSP opcional.</summary>
		public string? Password { get; set; }

		/// <summary>Construye la URL RTSP efectiva para el cliente nativo.</summary>
		public string BuildRtspUrl()
		{
			if (!string.IsNullOrWhiteSpace(StreamUrl))
				return StreamUrl.Trim();

			string path = string.IsNullOrWhiteSpace(RtspPath) ? "/v2" : RtspPath.Trim();
			if (!path.StartsWith('/'))
				path = "/" + path;

			int port = RtspPort > 0 ? RtspPort : 554;
			string host = mvarAddress.Equals(IPAddress.None) ? "127.0.0.1" : mvarAddress.ToString();

			if (!string.IsNullOrEmpty(Username))
			{
				string user = Uri.EscapeDataString(Username);
				string pass = Uri.EscapeDataString(Password ?? string.Empty);
				return $"rtsp://{user}:{pass}@{host}:{port}{path}";
			}

			return $"rtsp://{host}:{port}{path}";
		}
	}
}

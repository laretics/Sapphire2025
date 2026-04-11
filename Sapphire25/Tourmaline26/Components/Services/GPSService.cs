using System.Globalization;
using System.IO.Ports;
using System.Threading;
using Tourmaline26.Components.Services.Logic;
namespace Tourmaline26.Components.Services
{
	public class GPSService
	{
		private string? mvarPort;      // Nombre del puerto serie
		private int mvarBauds = 9600;  // Velocidad por defecto
		private int mvarDataBits = 8;  // Bits de datos por defecto
		private Parity mvarParity = Parity.None;
		private StopBits mvarStopBits = StopBits.One;
		private Handshake mvarHandshake = Handshake.None;
		private ILogger<GPSService> mvarLogger;
		private readonly object mvarLock = new ();
		private SerialPort? mvarSerialPort;
		public GPSData? CurrentData{ get; private set; }

		public bool IsConfigured { get; private set; } = false;

		public GPSService(
			ILogger<GPSService> logger,
			IConfiguration config)
		{
			mvarLogger = logger;

			var gpsSection = config.GetSection("SystemConfiguration:gps");
			if (!gpsSection.Exists())
			{
				mvarLogger.LogWarning("No se ha encontrado la sección 'SystemConfiguration:gps' en la configuración.");
				return;
			}

			mvarPort = gpsSection.GetValue<string>("Port");
			if (string.IsNullOrWhiteSpace(mvarPort))
			{
				mvarLogger.LogWarning("No se ha especificado el puerto del GPS en la configuración.");
				return;
			}

			mvarBauds = gpsSection.GetValue<int?>("BaudRate") ?? 9600;
			mvarDataBits = gpsSection.GetValue<int?>("DataBits") ?? 8;
			mvarParity = Enum.TryParse(gpsSection.GetValue<string>("Parity"), out Parity parity) ? parity : Parity.None;
			mvarStopBits = Enum.TryParse(gpsSection.GetValue<string>("StopBits"), out StopBits stopBits) ? stopBits : StopBits.One;
			mvarHandshake = Enum.TryParse(gpsSection.GetValue<string>("Handshake"), out Handshake handshake) ? handshake : Handshake.None;

			// Comprobación rápida de existencia del puerto
			if (!SerialPort.GetPortNames().Contains(mvarPort))
			{
				mvarLogger.LogWarning($"El puerto GPS '{mvarPort}' no está disponible en este sistema.");
				return;
			}

			try
			{
				mvarSerialPort = new SerialPort(mvarPort, mvarBauds, mvarParity, mvarDataBits, mvarStopBits)
				{
					Handshake = mvarHandshake,
					NewLine = "\r\n",
					ReadTimeout = 2000,
					WriteTimeout = 2000
				};
				mvarSerialPort.Open();
				IsConfigured = true;
				mvarLogger.LogInformation($"GPS configurado en {mvarPort} a {mvarBauds} baudios.");
			}
			catch (Exception ex)
			{
				mvarLogger.LogWarning($"No se pudo abrir el puerto serie del GPS: {ex.Message}");
			}
		}

		private DateTime? ParseNmeaTime(string time, string? date)
		{
			// time: hhmmss.ss, date: ddmmyy
			try
			{
				int hour = int.Parse(time.Substring(0, 2));
				int min = int.Parse(time.Substring(2, 2));
				int sec = int.Parse(time.Substring(4, 2));
				if (!string.IsNullOrEmpty(date) && date.Length == 6)
				{
					int day = int.Parse(date.Substring(0, 2));
					int month = int.Parse(date.Substring(2, 2));
					int year = 2000 + int.Parse(date.Substring(4, 2));
					return new DateTime(year, month, day, hour, min, sec, DateTimeKind.Utc);
				}
				else
				{
					var now = DateTime.UtcNow;
					return new DateTime(now.Year, now.Month, now.Day, hour, min, sec, DateTimeKind.Utc);
				}
			}
			catch
			{
				return null;
			}
		}
		private double? ParseNmeaLat(string value, string hemi)
		{
			// value: ddmm.mmmm, hemi: N/S
			if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0)
			{
				var deg = Math.Floor(v / 100);
				var min = v - deg * 100;
				var coord = deg + min / 60.0;
				if (hemi == "S") coord = -coord;
				return coord;
			}
			return null;
		}
		private double? ParseNmeaLon(string value, string hemi)
		{
			// value: dddmm.mmmm, hemi: E/W
			if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0)
			{
				var deg = Math.Floor(v / 100);
				var min = v - deg * 100;
				var coord = deg + min / 60.0;
				if (hemi == "W") coord = -coord;
				return coord;
			}
			return null;
		}
		private GPSData? ParseNmea(string line)
		{
			// Ejemplo $GPRMC,hhmmss.ss,A,llll.ll,a,yyyyy.yy,a,x.x,x.x,ddmmyy,x.x,a*hh
			// Ejemplo $GPGGA,hhmmss.ss,llll.ll,a,yyyyy.yy,a,x,xx,x.x,x.x,M,x.x,M,x.x,xxxx
			try
			{
				var parts = line.Split(',');
				if (parts[0] == "$GPRMC" && parts.Length > 6 && parts[2] == "A")
				{
					GPSData nuevo = new GPSData();
					var lat = ParseNmeaLat(parts[3], parts[4]);
					var lon = ParseNmeaLon(parts[5], parts[6]);
					var time = ParseNmeaTime(parts[1], parts[9]);
					lock (this)
					{
						if(null!=lat)
							nuevo.Latitude = (double)lat;
						if(null!=lon)
							nuevo.Longitude = (double)lon;
						if(null!=time)
							nuevo.Time = (DateTime)time;
					}
					return nuevo;
				}
				else if (parts[0] == "$GPGGA" && parts.Length > 6 && parts[6] != "0")
				{
					GPSData nuevo = new GPSData();
					var lat = ParseNmeaLat(parts[2], parts[3]);
					var lon = ParseNmeaLon(parts[4], parts[5]);
					var time = ParseNmeaTime(parts[1], null);
					lock (this)
					{
						if (null != lat)
							nuevo.Latitude = (double)lat;
						if (null != lon)
							nuevo.Longitude = (double)lon;
						if (null != time)
							nuevo.Time = (DateTime)time;
					}
					return nuevo;
				}
			}
			catch
			{
				// Ignorar errores de parseo
			}
			return null;
		}
		public bool ReadLoop()
		{
			try
			{
				while (null!=mvarSerialPort && mvarSerialPort.IsOpen)
				{
					string? line = null;
					try
					{
						line = mvarSerialPort.ReadLine();						
					}
					catch (TimeoutException) { continue; }
					catch (Exception ex)
					{
						mvarLogger.LogWarning($"Error leyendo del GPS: {ex.Message}");
						return false;
					}

					if (line != null && (line.StartsWith("$GPRMC") || line.StartsWith("$GPGGA")))
					{
						CurrentData = ParseNmea(line);
						return true;
					}
				}
			}
			catch (Exception ex)
			{
				mvarLogger.LogWarning($"Error en el bucle de lectura del GPS: {ex.Message}");				
			}
			return false;
		}

	}
}

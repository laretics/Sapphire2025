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
		public GPSData CurrentData{ get; private set; }

		public bool IsConfigured { get; private set; } = false;

		public GPSService(
			ILogger<GPSService> logger,
			IConfiguration config)
		{
			mvarLogger = logger;
			CurrentData = new GPSData();

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
        private void ParseNmea(string line, GPSData output)
        {
			if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("$"))
				return; 
            try
            {
                var parts = line.Split(',');

                if (parts.Length < 6)
                    return;

                string sentenceType = parts[0]; // ej: $GPRMC, $GNRMC, $GPGGA, etc.
				GPSData nuevo = output;

                // ====================== $GPRMC / $GNRMC ======================
                if ((sentenceType == "$GPRMC" || sentenceType == "$GNRMC") && parts.Length > 9)
                {
                    if (parts[2] != "A") // "V" = inválido
                        return;     // o quita este return si quieres datos aunque sea inválido

                    // Latitud y Longitud (reutilizo tus métodos)
                    var lat = ParseNmeaLat(parts[3], parts[4]);
                    var lon = ParseNmeaLon(parts[5], parts[6]);
                    var time = ParseNmeaTime(parts[1], parts[9]);

                    if (lat != null) nuevo.Latitude = (double)lat;
                    if (lon != null) nuevo.Longitude = (double)lon;
                    if (time != null) nuevo.Time = (DateTime)time;

                    // Velocidad
                    if (double.TryParse(parts[7], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double knots))
                    {
                        nuevo.SpeedKnots = knots;
                        nuevo.SpeedKmh = knots * 1.852;
                        nuevo.SpeedMs = knots * 0.514444;
                    }

                    // Rumbo (Course Over Ground)
                    if (double.TryParse(parts[8], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double course))
                    {
                        nuevo.Course = course;
                    }

                    lock (this)
                    {
                        return;
                    }
                }

                // ====================== $GPGGA / $GNGGA ======================
                else if ((sentenceType == "$GPGGA" || sentenceType == "$GNGGA") && parts.Length > 9)
                {
                    if (parts[6] == "0") // 0 = sin fix
                        return;

                    var lat = ParseNmeaLat(parts[2], parts[3]);
                    var lon = ParseNmeaLon(parts[4], parts[5]);
                    var time = ParseNmeaTime(parts[1], null);

                    if (lat != null) nuevo.Latitude = (double)lat;
                    if (lon != null) nuevo.Longitude = (double)lon;
                    if (time != null) nuevo.Time = (DateTime)time;

                    // Calidad del fix
                    if (int.TryParse(parts[6], out int quality))
                        nuevo.FixQuality = quality;

                    // Número de satélites
                    if (int.TryParse(parts[7], out int sats))
                        nuevo.SatellitesUsed = sats;

                    // HDOP
                    if (double.TryParse(parts[8], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double hdop))
                        nuevo.HDOP = hdop;

                    // Altitud
                    if (double.TryParse(parts[9], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double alt))
                        nuevo.Altitude = alt;

                    lock (this)
                    {
                        return;
                    }
                }

                // ====================== $GPVTG / $GNVTG (mejor para velocidad y rumbo) ======================
                else if ((sentenceType == "$GPVTG" || sentenceType == "$GNVTG") && parts.Length > 7)
                {
                    // Velocidad en km/h (campo 7 suele ser más directo)
                    if (double.TryParse(parts[7], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double kmh))
                    {
                        nuevo.SpeedKmh = kmh;
                        nuevo.SpeedKnots = kmh / 1.852;
                        nuevo.SpeedMs = kmh / 3.6;
                    }

                    // Rumbo verdadero (campo 1)
                    if (double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double course))
                    {
                        nuevo.Course = course;
                    }

                    lock (this)
                    {
                        return;   // VTG normalmente no trae posición, solo velocidad/rumbo
                    }
                }
            }
            catch
            {
                // Ignorar líneas mal formadas o con errores de parseo
            }

            return;
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
						ParseNmea(line,CurrentData);
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

using System.Globalization;
using System.IO.Ports;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using Tourmaline26.Logic;
namespace Tourmaline26.Services
{
    public class GPSService : IDisposable
    {
        private enum GpsMode
        {
            SerialEmitter,
            UdpReceiver
        }

        private sealed class GpsBroadcastPacket
        {
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public DateTime Time { get; set; }
            public double SpeedKnots { get; set; }
            public double SpeedKmh { get; set; }
            public double SpeedMs { get; set; }
            public double Course { get; set; }
            public double Altitude { get; set; }
            public int FixQuality { get; set; }
            public int SatellitesUsed { get; set; }
            public double HDOP { get; set; }
        }

        private ILogger<GPSService> mvarLogger;
        private readonly object mvarLock = new();

        private readonly JsonSerializerOptions mvarJsonOptions = new(JsonSerializerDefaults.Web);
        private GpsMode mvarMode = GpsMode.SerialEmitter;

        private string? mvarPort;      // Nombre del puerto serie
        private int mvarBauds = 9600;  // Velocidad por defecto
        private int mvarDataBits = 8;  // Bits de datos por defecto
        private Parity mvarParity = Parity.None;
        private StopBits mvarStopBits = StopBits.One;
        private Handshake mvarHandshake = Handshake.None;

        private int mvarBroadcastPort;
        private string mvarBroadcastAddress = "255.255.255.255";
        private int mvarListenPort;

        private SerialPort? mvarSerialPort;
        private UdpClient? mvarUdpSendClient;
        private UdpClient? mvarUdpReceiveClient;
        private IPEndPoint? mvarBroadcastEndPoint;

        private DateTime mvarNextReconectAttempt = DateTime.MinValue;

        public GPSData CurrentData { get; private set; }
        public bool IsConfigured { get; private set; } = false;
        public bool IsConnected { get; private set; } = false;

        /// <summary>True cuando Tourmaline actúa solo como receptor UDP (GpsEmitter en otro PC).</summary>
        public bool IsUdpReceiver => GpsMode.UdpReceiver == mvarMode;

        /// <summary>Etiqueta legible del modo activo (UI).</summary>
        public string ModeDisplayName => IsUdpReceiver ? "Receptor UDP" : "Emisor serie";

        public int ListenPort => mvarListenPort;
        public int BroadcastPort => mvarBroadcastPort;
        public string BroadcastAddress => mvarBroadcastAddress;

        /// <summary>Origen del último paquete UDP recibido (IP:puerto), si aplica.</summary>
        public string? LastRemoteEndPoint { get; private set; }


        public GPSService(
            ILogger<GPSService> logger,
            IConfiguration config)
        {
            mvarLogger = logger;
            CurrentData = new GPSData();

            var gpsSection = config.GetSection("SystemConfiguration:gps");
            if (!gpsSection.Exists())
            {
                mvarLogger.LogWarning("Couldn't find 'SystemConfiguration:gps' section.");
                return;
            }

            string? auxMode = gpsSection.GetValue<string>("Mode");
            if (string.Equals(auxMode, "Receiver", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(auxMode, "Udp", StringComparison.OrdinalIgnoreCase))
            {
                mvarMode = GpsMode.UdpReceiver;
                mvarListenPort = gpsSection.GetValue<int?>("ListenPort") ?? 0;
                if (mvarListenPort <= 0)
                {
                    mvarLogger.LogWarning("No UDP port configured for GPS.");
                    return;
                }

                try
                {
                    mvarUdpReceiveClient = new UdpClient(mvarListenPort);
                    IsConfigured = true;
                    IsConnected = true;
                    mvarLogger.LogInformation("GPS in UDP receiver mode. Port: {Port}", mvarListenPort);
                }
                catch (Exception ex)
                {
                    mvarLogger.LogWarning("Could not open UDP port {Port}: {Message}", mvarListenPort, ex.Message);
                }
                return;

            }
            mvarMode = GpsMode.SerialEmitter;

            mvarPort = gpsSection.GetValue<string>("Port");
            if (string.IsNullOrWhiteSpace(mvarPort))
            {
                mvarLogger.LogWarning("GPS port not specified.");
                return;
            }

            mvarBauds = gpsSection.GetValue<int?>("BaudRate") ?? 9600;
            mvarDataBits = gpsSection.GetValue<int?>("DataBits") ?? 8;
            mvarParity = Enum.TryParse(gpsSection.GetValue<string>("Parity"), out Parity parity) ? parity : Parity.None;
            mvarStopBits = Enum.TryParse(gpsSection.GetValue<string>("StopBits"), out StopBits stopBits) ? stopBits : StopBits.One;
            mvarHandshake = Enum.TryParse(gpsSection.GetValue<string>("Handshake"), out Handshake handshake) ? handshake : Handshake.None;

            mvarBroadcastPort = gpsSection.GetValue<int?>("BroadcastPort") ?? 0;
            mvarBroadcastAddress = gpsSection.GetValue<string>("BroadcastAddress") ?? "255.255.255.255";

            // Comprobación rápida de existencia del puerto
            if (!SerialPort.GetPortNames().Contains(mvarPort))
            {
                mvarLogger.LogWarning($"GPS port '{mvarPort}' not available.");
                return;
            }

            if (mvarBroadcastPort > 0)
            {
                try
                {
                    mvarUdpSendClient = new UdpClient();
                    mvarUdpSendClient.EnableBroadcast = true;
                    mvarBroadcastEndPoint = new IPEndPoint(IPAddress.Parse(mvarBroadcastAddress), mvarBroadcastPort);
                }
                catch (Exception ex)
                {
                    mvarLogger.LogWarning("Could not prepara UDP broadcast: {Message}", ex.Message);
                    return;
                }
            }
            IsConfigured = true;
        }

        private void DisposeSerialPort()
        {
            if (null != mvarSerialPort && mvarSerialPort.IsOpen)
                mvarSerialPort.Close();
            mvarSerialPort = null;
        }

        private void DisposeUdp()
        {
            mvarUdpSendClient?.Dispose();
            mvarUdpSendClient = null;

            mvarUdpReceiveClient?.Dispose();
            mvarUdpReceiveClient = null;

            mvarBroadcastEndPoint = null;
        }

        public void Dispose()
        {
            DisposeSerialPort();
            DisposeUdp();
        }

        private bool EnsureConnected()
        {
            if (!IsConfigured)
                return false; //No vamos a reconectar si no está bien el puerto.

            if (GpsMode.UdpReceiver == mvarMode)
                return null != mvarUdpReceiveClient;

            if (true == mvarSerialPort?.IsOpen)
                return true; //Conexión mantenida.

            if (DateTime.UtcNow < mvarNextReconectAttempt)
                return false; //No hemos conseguido reconectar, pero lo intentaremos después

            mvarNextReconectAttempt = DateTime.UtcNow.AddSeconds(5);
            //Intentamos reconectar.
            try
            {
                DisposeSerialPort();
                mvarSerialPort = new SerialPort(mvarPort, mvarBauds, mvarParity, mvarDataBits, mvarStopBits)
                {
                    Handshake = mvarHandshake,
                    NewLine = "\r\n",
                    ReadTimeout = 2000,
                    WriteTimeout = 2000
                };
                mvarSerialPort.Open();
                IsConnected = true;
                return true; //Ha conseguido reconectar.
            }
            catch (Exception ex)
            {
                mvarLogger.LogWarning("GPS connection error: {Message}", ex.Message);
                return false;
            }
        }

        private void BroadcastCurrentData()
        {
            if (null == mvarUdpSendClient || null == mvarBroadcastEndPoint) return;

            try
            {
                GPSData snapshot;
                lock (mvarLock)
                {
                    snapshot = new GPSData
                    {
                        Latitude = CurrentData.Latitude,
                        Longitude = CurrentData.Longitude,
                        Time = CurrentData.Time,
                        SpeedKnots = CurrentData.SpeedKnots,
                        SpeedKmh = CurrentData.SpeedKmh,
                        SpeedMs = CurrentData.SpeedMs,
                        Course = CurrentData.Course,
                        Altitude = CurrentData.Altitude,
                        FixQuality = CurrentData.FixQuality,
                        SatellitesUsed = CurrentData.SatellitesUsed,
                        HDOP = CurrentData.HDOP
                    };
                }

                GpsBroadcastPacket packet = new GpsBroadcastPacket
                {
                    Latitude = snapshot.Latitude,
                    Longitude = snapshot.Longitude,
                    Time = snapshot.Time,
                    SpeedKnots = snapshot.SpeedKnots,
                    SpeedKmh = snapshot.SpeedKmh,
                    SpeedMs = snapshot.SpeedMs,
                    Course = snapshot.Course,
                    Altitude = snapshot.Altitude,
                    FixQuality = snapshot.FixQuality,
                    SatellitesUsed = snapshot.SatellitesUsed,
                    HDOP = snapshot.HDOP
                };

                byte[] payload = JsonSerializer.SerializeToUtf8Bytes(packet, mvarJsonOptions);
                mvarUdpSendClient.Send(payload, payload.Length, mvarBroadcastEndPoint);
            }
            catch (Exception ex)
            {
                mvarLogger.LogWarning("Error sending GPS data by UDP: {Message}", ex.Message);
            }
        }

        private bool ReadUdpLoop()
        {
            if (null == mvarUdpReceiveClient) return false;

            bool updated = false;

            while (mvarUdpReceiveClient.Available > 0)
            {
                try
                {
                    IPEndPoint remote = new(IPAddress.Any, 0);
                    byte[] buffer = mvarUdpReceiveClient.Receive(ref remote);
                    LastRemoteEndPoint = remote.ToString();

                    GpsBroadcastPacket? packet = JsonSerializer.Deserialize<GpsBroadcastPacket>(buffer, mvarJsonOptions);
                    if (null == packet) continue;

                    lock (mvarLock)
                    {
                        CurrentData.Latitude = packet.Latitude;
                        CurrentData.Longitude = packet.Longitude;
                        CurrentData.Time = packet.Time.Kind == DateTimeKind.Utc
                            ? packet.Time
                            : DateTime.SpecifyKind(packet.Time, DateTimeKind.Utc);

                        CurrentData.SpeedKnots = packet.SpeedKnots;
                        CurrentData.SpeedKmh = packet.SpeedKmh;
                        CurrentData.SpeedMs = packet.SpeedMs;
                        CurrentData.Course = packet.Course;
                        CurrentData.Altitude = packet.Altitude;
                        CurrentData.FixQuality = packet.FixQuality;
                        CurrentData.SatellitesUsed = packet.SatellitesUsed;
                        CurrentData.HDOP = packet.HDOP;
                    }
                    updated = true;
                }
                catch (Exception ex)
                {
                    mvarLogger.LogWarning("Error reading GPS by UDP: {Message}", ex.Message);
                    break;
                }
            }
            return updated;
        }

        public bool ReadLoop()
        {
            try
            {
                if (!IsConfigured) return false;

                if (GpsMode.UdpReceiver == mvarMode) return ReadUdpLoop();

                if (!EnsureConnected()) return false;

                System.Diagnostics.Debug.Assert(null != mvarSerialPort);

                const int maxBufferedBytes = 4096;

                if (mvarSerialPort.BytesToRead > maxBufferedBytes)
                {
                    mvarLogger.LogWarning("GPS data overload. Purge.");
                    mvarSerialPort.DiscardInBuffer();
                    return false;
                }
                bool updated = false;
                while (mvarSerialPort.BytesToRead > 0)
                {
                    string? line = null;
                    try
                    {
                        line = mvarSerialPort.ReadLine();
                    }
                    catch (TimeoutException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        mvarLogger.LogWarning($"GPS reading error: {ex.Message}");
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (line.StartsWith("$GPRMC") || line.StartsWith("$GNRMC") ||
                    line.StartsWith("$GPGGA") || line.StartsWith("$GNGGA") ||
                    line.StartsWith("$GPVTG") || line.StartsWith("$GNVTG"))
                    {
                        ParseNmea(line, CurrentData);
                        updated = true;
                    }
                }

                if (updated)
                    BroadcastCurrentData();

                return updated;
            }
            catch (Exception ex)
            {
                mvarLogger.LogWarning($"Reading GPS Error: {ex.Message}");
            }
            return false;
        }

        public string? Port => mvarPort;
        public int Bauds => mvarBauds;
        public int DataBits => mvarDataBits;
        public Parity Parity => mvarParity;
        public StopBits StopBits => mvarStopBits;
        public Handshake Handshake => mvarHandshake;

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
    }
}


/* appsettings.json
 * 
 * Ejemplo de configuración en emisor: 
 
 {
  "SystemConfiguration": {
    "gps": {
      "Mode": "Emitter",
      "Port": "COM6",
      "BaudRate": 9600,
      "DataBits": 8,
      "Parity": "None",
      "StopBits": 1,
      "Handshake": "None",
      "BroadcastAddress": "255.255.255.255",
      "BroadcastPort": 5005
    }
  }
} 


En el receptor:
{
  "SystemConfiguration": {
    "gps": {
      "Mode": "Receiver",
      "ListenPort": 5005
    }
  }
}
  
 * */
using System.Globalization;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
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

        private readonly ILogger<GPSService> mvarLogger;
        private readonly object mvarLock = new();
        private readonly JsonSerializerOptions mvarJsonOptions = new(JsonSerializerDefaults.Web);

        private GpsMode mvarMode = GpsMode.SerialEmitter;

        private string? mvarPort;
        private int mvarBauds = 9600;
        private int mvarDataBits = 8;
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
        private DateTime mvarLastUdpPacketUtc = DateTime.MinValue;
        private long mvarUdpPacketsReceived;
        private long mvarUdpPacketsRejected;

        private CancellationTokenSource? mvarUdpCts;
        private Task? mvarUdpReceiveTask;

        public GPSData CurrentData { get; private set; }
        public bool IsConfigured { get; private set; }
        public bool IsConnected { get; private set; }

        /// <summary>True cuando Tourmaline actúa solo como receptor UDP (GpsEmitter en otro PC).</summary>
        public bool IsUdpReceiver => GpsMode.UdpReceiver == mvarMode;

        /// <summary>Etiqueta legible del modo activo (UI).</summary>
        public string ModeDisplayName => IsUdpReceiver ? "Receptor UDP" : "Emisor serie";

        public int ListenPort => mvarListenPort;
        public int BroadcastPort => mvarBroadcastPort;
        public string BroadcastAddress => mvarBroadcastAddress;
        public long UdpPacketsReceived => Interlocked.Read(ref mvarUdpPacketsReceived);
        public long UdpPacketsRejected => Interlocked.Read(ref mvarUdpPacketsRejected);
        public DateTime LastUdpPacketUtc
        {
            get { lock (mvarLock) return mvarLastUdpPacketUtc; }
        }

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

                if (TryStartUdpReceiver(mvarListenPort))
                {
                    IsConfigured = true;
                    IsConnected = true;
                    mvarLogger.LogInformation(
                        "GPS in UDP receiver mode. Listening on 0.0.0.0:{Port} (any interface).",
                        mvarListenPort);
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

            // No abortar la configuración si el puerto no está aún: se reintentará en EnsureConnected.
            if (!SerialPort.GetPortNames().Contains(mvarPort))
            {
                mvarLogger.LogWarning(
                    "GPS port '{Port}' not available at startup; will retry when present. Available: {Ports}",
                    mvarPort,
                    string.Join(", ", SerialPort.GetPortNames()) is { Length: > 0 } list ? list : "(none)");
            }

            if (mvarBroadcastPort > 0)
            {
                try
                {
                    mvarUdpSendClient = new UdpClient();
                    mvarUdpSendClient.EnableBroadcast = true;
                    mvarBroadcastEndPoint = new IPEndPoint(IPAddress.Parse(mvarBroadcastAddress), mvarBroadcastPort);
                    mvarLogger.LogInformation(
                        "GPS serial emitter will also broadcast to {Address}:{Port}",
                        mvarBroadcastAddress,
                        mvarBroadcastPort);
                }
                catch (Exception ex)
                {
                    mvarLogger.LogWarning("Could not prepare UDP broadcast: {Message}", ex.Message);
                    // Seguimos con serie aunque el broadcast falle.
                }
            }

            IsConfigured = true;
        }

        /// <summary>
        /// Abre el socket UDP de forma explícita (Any + ReuseAddress) y arranca un bucle
        /// de recepción en background. El poll de Available es frágil si hay picos de tráfico
        /// o si el hilo de lectura no se invoca con suficiente frecuencia.
        /// </summary>
        private bool TryStartUdpReceiver(int port)
        {
            try
            {
                StopUdpReceiver();

                Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                // En Windows, ExclusiveAddressUse=false + ReuseAddress permite convivir mejor
                // con reinicios rápidos del proceso.
                try { socket.ExclusiveAddressUse = false; } catch { /* no soportado en todas las plataformas */ }
                socket.Bind(new IPEndPoint(IPAddress.Any, port));

                mvarUdpReceiveClient = new UdpClient { Client = socket };
                // Aceptar también datagramas de broadcast en interfaces multi-homed.
                mvarUdpReceiveClient.EnableBroadcast = true;

                mvarUdpCts = new CancellationTokenSource();
                CancellationToken token = mvarUdpCts.Token;
                mvarUdpReceiveTask = Task.Run(() => UdpReceiveLoopAsync(token), token);
                return true;
            }
            catch (Exception ex)
            {
                mvarLogger.LogWarning("Could not open UDP port {Port}: {Message}", port, ex.Message);
                StopUdpReceiver();
                return false;
            }
        }

        private async Task UdpReceiveLoopAsync(CancellationToken token)
        {
            mvarLogger.LogInformation("GPS UDP receive loop running on port {Port}.", mvarListenPort);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (null == mvarUdpReceiveClient)
                    {
                        await Task.Delay(200, token);
                        continue;
                    }

                    // ReceiveAsync con token (NET 6+). Si el socket se cierra, sale.
                    UdpReceiveResult result = await mvarUdpReceiveClient.ReceiveAsync(token);
                    ApplyUdpPacket(result.Buffer, result.RemoteEndPoint);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException ex) when (token.IsCancellationRequested)
                {
                    mvarLogger.LogDebug("GPS UDP socket closed: {Message}", ex.Message);
                    break;
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref mvarUdpPacketsRejected);
                    mvarLogger.LogWarning("Error reading GPS by UDP: {Message}", ex.Message);
                    try { await Task.Delay(100, token); } catch (OperationCanceledException) { break; }
                }
            }

            mvarLogger.LogInformation("GPS UDP receive loop stopped.");
        }

        private void ApplyUdpPacket(byte[] buffer, IPEndPoint remote)
        {
            try
            {
                GpsBroadcastPacket? packet = JsonSerializer.Deserialize<GpsBroadcastPacket>(buffer, mvarJsonOptions);
                if (null == packet)
                {
                    Interlocked.Increment(ref mvarUdpPacketsRejected);
                    return;
                }

                // Paquete sin posición útil: no pisar un fix bueno con ceros.
                if (packet.Latitude == 0 && packet.Longitude == 0 && packet.FixQuality <= 0)
                {
                    Interlocked.Increment(ref mvarUdpPacketsRejected);
                    return;
                }

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

                    mvarLastUdpPacketUtc = DateTime.UtcNow;
                    LastRemoteEndPoint = remote.ToString();
                    IsConnected = true;
                }

                long n = Interlocked.Increment(ref mvarUdpPacketsReceived);
                if (1 == n || 0 == n % 50)
                {
                    mvarLogger.LogInformation(
                        "GPS UDP packet #{Count} from {Remote}: lat={Lat:F6} lon={Lon:F6} fix={Fix} speed={Speed:F1} km/h",
                        n,
                        remote,
                        packet.Latitude,
                        packet.Longitude,
                        packet.FixQuality,
                        packet.SpeedKmh);
                }
            }
            catch (JsonException ex)
            {
                Interlocked.Increment(ref mvarUdpPacketsRejected);
                mvarLogger.LogWarning(
                    "GPS UDP JSON invalid from {Remote} ({Bytes} bytes): {Message}",
                    remote,
                    buffer.Length,
                    ex.Message);
            }
        }

        private void StopUdpReceiver()
        {
            try { mvarUdpCts?.Cancel(); } catch { /* ignore */ }

            try
            {
                mvarUdpReceiveClient?.Close();
                mvarUdpReceiveClient?.Dispose();
            }
            catch { /* ignore */ }
            mvarUdpReceiveClient = null;

            try
            {
                if (null != mvarUdpReceiveTask)
                    mvarUdpReceiveTask.Wait(TimeSpan.FromSeconds(1));
            }
            catch { /* ignore */ }
            mvarUdpReceiveTask = null;

            try { mvarUdpCts?.Dispose(); } catch { /* ignore */ }
            mvarUdpCts = null;
        }

        private void DisposeSerialPort()
        {
            if (null != mvarSerialPort && mvarSerialPort.IsOpen)
                mvarSerialPort.Close();
            mvarSerialPort = null;
        }

        private void DisposeUdpSend()
        {
            mvarUdpSendClient?.Dispose();
            mvarUdpSendClient = null;
            mvarBroadcastEndPoint = null;
        }

        public void Dispose()
        {
            DisposeSerialPort();
            StopUdpReceiver();
            DisposeUdpSend();
        }

        private bool EnsureConnected()
        {
            if (!IsConfigured)
                return false;

            if (GpsMode.UdpReceiver == mvarMode)
                return null != mvarUdpReceiveClient;

            if (true == mvarSerialPort?.IsOpen)
                return true;

            if (DateTime.UtcNow < mvarNextReconectAttempt)
                return false;

            mvarNextReconectAttempt = DateTime.UtcNow.AddSeconds(5);

            try
            {
                DisposeSerialPort();

                if (string.IsNullOrWhiteSpace(mvarPort))
                    return false;

                if (!SerialPort.GetPortNames().Contains(mvarPort))
                {
                    mvarLogger.LogDebug("GPS port {Port} still not present.", mvarPort);
                    IsConnected = false;
                    return false;
                }

                mvarSerialPort = new SerialPort(mvarPort, mvarBauds, mvarParity, mvarDataBits, mvarStopBits)
                {
                    Handshake = mvarHandshake,
                    NewLine = "\r\n",
                    ReadTimeout = 2000,
                    WriteTimeout = 2000
                };
                mvarSerialPort.Open();
                IsConnected = true;
                mvarLogger.LogInformation("GPS serial port {Port} opened.", mvarPort);
                return true;
            }
            catch (Exception ex)
            {
                mvarLogger.LogWarning("GPS connection error: {Message}", ex.Message);
                IsConnected = false;
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

                GpsBroadcastPacket packet = new()
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

        /// <summary>
        /// En modo UDP: devuelve true si llegó un paquete reciente (actualizado por el bucle background).
        /// En modo serie: lee NMEA del puerto y opcionalmente reemite por UDP.
        /// </summary>
        public bool ReadLoop()
        {
            try
            {
                if (!IsConfigured) return false;

                if (GpsMode.UdpReceiver == mvarMode)
                {
                    // El bucle background ya actualiza CurrentData; aquí solo reportamos frescura.
                    DateTime last;
                    lock (mvarLock) last = mvarLastUdpPacketUtc;
                    if (last == DateTime.MinValue)
                        return false;
                    // Consideramos "nueva lectura" si el paquete es de los últimos 3 s.
                    return (DateTime.UtcNow - last) < TimeSpan.FromSeconds(3);
                }

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
                    string? line;
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
                        mvarLogger.LogWarning("GPS reading error: {Message}", ex.Message);
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (line.StartsWith("$GPRMC") || line.StartsWith("$GNRMC") ||
                        line.StartsWith("$GPGGA") || line.StartsWith("$GNGGA") ||
                        line.StartsWith("$GPVTG") || line.StartsWith("$GNVTG"))
                    {
                        if (ParseNmea(line))
                            updated = true;
                    }
                }

                if (updated)
                    BroadcastCurrentData();

                return updated;
            }
            catch (Exception ex)
            {
                mvarLogger.LogWarning("Reading GPS Error: {Message}", ex.Message);
            }
            return false;
        }

        public string? Port => mvarPort;
        public int Bauds => mvarBauds;
        public int DataBits => mvarDataBits;
        public Parity Parity => mvarParity;
        public StopBits StopBits => mvarStopBits;
        public Handshake Handshake => mvarHandshake;

        private static DateTime? ParseNmeaTime(string time, string? date)
        {
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

                DateTime now = DateTime.UtcNow;
                return new DateTime(now.Year, now.Month, now.Day, hour, min, sec, DateTimeKind.Utc);
            }
            catch
            {
                return null;
            }
        }

        private static double? ParseNmeaLat(string value, string hemi)
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0)
            {
                double deg = Math.Floor(v / 100);
                double min = v - deg * 100;
                double coord = deg + min / 60.0;
                if (hemi == "S") coord = -coord;
                return coord;
            }
            return null;
        }

        private static double? ParseNmeaLon(string value, string hemi)
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0)
            {
                double deg = Math.Floor(v / 100);
                double min = v - deg * 100;
                double coord = deg + min / 60.0;
                if (hemi == "W") coord = -coord;
                return coord;
            }
            return null;
        }

        /// <summary>Parsea una sentencia NMEA y actualiza CurrentData bajo lock. True si hubo datos útiles.</summary>
        private bool ParseNmea(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("$"))
                return false;

            try
            {
                string[] parts = line.Split(',');
                if (parts.Length < 6)
                    return false;

                string sentenceType = parts[0];
                bool changed = false;

                lock (mvarLock)
                {
                    if ((sentenceType == "$GPRMC" || sentenceType == "$GNRMC") && parts.Length > 9)
                    {
                        if (parts[2] != "A")
                            return false;

                        double? lat = ParseNmeaLat(parts[3], parts[4]);
                        double? lon = ParseNmeaLon(parts[5], parts[6]);
                        DateTime? time = ParseNmeaTime(parts[1], parts[9]);

                        if (lat != null) { CurrentData.Latitude = lat.Value; changed = true; }
                        if (lon != null) { CurrentData.Longitude = lon.Value; changed = true; }
                        if (time != null) { CurrentData.Time = time.Value; changed = true; }

                        if (double.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out double knots))
                        {
                            CurrentData.SpeedKnots = knots;
                            CurrentData.SpeedKmh = knots * 1.852;
                            CurrentData.SpeedMs = knots * 0.514444;
                            changed = true;
                        }

                        if (double.TryParse(parts[8], NumberStyles.Float, CultureInfo.InvariantCulture, out double course))
                        {
                            CurrentData.Course = course;
                            changed = true;
                        }

                        if (CurrentData.FixQuality == 0)
                            CurrentData.FixQuality = 1;

                        return changed;
                    }

                    if ((sentenceType == "$GPGGA" || sentenceType == "$GNGGA") && parts.Length > 9)
                    {
                        if (parts[6] == "0")
                            return false;

                        double? lat = ParseNmeaLat(parts[2], parts[3]);
                        double? lon = ParseNmeaLon(parts[4], parts[5]);
                        DateTime? time = ParseNmeaTime(parts[1], null);

                        if (lat != null) { CurrentData.Latitude = lat.Value; changed = true; }
                        if (lon != null) { CurrentData.Longitude = lon.Value; changed = true; }
                        if (time != null) { CurrentData.Time = time.Value; changed = true; }

                        if (int.TryParse(parts[6], out int quality))
                        {
                            CurrentData.FixQuality = quality;
                            changed = true;
                        }
                        if (int.TryParse(parts[7], out int sats))
                            CurrentData.SatellitesUsed = sats;
                        if (double.TryParse(parts[8], NumberStyles.Float, CultureInfo.InvariantCulture, out double hdop))
                            CurrentData.HDOP = hdop;
                        if (double.TryParse(parts[9], NumberStyles.Float, CultureInfo.InvariantCulture, out double alt))
                            CurrentData.Altitude = alt;

                        return changed;
                    }

                    if ((sentenceType == "$GPVTG" || sentenceType == "$GNVTG") && parts.Length > 7)
                    {
                        if (double.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out double kmh))
                        {
                            CurrentData.SpeedKmh = kmh;
                            CurrentData.SpeedKnots = kmh / 1.852;
                            CurrentData.SpeedMs = kmh / 3.6;
                            changed = true;
                        }

                        if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double course))
                        {
                            CurrentData.Course = course;
                            changed = true;
                        }

                        return changed;
                    }
                }
            }
            catch
            {
                // Línea mal formada.
            }

            return false;
        }
    }
}


/* appsettings.json
 *
 * Emisor (serie + rebroadcast UDP):
 * {
 *   "SystemConfiguration": {
 *     "gps": {
 *       "Mode": "Emitter",
 *       "Port": "COM6",
 *       "BaudRate": 9600,
 *       "BroadcastAddress": "255.255.255.255",
 *       "BroadcastPort": 5005
 *     }
 *   }
 * }
 *
 * Receptor (GpsEmitter en otro equipo):
 * {
 *   "SystemConfiguration": {
 *     "gps": {
 *       "Mode": "Receiver",
 *       "ListenPort": 5005
 *     }
 *   }
 * }
 */

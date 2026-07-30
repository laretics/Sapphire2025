using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using GpsEmitter.Models;
using Microsoft.Extensions.Options;

namespace GpsEmitter;

public sealed class GpsEmitterWorker : BackgroundService
{
    private readonly ILogger<GpsEmitterWorker> mvarLogger;
    private readonly EmitterOptions mvarOptions;
    private readonly JsonSerializerOptions mvarJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly GpsBroadcastPacket mvarCurrent = new();
    private readonly object mvarLock = new();

    private UdpClient? mvarUdp;
    private IPEndPoint? mvarEndPoint;
    private SerialPort? mvarSerial;
    private DateTime mvarNextReconnectUtc = DateTime.MinValue;
    private DateTime mvarLastStatusUtc = DateTime.MinValue;
    private long mvarPacketsSent;
    private long mvarNmeaAccepted;

    public GpsEmitterWorker(ILogger<GpsEmitterWorker> logger, IOptions<EmitterOptions> options)
    {
        mvarLogger = logger;
        mvarOptions = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        PrintBanner();

        if (mvarOptions.BroadcastPort <= 0)
        {
            mvarLogger.LogError("BroadcastPort no válido ({Port}).", mvarOptions.BroadcastPort);
            return;
        }

        try
        {
            mvarUdp = new UdpClient();
            mvarUdp.EnableBroadcast = true;
            mvarEndPoint = new IPEndPoint(IPAddress.Parse(mvarOptions.BroadcastAddress), mvarOptions.BroadcastPort);
            mvarLogger.LogInformation(
                "UDP listo → {Address}:{Port}",
                mvarOptions.BroadcastAddress,
                mvarOptions.BroadcastPort);
        }
        catch (Exception ex)
        {
            mvarLogger.LogError(ex, "No se pudo preparar el cliente UDP.");
            return;
        }

        if (mvarOptions.IsSimulate)
            await RunSimulateAsync(stoppingToken);
        else
            await RunSerialAsync(stoppingToken);
    }

    private void PrintBanner()
    {
        Console.WriteLine();
        Console.WriteLine("  GpsEmitter — puente GPS serie → UDP (Tourmaline)");
        Console.WriteLine("  Ctrl+C para detener");
        Console.WriteLine();

        if (mvarOptions.IsSimulate)
        {
            mvarLogger.LogInformation(
                "Modo SIMULATE  lat={Lat:F6} lon={Lon:F6} cada {Ms} ms",
                mvarOptions.SimulateLatitude,
                mvarOptions.SimulateLongitude,
                mvarOptions.SimulateIntervalMs);
        }
        else
        {
            mvarLogger.LogInformation(
                "Modo SERIAL  {Port} @ {Baud}  →  {Address}:{UdpPort}",
                mvarOptions.Port,
                mvarOptions.BaudRate,
                mvarOptions.BroadcastAddress,
                mvarOptions.BroadcastPort);

            string[] ports = SerialPort.GetPortNames();
            mvarLogger.LogInformation(
                "Puertos serie disponibles: {Ports}",
                ports.Length == 0 ? "(ninguno)" : string.Join(", ", ports.OrderBy(p => p)));
        }
    }

    private async Task RunSimulateAsync(CancellationToken stoppingToken)
    {
        int interval = Math.Max(100, mvarOptions.SimulateIntervalMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            GpsBroadcastPacket packet = new()
            {
                Latitude = mvarOptions.SimulateLatitude,
                Longitude = mvarOptions.SimulateLongitude,
                Time = DateTime.UtcNow,
                SpeedKmh = mvarOptions.SimulateSpeedKmh,
                SpeedKnots = mvarOptions.SimulateSpeedKmh / 1.852,
                SpeedMs = mvarOptions.SimulateSpeedKmh / 3.6,
                Course = mvarOptions.SimulateCourse,
                Altitude = 0,
                FixQuality = 1,
                SatellitesUsed = 8,
                HDOP = 1.0
            };

            SendPacket(packet);
            LogStatusIfDue(packet, force: false);
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunSerialAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!EnsureSerialOpen())
            {
                try
                {
                    await Task.Delay(500, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                continue;
            }

            try
            {
                System.Diagnostics.Debug.Assert(mvarSerial is not null);

                // Vaciar si se acumula basura (GPS desconectado/reconectado)
                if (mvarSerial.BytesToRead > 4096)
                {
                    mvarLogger.LogWarning("Buffer serie desbordado; purgando.");
                    mvarSerial.DiscardInBuffer();
                }

                bool updated = false;
                while (mvarSerial.BytesToRead > 0 && !stoppingToken.IsCancellationRequested)
                {
                    string? line;
                    try
                    {
                        line = mvarSerial.ReadLine();
                    }
                    catch (TimeoutException)
                    {
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // Algunos receptores envían checksum al final; el parser usa Split por coma y tolera el resto.
                    if (!line.StartsWith("$GP") && !line.StartsWith("$GN"))
                        continue;

                    lock (mvarLock)
                    {
                        if (NmeaParser.TryApply(line, mvarCurrent))
                        {
                            mvarNmeaAccepted++;
                            updated = true;
                        }
                    }
                }

                if (updated)
                {
                    GpsBroadcastPacket snapshot;
                    lock (mvarLock)
                    {
                        snapshot = Clone(mvarCurrent);
                    }

                    // Solo emitir si hay fix razonable
                    if (snapshot.FixQuality > 0 && snapshot.Latitude != 0 && snapshot.Longitude != 0)
                    {
                        SendPacket(snapshot);
                        LogStatusIfDue(snapshot, force: false);
                    }
                }
                else
                {
                    LogStatusIfDue(null, force: false);
                }

                // Pequeña pausa para no saturar CPU si el GPS emite despacio
                try
                {
                    await Task.Delay(50, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                mvarLogger.LogWarning("Error leyendo serie: {Message}", ex.Message);
                CloseSerial();
                mvarNextReconnectUtc = DateTime.UtcNow.AddSeconds(Math.Max(1, mvarOptions.ReconnectSeconds));
            }
        }
    }

    private bool EnsureSerialOpen()
    {
        if (mvarSerial is { IsOpen: true })
            return true;

        if (DateTime.UtcNow < mvarNextReconnectUtc)
            return false;

        mvarNextReconnectUtc = DateTime.UtcNow.AddSeconds(Math.Max(1, mvarOptions.ReconnectSeconds));

        try
        {
            CloseSerial();

            if (!SerialPort.GetPortNames().Contains(mvarOptions.Port, StringComparer.OrdinalIgnoreCase))
            {
                mvarLogger.LogWarning(
                    "Puerto {Port} no disponible. Disponibles: {Ports}",
                    mvarOptions.Port,
                    string.Join(", ", SerialPort.GetPortNames()) is { Length: > 0 } list ? list : "(ninguno)");
                return false;
            }

            mvarSerial = new SerialPort(
                mvarOptions.Port,
                mvarOptions.BaudRate,
                mvarOptions.ResolvedParity,
                mvarOptions.DataBits,
                mvarOptions.ResolvedStopBits)
            {
                Handshake = mvarOptions.ResolvedHandshake,
                NewLine = "\r\n",
                ReadTimeout = 500,
                WriteTimeout = 1000
            };
            mvarSerial.Open();
            mvarLogger.LogInformation("Puerto serie {Port} abierto.", mvarOptions.Port);
            return true;
        }
        catch (Exception ex)
        {
            mvarLogger.LogWarning("No se pudo abrir {Port}: {Message}", mvarOptions.Port, ex.Message);
            CloseSerial();
            return false;
        }
    }

    private void CloseSerial()
    {
        try
        {
            if (mvarSerial is { IsOpen: true })
                mvarSerial.Close();
        }
        catch
        {
            // ignore
        }

        mvarSerial?.Dispose();
        mvarSerial = null;
    }

    private void SendPacket(GpsBroadcastPacket packet)
    {
        if (mvarUdp is null || mvarEndPoint is null)
            return;

        try
        {
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(packet, mvarJsonOptions);
            mvarUdp.Send(payload, payload.Length, mvarEndPoint);
            mvarPacketsSent++;
        }
        catch (Exception ex)
        {
            mvarLogger.LogWarning("Error enviando UDP: {Message}", ex.Message);
        }
    }

    private void LogStatusIfDue(GpsBroadcastPacket? packet, bool force)
    {
        if (!force && DateTime.UtcNow - mvarLastStatusUtc < TimeSpan.FromSeconds(5))
            return;

        mvarLastStatusUtc = DateTime.UtcNow;

        if (packet is null)
        {
            mvarLogger.LogInformation(
                "Estado: NMEA ok={Nmea}  UDP enviados={Sent}  (esperando fix)",
                mvarNmeaAccepted,
                mvarPacketsSent);
            return;
        }

        mvarLogger.LogInformation(
            "GPS lat={Lat:F6} lon={Lon:F6}  {Speed:F1} km/h  curso={Course:F0}°  fix={Fix} sats={Sats}  | UDP={Sent}",
            packet.Latitude,
            packet.Longitude,
            packet.SpeedKmh,
            packet.Course,
            packet.FixQuality,
            packet.SatellitesUsed,
            mvarPacketsSent);
    }

    private static GpsBroadcastPacket Clone(GpsBroadcastPacket src) => new()
    {
        Latitude = src.Latitude,
        Longitude = src.Longitude,
        Time = src.Time,
        SpeedKnots = src.SpeedKnots,
        SpeedKmh = src.SpeedKmh,
        SpeedMs = src.SpeedMs,
        Course = src.Course,
        Altitude = src.Altitude,
        FixQuality = src.FixQuality,
        SatellitesUsed = src.SatellitesUsed,
        HDOP = src.HDOP
    };

    public override void Dispose()
    {
        CloseSerial();
        mvarUdp?.Dispose();
        mvarUdp = null;
        base.Dispose();
    }
}

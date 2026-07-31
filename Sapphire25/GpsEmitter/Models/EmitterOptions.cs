using System.IO.Ports;

namespace GpsEmitter.Models;

public sealed class EmitterOptions
{
    public const string SectionName = "GpsEmitter";

    /// <summary>Serial | Simulate</summary>
    public string Mode { get; set; } = "Serial";

    public string Port { get; set; } = "COM6";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public string Handshake { get; set; } = "None";

    public string BroadcastAddress { get; set; } = "255.255.255.255";
    public int BroadcastPort { get; set; } = 5005;

    public double SimulateLatitude { get; set; } = 39.57634918310896;
    public double SimulateLongitude { get; set; } = 2.6546805667305313;
    public double SimulateSpeedKmh { get; set; }
    public double SimulateCourse { get; set; }
    public int SimulateIntervalMs { get; set; } = 500;

    public int ReconnectSeconds { get; set; } = 5;

    public bool IsSimulate =>
        string.Equals(Mode, "Simulate", StringComparison.OrdinalIgnoreCase);

    public Parity ResolvedParity =>
        Enum.TryParse(Parity, ignoreCase: true, out Parity p) ? p : System.IO.Ports.Parity.None;

    public StopBits ResolvedStopBits =>
        Enum.TryParse(StopBits, ignoreCase: true, out StopBits s) ? s : System.IO.Ports.StopBits.One;

    public Handshake ResolvedHandshake =>
        Enum.TryParse(Handshake, ignoreCase: true, out Handshake h) ? h : System.IO.Ports.Handshake.None;
}

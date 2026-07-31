namespace GpsEmitter.Models;

/// <summary>
/// Contrato JSON UDP compartido con Tourmaline26.Services.GPSService.
/// Propiedades en camelCase vía JsonSerializerDefaults.Web.
/// </summary>
public sealed class GpsBroadcastPacket
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

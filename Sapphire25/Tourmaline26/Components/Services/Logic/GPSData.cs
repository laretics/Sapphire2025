using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace Tourmaline26.Components.Services.Logic
{
    public class GPSData
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime Time { get; set; }           // UTC
        public double SpeedKnots { get; set; }
        public double SpeedKmh { get; set; }
        public double SpeedMs { get; set; }
        public double Course { get; set; }           // Rumbo en grados (0 = Norte)
        public double Altitude { get; set; }         // metros sobre nivel del mar
        public int FixQuality { get; set; }          // 0=inválido, 1=GPS, 2=DGPS, 4=RTK Fix...
        public int SatellitesUsed { get; set; }
        public double HDOP { get; set; }             // menor = mejor precisión

        public bool IsValid => FixQuality > 0 && Latitude != 0 && Longitude != 0;
    }
}

namespace Tourmaline26.Logic
{
	public class GPSData
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

		public bool IsValid => FixQuality > 0 && Latitude != 0 && Longitude != 0;
	}
}

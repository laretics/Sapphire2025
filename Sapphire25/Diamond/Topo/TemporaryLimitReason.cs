namespace Diamond.Topo
{
	/// <summary>Causa de una limitación temporal de velocidad.</summary>
	public enum TemporaryLimitReason : byte
	{
		Works = 0,
		Geometry = 1,
		TracksideHazard = 2,
		Electrification = 3,
		Gauge = 4,
		Weather = 5,
		NaturalDisaster = 6,
		Other = 7
	}

	public static class TemporaryLimitReasonText
	{
		public static string Label(TemporaryLimitReason reason)
		{
			switch (reason)
			{
				case TemporaryLimitReason.Works:
					return "Obras";
				case TemporaryLimitReason.Geometry:
					return "Geometría";
				case TemporaryLimitReason.TracksideHazard:
					return "Peligro junto a la vía";
				case TemporaryLimitReason.Electrification:
					return "Electrificación";
				case TemporaryLimitReason.Gauge:
					return "Gálibo";
				case TemporaryLimitReason.Weather:
					return "Meteorología";
				case TemporaryLimitReason.NaturalDisaster:
					return "Catástrofe natural";
				case TemporaryLimitReason.Other:
					return "Otros";
				default:
					return "—";
			}
		}
	}
}

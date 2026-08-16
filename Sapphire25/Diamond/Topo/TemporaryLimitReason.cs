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
}

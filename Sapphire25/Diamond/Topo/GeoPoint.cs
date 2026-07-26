namespace Diamond.Topo
{
	/// <summary>
	/// Coordenada geográfica WGS84.
	/// </summary>
	public readonly struct GeoPoint
	{
		public GeoPoint(double latitude, double longitude)
		{
			Latitude = latitude;
			Longitude = longitude;
		}

		public double Latitude { get; }

		public double Longitude { get; }

		public override string ToString()
		{
			return $"({Latitude:F6}, {Longitude:F6})";
		}
	}
}

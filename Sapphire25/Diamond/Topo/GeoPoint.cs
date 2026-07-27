namespace Diamond.Topo
{
	/// <summary>
	/// Coordenada geográfica WGS84.
	/// </summary>
	public readonly struct GeoPoint
	{
		private readonly double mvarLatitude;
		private readonly double mvarLongitude;

		public GeoPoint(double latitude, double longitude)
		{
			mvarLatitude = latitude;
			mvarLongitude = longitude;
		}

		public double Latitude
		{
			get { return mvarLatitude; }
		}

		public double Longitude
		{
			get { return mvarLongitude; }
		}

		public override string ToString()
		{
			return $"({Latitude:F6}, {Longitude:F6})";
		}
	}
}

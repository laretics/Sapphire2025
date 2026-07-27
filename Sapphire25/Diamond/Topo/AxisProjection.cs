namespace Diamond.Topo
{
	/// <summary>
	/// Resultado de proyectar un punto geográfico sobre un <see cref="Axis"/>.
	/// </summary>
	public readonly struct AxisProjection
	{
		private readonly bool mvarSuccess;
		private readonly long mvarPK;
		private readonly double mvarDistanceMeters;
		private readonly double mvarLatitude;
		private readonly double mvarLongitude;

		public AxisProjection(bool success, long pk, double distanceMeters, double latitude, double longitude)
		{
			mvarSuccess = success;
			mvarPK = pk;
			mvarDistanceMeters = distanceMeters;
			mvarLatitude = latitude;
			mvarLongitude = longitude;
		}

		public static AxisProjection Fail(double distanceMeters)
		{
			return new AxisProjection(false, 0L, distanceMeters, 0.0, 0.0);
		}

		public bool Success
		{
			get { return mvarSuccess; }
		}

		public long PK
		{
			get { return mvarPK; }
		}

		/// <summary>
		/// Distancia mínima del punto de consulta al eje, en metros.
		/// </summary>
		public double DistanceMeters
		{
			get { return mvarDistanceMeters; }
		}

		/// <summary>
		/// Punto sobre el eje (proyección), si <see cref="Success"/> es true.
		/// </summary>
		public double Latitude
		{
			get { return mvarLatitude; }
		}

		public double Longitude
		{
			get { return mvarLongitude; }
		}
	}
}

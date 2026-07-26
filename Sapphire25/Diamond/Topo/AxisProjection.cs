namespace Diamond.Topo
{
	/// <summary>
	/// Resultado de proyectar un punto geográfico sobre un <see cref="Axis"/>.
	/// </summary>
	public readonly struct AxisProjection
	{
		public AxisProjection(bool success, long pk, double distanceMeters, double latitude, double longitude)
		{
			Success = success;
			PK = pk;
			DistanceMeters = distanceMeters;
			Latitude = latitude;
			Longitude = longitude;
		}

		public static AxisProjection Fail(double distanceMeters)
		{
			return new AxisProjection(false, 0L, distanceMeters, 0.0, 0.0);
		}

		public bool Success { get; }

		public long PK { get; }

		/// <summary>
		/// Distancia mínima del punto de consulta al eje, en metros.
		/// </summary>
		public double DistanceMeters { get; }

		/// <summary>
		/// Punto sobre el eje (proyección), si <see cref="Success"/> es true.
		/// </summary>
		public double Latitude { get; }

		public double Longitude { get; }
	}
}

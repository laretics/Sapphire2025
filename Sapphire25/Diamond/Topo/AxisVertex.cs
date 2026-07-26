namespace Diamond.Topo
{
	/// <summary>
	/// Vértice de la polilínea de un <see cref="Axis"/>.
	/// Puede ser libre (solo lat/lon), ancla de calibración (pk) y/o estar ligado a una <see cref="Station"/>.
	/// Tras <see cref="Axis.Rebuild"/>, todos los vértices tienen un PK efectivo.
	/// </summary>
	public sealed class AxisVertex
	{
		private double mvarLatitude;
		private double mvarLongitude;
		private long? mvarAnchorPk;
		private long mvarPK;
		private Station? mvarStation;

		public AxisVertex(double latitude, double longitude)
		{
			mvarLatitude = latitude;
			mvarLongitude = longitude;
			mvarAnchorPk = null;
			mvarPK = 0L;
			mvarStation = null;
		}

		public AxisVertex(double latitude, double longitude, long anchorPk)
		{
			mvarLatitude = latitude;
			mvarLongitude = longitude;
			mvarAnchorPk = anchorPk;
			mvarPK = anchorPk;
			mvarStation = null;
		}

		public double Latitude
		{
			get { return mvarLatitude; }
			set { mvarLatitude = value; }
		}

		public double Longitude
		{
			get { return mvarLongitude; }
			set { mvarLongitude = value; }
		}

		/// <summary>
		/// PK de calibración si el vértice es un hito/referencia; null si es solo forma.
		/// </summary>
		public long? AnchorPk
		{
			get { return mvarAnchorPk; }
			set
			{
				mvarAnchorPk = value;
				if (value.HasValue)
				{
					mvarPK = value.Value;
				}
			}
		}

		public bool IsAnchor
		{
			get { return mvarAnchorPk.HasValue; }
		}

		/// <summary>
		/// PK efectivo (ancla o precálculado). No mutar desde fuera de <see cref="Axis.Rebuild"/>.
		/// </summary>
		public long PK
		{
			get { return mvarPK; }
			internal set { mvarPK = value; }
		}

		/// <summary>
		/// Estación asociada a este vértice (si es una parada/hito con identidad).
		/// La misma instancia puede compartirse entre varios ejes.
		/// </summary>
		public Station? Station
		{
			get { return mvarStation; }
			set { mvarStation = value; }
		}

		public override string ToString()
		{
			if (mvarStation is not null)
			{
				return $"{mvarStation.Name} @ {Latitude:F6},{Longitude:F6} PK={mvarPK}";
			}

			if (IsAnchor)
			{
				return $"{Latitude:F6},{Longitude:F6} anchor PK={mvarPK}";
			}

			return $"{Latitude:F6},{Longitude:F6} PK={mvarPK}";
		}
	}
}

namespace Diamond.Topo
{
	/// <summary>
	/// Estación con identidad propia (nodo del grafo de red).
	/// Puede aparecer en uno o varios ejes mediante <see cref="StationOnAxis"/>.
	/// </summary>
	public sealed class Station
	{
		private string mvarId;
		private string mvarName;
		private string mvarAvr;
		private double? mvarLatitude;
		private double? mvarLongitude;

		public Station(string id)
		{
			mvarId = id ?? string.Empty;
			mvarName = string.Empty;
			mvarAvr = string.Empty;
			mvarLatitude = null;
			mvarLongitude = null;
		}

		/// <summary>
		/// Identificador estable (atributo XML id / station).
		/// </summary>
		public string Id
		{
			get { return mvarId; }
			set { mvarId = value ?? string.Empty; }
		}

		public string Name
		{
			get { return mvarName; }
			set { mvarName = value ?? string.Empty; }
		}

		/// <summary>
		/// Código corto / AVR.
		/// </summary>
		public string Avr
		{
			get { return mvarAvr; }
			set { mvarAvr = value ?? string.Empty; }
		}

		/// <summary>
		/// Coordenada canónica opcional (p. ej. edificio de viajeros). La geo en cada eje
		/// puede diferir y vive en la polilínea / incidencia.
		/// </summary>
		public double? Latitude
		{
			get { return mvarLatitude; }
			set { mvarLatitude = value; }
		}

		public double? Longitude
		{
			get { return mvarLongitude; }
			set { mvarLongitude = value; }
		}

		public override string ToString()
		{
			if (mvarAvr.Length > 0)
			{
				return $"{mvarName} ({mvarAvr}) [{mvarId}]";
			}

			return $"{mvarName} [{mvarId}]";
		}
	}
}

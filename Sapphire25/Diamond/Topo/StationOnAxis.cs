using Diamond.Basis;

namespace Diamond.Topo
{
	/// <summary>
	/// Incidencia de una <see cref="Station"/> sobre un <see cref="Axis"/> en un PK concreto.
	/// Una estación de enlace tiene varias incidencias (una por eje).
	/// </summary>
	public sealed class StationOnAxis : Punctual<long, LongAxis>
	{
		private readonly Station mvarStation;

		public StationOnAxis(Station station, long pk)
			: base(pk)
		{
			if (station is null)
			{
				throw new System.ArgumentNullException(nameof(station));
			}

			mvarStation = station;
		}

		public Station Station
		{
			get { return mvarStation; }
		}

		public override string ToString()
		{
			return $"{mvarStation.Name} @ {base.ToString()}";
		}
	}
}

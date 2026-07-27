using Diamond.Basis;

namespace Diamond.Topo
{
	/// <summary>
	/// Incidencia de una <see cref="Station"/> sobre un <see cref="RouteView"/> en un PK de ruta.
	/// </summary>
	public sealed class StationOnRoute : Punctual<long, LongAxis>
	{
		private readonly Station mvarStation;
		private readonly RouteLeg mvarLeg;
		private readonly long mvarAxisPk;

		public StationOnRoute(Station station, long routePk, RouteLeg leg, long axisPk)
			: base(routePk)
		{
			if (station is null)
			{
				throw new System.ArgumentNullException(nameof(station));
			}

			if (leg is null)
			{
				throw new System.ArgumentNullException(nameof(leg));
			}

			mvarStation = station;
			mvarLeg = leg;
			mvarAxisPk = axisPk;
		}

		public Station Station
		{
			get { return mvarStation; }
		}

		public RouteLeg Leg
		{
			get { return mvarLeg; }
		}

		/// <summary>
		/// PK del eje físico en el que está la estación dentro de este tramo.
		/// </summary>
		public long AxisPk
		{
			get { return mvarAxisPk; }
		}

		/// <summary>
		/// Proyección al eje físico (misma estación, PK de eje).
		/// </summary>
		public StationOnAxis AsStationOnAxis()
		{
			return new StationOnAxis(mvarStation, mvarAxisPk);
		}

		public override string ToString()
		{
			return mvarStation.Name + " @R" + base.ToString() + " (" + mvarLeg.Axis.Id + ")";
		}
	}
}

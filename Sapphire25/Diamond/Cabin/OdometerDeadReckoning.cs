namespace Diamond.Cabin
{
	/// <summary>
	/// Posición de ruta por incremento de odómetro mientras no hay GPS.
	/// El origen se arma al perder satélite o al resincronizar en una estación.
	/// </summary>
	public sealed class OdometerDeadReckoning
	{
		public bool Armed { get; private set; }

		public int OriginOdometer { get; private set; }

		public long OriginPk { get; private set; }

		public void Arm(int odometerMeters, long routePk)
		{
			OriginOdometer = odometerMeters;
			OriginPk = routePk;
			Armed = true;
		}

		public void Disarm()
		{
			Armed = false;
		}

		/// <summary>
		/// Metros recorridos desde el origen (odómetro no decrece; un wrap se ignora).
		/// </summary>
		public long TraveledMeters(int odometerMeters)
		{
			long delta = (long)odometerMeters - OriginOdometer;
			return delta < 0 ? 0 : delta;
		}

		public long Project(int odometerMeters, bool pkIncreasing)
		{
			long delta = TraveledMeters(odometerMeters);
			return pkIncreasing ? OriginPk + delta : OriginPk - delta;
		}

		public void Resync(int odometerMeters, long routePk)
		{
			Arm(odometerMeters, routePk);
		}
	}
}

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Tipo de marca en la columna Dependencia.
	/// </summary>
	public enum CirculationSheetMarkKind
	{
		/// <summary>Estación principal: blanco sobre negro (solo el texto).</summary>
		PrincipalStation = 0,

		/// <summary>Apeadero u otra dependencia menor: negro sobre blanco.</summary>
		Halt = 1,

		/// <summary>Frontera de limitación de velocidad en plena vía (sin estación).</summary>
		SpeedLimitChange = 2
	}

	/// <summary>
	/// Una frontera de la ficha: punto de PK (estación, apeadero o cambio de V).
	/// Los datos de tramo (VMx, vía, tiempo concedido) describen el segmento hacia la
	/// siguiente frontera (columnas desfasadas en el dibujo).
	/// </summary>
	public sealed class CirculationSheetFrontier
	{
		private readonly long mvarRoutePk;
		private readonly string mvarStationKm;
		private readonly string mvarDependencyName;
		private readonly CirculationSheetMarkKind mvarMarkKind;
		private readonly bool mvarIsOrigin;
		private readonly bool mvarIsDestination;
		private readonly bool mvarIsCommercialStop;
		private readonly TimeSpan mvarDwell;
		private readonly TimeSpan? mvarArrival;
		private readonly TimeSpan? mvarDeparture;
		private readonly int? mvarOutgoingTrackCount;
		private readonly int? mvarOutgoingVmaxKmh;
		private readonly TimeSpan? mvarGrantedToNext;
		/// <summary>Números de trenes con los que se cruza cerca de este PK.</summary>
		private readonly string mvarCrossingTrains;

		public CirculationSheetFrontier(
			long routePk,
			string stationKm,
			string dependencyName,
			CirculationSheetMarkKind markKind,
			bool isOrigin,
			bool isDestination,
			bool isCommercialStop,
			TimeSpan dwell,
			TimeSpan? arrival,
			TimeSpan? departure,
			int? outgoingTrackCount,
			int? outgoingVmaxKmh,
			TimeSpan? grantedToNext,
			string? crossingTrains = null)
		{
			mvarRoutePk = routePk;
			mvarStationKm = stationKm ?? string.Empty;
			mvarDependencyName = dependencyName ?? string.Empty;
			mvarMarkKind = markKind;
			mvarIsOrigin = isOrigin;
			mvarIsDestination = isDestination;
			mvarIsCommercialStop = isCommercialStop;
			mvarDwell = dwell < TimeSpan.Zero ? TimeSpan.Zero : dwell;
			mvarArrival = arrival;
			mvarDeparture = departure;
			mvarOutgoingTrackCount = outgoingTrackCount;
			mvarOutgoingVmaxKmh = outgoingVmaxKmh;
			mvarGrantedToNext = grantedToNext;
			mvarCrossingTrains = crossingTrains ?? string.Empty;
		}

		public CirculationSheetFrontier WithCrossingTrains(string? crossingTrains)
		{
			return new CirculationSheetFrontier(
				mvarRoutePk,
				mvarStationKm,
				mvarDependencyName,
				mvarMarkKind,
				mvarIsOrigin,
				mvarIsDestination,
				mvarIsCommercialStop,
				mvarDwell,
				mvarArrival,
				mvarDeparture,
				mvarOutgoingTrackCount,
				mvarOutgoingVmaxKmh,
				mvarGrantedToNext,
				crossingTrains);
		}

		public long RoutePk
		{
			get { return mvarRoutePk; }
		}

		public string StationKm
		{
			get { return mvarStationKm; }
		}

		public string DependencyName
		{
			get { return mvarDependencyName; }
		}

		public CirculationSheetMarkKind MarkKind
		{
			get { return mvarMarkKind; }
		}

		public bool IsOrigin
		{
			get { return mvarIsOrigin; }
		}

		public bool IsDestination
		{
			get { return mvarIsDestination; }
		}

		public bool IsCommercialStop
		{
			get { return mvarIsCommercialStop; }
		}

		public TimeSpan Dwell
		{
			get { return mvarDwell; }
		}

		public TimeSpan? Arrival
		{
			get { return mvarArrival; }
		}

		public TimeSpan? Departure
		{
			get { return mvarDeparture; }
		}

		public bool HasOutgoingSegment
		{
			get { return mvarOutgoingTrackCount.HasValue || mvarOutgoingVmaxKmh.HasValue || mvarGrantedToNext.HasValue; }
		}

		public int? OutgoingTrackCount
		{
			get { return mvarOutgoingTrackCount; }
		}

		public bool OutgoingIsDoubleTrack
		{
			get { return mvarOutgoingTrackCount.HasValue && mvarOutgoingTrackCount.Value >= 2; }
		}

		public int? OutgoingVmaxKmh
		{
			get { return mvarOutgoingVmaxKmh; }
		}

		public TimeSpan? GrantedToNext
		{
			get { return mvarGrantedToNext; }
		}

		/// <summary>Texto de cruces (números de tren), vacío si no hay.</summary>
		public string CrossingTrains
		{
			get { return mvarCrossingTrains; }
		}
	}
}

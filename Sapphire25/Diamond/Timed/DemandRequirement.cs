using System;
using Diamond.Topo;

namespace Diamond.Timed
{
	/// <summary>
	/// Requisito de demanda compilado desde el mini-DSL (o creado en código).
	/// Orden e identidad estables → compilación determinista.
	/// </summary>
	public sealed class DemandRequirement
	{
		private string mvarId;
		private StationRef mvarFrom;
		private StationRef mvarTo;
		private FrequencySpec mvarFrequency;
		private DemandDirection mvarDirection;
		private TimeOnly? mvarWindowStart;
		private TimeOnly? mvarWindowEnd;
		private string mvarFleetId;
		private Station? mvarFromStation;
		private Station? mvarToStation;
		private int mvarSourceLine;
		private readonly StopPattern mvarStops;

		public DemandRequirement(
			string id,
			StationRef from,
			StationRef to,
			FrequencySpec frequency,
			DemandDirection direction,
			TimeOnly? windowStart,
			TimeOnly? windowEnd,
			string? fleetId,
			int sourceLine,
			StopPattern? stops = null)
		{
			if (from is null)
			{
				throw new ArgumentNullException(nameof(from));
			}

			if (to is null)
			{
				throw new ArgumentNullException(nameof(to));
			}

			if (frequency is null)
			{
				throw new ArgumentNullException(nameof(frequency));
			}

			mvarId = id ?? string.Empty;
			mvarFrom = from;
			mvarTo = to;
			mvarFrequency = frequency;
			mvarDirection = direction;
			mvarWindowStart = windowStart;
			mvarWindowEnd = windowEnd;
			mvarFleetId = fleetId ?? string.Empty;
			mvarFromStation = null;
			mvarToStation = null;
			mvarSourceLine = sourceLine;
			mvarStops = stops ?? new StopPattern();
		}

		public string Id
		{
			get { return mvarId; }
			set { mvarId = value ?? string.Empty; }
		}

		public StationRef From
		{
			get { return mvarFrom; }
		}

		public StationRef To
		{
			get { return mvarTo; }
		}

		public FrequencySpec Frequency
		{
			get { return mvarFrequency; }
		}

		public DemandDirection Direction
		{
			get { return mvarDirection; }
		}

		public TimeOnly? WindowStart
		{
			get { return mvarWindowStart; }
		}

		public TimeOnly? WindowEnd
		{
			get { return mvarWindowEnd; }
		}

		public string FleetId
		{
			get { return mvarFleetId; }
			set { mvarFleetId = value ?? string.Empty; }
		}

		public Station? FromStation
		{
			get { return mvarFromStation; }
			internal set { mvarFromStation = value; }
		}

		public Station? ToStation
		{
			get { return mvarToStation; }
			internal set { mvarToStation = value; }
		}

		public int SourceLine
		{
			get { return mvarSourceLine; }
		}

		public StopPattern Stops
		{
			get { return mvarStops; }
		}

		public bool IsResolved
		{
			get { return mvarFromStation is not null && mvarToStation is not null; }
		}

		public override string ToString()
		{
			return mvarId + ": " + mvarFrom.Text + " -> " + mvarTo.Text + " " + mvarFrequency;
		}
	}
}

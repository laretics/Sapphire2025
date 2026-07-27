using System;

namespace Diamond.Timed
{
	/// <summary>
	/// Cadencia de un requisito: trenes por hora o intervalo fijo en minutos.
	/// </summary>
	public sealed class FrequencySpec
	{
		private readonly int? mvarTrainsPerHour;
		private readonly int? mvarIntervalMinutes;

		private FrequencySpec(int? trainsPerHour, int? intervalMinutes)
		{
			mvarTrainsPerHour = trainsPerHour;
			mvarIntervalMinutes = intervalMinutes;
		}

		public static FrequencySpec PerHour(int trainsPerHour)
		{
			if (trainsPerHour <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(trainsPerHour));
			}

			return new FrequencySpec(trainsPerHour, null);
		}

		public static FrequencySpec EveryMinutes(int intervalMinutes)
		{
			if (intervalMinutes <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(intervalMinutes));
			}

			return new FrequencySpec(null, intervalMinutes);
		}

		public int? TrainsPerHour
		{
			get { return mvarTrainsPerHour; }
		}

		public int? IntervalMinutes
		{
			get { return mvarIntervalMinutes; }
		}

		/// <summary>
		/// Equivalente en trenes/hora (p. ej. every 30 min → 2.0).
		/// </summary>
		public double TrainsPerHourValue
		{
			get
			{
				if (mvarTrainsPerHour.HasValue)
				{
					return mvarTrainsPerHour.Value;
				}

				return 60.0 / mvarIntervalMinutes!.Value;
			}
		}

		public override string ToString()
		{
			if (mvarTrainsPerHour.HasValue)
			{
				return mvarTrainsPerHour.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/h";
			}

			return "every " + mvarIntervalMinutes!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + " min";
		}
	}
}

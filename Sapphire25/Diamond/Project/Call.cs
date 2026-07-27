using System;

namespace Diamond.Project
{
	/// <summary>
	/// Parada / paso de una circulación o de una asimilación (tiempos relativos o absolutos).
	/// </summary>
	public sealed class Call
	{
		private readonly StationInfo mvarStation;
		private readonly long mvarPk;
		private readonly TimeSpan mvarArrivalOffset;
		private readonly TimeSpan mvarDepartureOffset;
		private readonly TimeSpan mvarDwell;
		private readonly bool mvarIsOrigin;
		private readonly bool mvarIsDestination;
		private readonly bool mvarCommercialStop;

		public Call(
			StationInfo station,
			long pk,
			TimeSpan arrivalOffset,
			TimeSpan departureOffset,
			TimeSpan dwell,
			bool isOrigin,
			bool isDestination,
			bool commercialStop)
		{
			if (station is null)
			{
				throw new ArgumentNullException(nameof(station));
			}

			if (departureOffset < arrivalOffset)
			{
				throw new ArgumentException("La salida no puede ser anterior a la llegada.", nameof(departureOffset));
			}

			mvarStation = station;
			mvarPk = pk;
			mvarArrivalOffset = arrivalOffset;
			mvarDepartureOffset = departureOffset;
			mvarDwell = dwell < TimeSpan.Zero ? TimeSpan.Zero : dwell;
			mvarIsOrigin = isOrigin;
			mvarIsDestination = isDestination;
			mvarCommercialStop = commercialStop;
		}

		public StationInfo Station
		{
			get { return mvarStation; }
		}

		/// <summary>PK de ruta de la vista en la que se calculó la marcha.</summary>
		public long Pk
		{
			get { return mvarPk; }
		}

		/// <summary>Tiempo desde la salida del origen hasta la llegada a esta parada.</summary>
		public TimeSpan ArrivalOffset
		{
			get { return mvarArrivalOffset; }
		}

		/// <summary>Tiempo desde la salida del origen hasta la salida de esta parada.</summary>
		public TimeSpan DepartureOffset
		{
			get { return mvarDepartureOffset; }
		}

		public TimeSpan Dwell
		{
			get { return mvarDwell; }
		}

		public bool IsOrigin
		{
			get { return mvarIsOrigin; }
		}

		public bool IsDestination
		{
			get { return mvarIsDestination; }
		}

		/// <summary>True si hay parada comercial (dwell &gt; 0 o extremo de trayecto).</summary>
		public bool CommercialStop
		{
			get { return mvarCommercialStop; }
		}

		public TimeSpan AbsoluteArrival(TimeSpan trainDeparture)
		{
			return trainDeparture + mvarArrivalOffset;
		}

		public TimeSpan AbsoluteDeparture(TimeSpan trainDeparture)
		{
			return trainDeparture + mvarDepartureOffset;
		}

		public override string ToString()
		{
			return mvarStation.DisplayCode
				+ " +" + FormatOffset(mvarArrivalOffset)
				+ (mvarDwell > TimeSpan.Zero ? " d=" + ((int)mvarDwell.TotalSeconds).ToString() + "s" : string.Empty);
		}

		private static string FormatOffset(TimeSpan ts)
		{
			int totalMinutes = (int)Math.Floor(ts.TotalMinutes);
			int hours = totalMinutes / 60;
			int minutes = totalMinutes % 60;
			int seconds = ts.Seconds;
			if (seconds != 0)
			{
				return hours.ToString("00") + ":" + minutes.ToString("00") + ":" + seconds.ToString("00");
			}

			return hours.ToString("00") + ":" + minutes.ToString("00");
		}
	}
}

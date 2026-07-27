using System;
using System.Collections.Generic;
using System.Globalization;

namespace Diamond.Project
{
	/// <summary>
	/// Tren concreto del proyecto: una salida en el tiempo que clona una <see cref="Asimilation"/>.
	/// Base para documentación, turnos de material y tracción.
	/// </summary>
	public sealed class Circulation
	{
		private readonly string mvarId;
		private readonly string mvarTechnicalId;
		private readonly string mvarDemandId;
		private readonly string mvarServiceNumber;
		private readonly TimeSpan mvarDeparture;
		private readonly string mvarColor;
		private readonly Asimilation mvarAsimilation;
		private readonly List<TimedCall> mcolCalls;

		public Circulation(
			string id,
			string technicalId,
			string demandId,
			string serviceNumber,
			TimeSpan departure,
			string color,
			Asimilation asimilation)
		{
			if (asimilation is null)
			{
				throw new ArgumentNullException(nameof(asimilation));
			}

			mvarId = id ?? string.Empty;
			mvarTechnicalId = technicalId ?? string.Empty;
			mvarDemandId = demandId ?? string.Empty;
			mvarServiceNumber = serviceNumber ?? string.Empty;
			mvarDeparture = departure;
			mvarColor = color ?? string.Empty;
			mvarAsimilation = asimilation;
			mcolCalls = new List<TimedCall>();

			int i = 0;
			while (i < asimilation.Calls.Count)
			{
				Call rel = asimilation.Calls[i];
				mcolCalls.Add(new TimedCall(
					rel,
					departure + rel.ArrivalOffset,
					departure + rel.DepartureOffset));
				i++;
			}

			asimilation.AttachCirculation(this);
		}

		/// <summary>Identificador de presentación (normalmente el número de tren).</summary>
		public string Id
		{
			get { return mvarId; }
		}

		public string TechnicalId
		{
			get { return mvarTechnicalId; }
		}

		public string DemandId
		{
			get { return mvarDemandId; }
		}

		public string ServiceNumber
		{
			get { return mvarServiceNumber; }
		}

		public bool HasServiceNumber
		{
			get { return mvarServiceNumber.Length > 0; }
		}

		public TimeSpan Departure
		{
			get { return mvarDeparture; }
		}

		public TimeSpan Arrival
		{
			get { return mvarDeparture + mvarAsimilation.TotalTime; }
		}

		public string Color
		{
			get { return mvarColor; }
		}

		public bool HasColor
		{
			get { return mvarColor.Length > 0; }
		}

		public Asimilation Asimilation
		{
			get { return mvarAsimilation; }
		}

		public StationInfo Origin
		{
			get { return mvarAsimilation.Origin; }
		}

		public StationInfo Destination
		{
			get { return mvarAsimilation.Destination; }
		}

		/// <summary>Horario absoluto por parada (llegada / salida).</summary>
		public IReadOnlyList<TimedCall> Calls
		{
			get { return mcolCalls; }
		}

		public override string ToString()
		{
			string num = mvarServiceNumber.Length > 0
				? mvarServiceNumber
				: mvarId;
			return num + " " + Origin.DisplayCode + "→" + Destination.DisplayCode
				+ " dep " + FormatClock(mvarDeparture);
		}

		private static string FormatClock(TimeSpan ts)
		{
			int hours = (int)ts.TotalHours;
			int minutes = ts.Minutes;
			return hours.ToString("00", CultureInfo.InvariantCulture)
				+ ":"
				+ minutes.ToString("00", CultureInfo.InvariantCulture);
		}
	}

	/// <summary>
	/// Llamada de una circulación con horarios absolutos del día.
	/// </summary>
	public sealed class TimedCall
	{
		private readonly Call mvarTemplate;
		private readonly TimeSpan mvarArrival;
		private readonly TimeSpan mvarDeparture;

		public TimedCall(Call template, TimeSpan arrival, TimeSpan departure)
		{
			if (template is null)
			{
				throw new ArgumentNullException(nameof(template));
			}

			mvarTemplate = template;
			mvarArrival = arrival;
			mvarDeparture = departure;
		}

		public Call Template
		{
			get { return mvarTemplate; }
		}

		public StationInfo Station
		{
			get { return mvarTemplate.Station; }
		}

		public long Pk
		{
			get { return mvarTemplate.Pk; }
		}

		public TimeSpan Arrival
		{
			get { return mvarArrival; }
		}

		public TimeSpan Departure
		{
			get { return mvarDeparture; }
		}

		public TimeSpan Dwell
		{
			get { return mvarTemplate.Dwell; }
		}

		public bool IsOrigin
		{
			get { return mvarTemplate.IsOrigin; }
		}

		public bool IsDestination
		{
			get { return mvarTemplate.IsDestination; }
		}

		public bool CommercialStop
		{
			get { return mvarTemplate.CommercialStop; }
		}
	}
}

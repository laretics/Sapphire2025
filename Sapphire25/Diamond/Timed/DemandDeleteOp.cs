using System;

namespace Diamond.Timed
{
	/// <summary>
	/// Directiva de post-proceso: elimina circulaciones ya planificadas en una franja horaria.
	/// Se aplica en el orden del script (tras los <c>require</c> anteriores y antes de los siguientes).
	/// </summary>
	public sealed class DemandDeleteOp
	{
		private readonly TimeOnly mvarWindowStart;
		private readonly TimeOnly mvarWindowEnd;
		private readonly bool mvarAll;
		private ServiceDays mvarServiceDays;
		private readonly int mvarSourceLine;
		private readonly int mvarScriptOrder;

		public DemandDeleteOp(
			TimeOnly windowStart,
			TimeOnly windowEnd,
			bool all,
			int sourceLine,
			int scriptOrder,
			ServiceDays? serviceDays = null)
		{
			if (windowEnd <= windowStart)
			{
				throw new ArgumentException("La franja de delete debe tener fin posterior al inicio.", nameof(windowEnd));
			}

			mvarWindowStart = windowStart;
			mvarWindowEnd = windowEnd;
			mvarAll = all;
			mvarSourceLine = sourceLine;
			mvarScriptOrder = scriptOrder;
			mvarServiceDays = serviceDays ?? ServiceDays.All;
		}

		/// <summary>Inicio de la franja (inclusive).</summary>
		public TimeOnly WindowStart
		{
			get { return mvarWindowStart; }
		}

		/// <summary>Fin de la franja (exclusive para solapes; la salida en el fin no se borra por defecto).</summary>
		public TimeOnly WindowEnd
		{
			get { return mvarWindowEnd; }
		}

		/// <summary>
		/// Si es false: solo salidas en la franja.
		/// Si es true: también circulaciones cuyo trayecto solapa la franja en cualquier tramo.
		/// </summary>
		public bool All
		{
			get { return mvarAll; }
		}

		public ServiceDays ServiceDays
		{
			get { return mvarServiceDays; }
			set { mvarServiceDays = value ?? ServiceDays.All; }
		}

		public int SourceLine
		{
			get { return mvarSourceLine; }
		}

		/// <summary>Orden global en el script (mezclado con los require).</summary>
		public int ScriptOrder
		{
			get { return mvarScriptOrder; }
		}

		public bool AppliesOn(DayOfWeek dayOfWeek)
		{
			return mvarServiceDays.AppliesOn(dayOfWeek);
		}

		public TimeSpan WindowStartTime
		{
			get { return mvarWindowStart.ToTimeSpan(); }
		}

		public TimeSpan WindowEndTime
		{
			get { return mvarWindowEnd.ToTimeSpan(); }
		}

		/// <summary>
		/// Criterio de borrado sobre una circulación ya planificada.
		/// Franja semiabierta <c>[start, end)</c>.
		/// </summary>
		public bool Matches(Circulation circulation)
		{
			if (circulation is null)
			{
				return false;
			}

			TimeSpan start = WindowStartTime;
			TimeSpan end = WindowEndTime;
			TimeSpan dep = circulation.Departure;

			// Sin all: solo salida en la franja.
			if (!mvarAll)
			{
				return dep >= start && dep < end;
			}

			// all: solape de [dep, arr) con [start, end)
			TimeSpan arr = circulation.Arrival;
			return dep < end && arr > start;
		}

		public override string ToString()
		{
			string range = mvarWindowStart.ToString("HH:mm") + "-" + mvarWindowEnd.ToString("HH:mm");
			return mvarAll ? "delete " + range + " all" : "delete " + range;
		}
	}
}

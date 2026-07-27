using System;

namespace Diamond.Timed
{
	/// <summary>
	/// Ocupación de un cantón por una circulación, como rectángulo en el plano tiempo–espacio:
	/// horizontal = tiempo [TimeEnter, TimeExit), vertical = PK [PkStart, PkEnd).
	/// Dos trenes son compatibles en acantonamiento si sus rectángulos no se superponen
	/// (modelo conservador: la trayectoria real es una polilínea interior al rectángulo).
	/// </summary>
	public sealed class CantonOccupationRect
	{
		private readonly string mvarCirculationId;
		private readonly string mvarAxisId;
		private readonly long mvarPkStart;
		private readonly long mvarPkEnd;
		private readonly TimeSpan mvarTimeEnter;
		private readonly TimeSpan mvarTimeExit;

		public CantonOccupationRect(
			string circulationId,
			string axisId,
			long pkStart,
			long pkEnd,
			TimeSpan timeEnter,
			TimeSpan timeExit)
		{
			if (pkEnd < pkStart)
			{
				long swap = pkStart;
				pkStart = pkEnd;
				pkEnd = swap;
			}

			if (timeExit < timeEnter)
			{
				TimeSpan t = timeEnter;
				timeEnter = timeExit;
				timeExit = t;
			}

			mvarCirculationId = circulationId ?? string.Empty;
			mvarAxisId = axisId ?? string.Empty;
			mvarPkStart = pkStart;
			mvarPkEnd = pkEnd;
			mvarTimeEnter = timeEnter;
			mvarTimeExit = timeExit;
		}

		public string CirculationId
		{
			get { return mvarCirculationId; }
		}

		public string AxisId
		{
			get { return mvarAxisId; }
		}

		/// <summary>
		/// Extremo espacial inferior del cantón (metros de PK).
		/// </summary>
		public long PkStart
		{
			get { return mvarPkStart; }
		}

		/// <summary>
		/// Extremo espacial superior exclusivo del cantón.
		/// </summary>
		public long PkEnd
		{
			get { return mvarPkEnd; }
		}

		/// <summary>
		/// Instante absoluto de entrada al cantón.
		/// </summary>
		public TimeSpan TimeEnter
		{
			get { return mvarTimeEnter; }
		}

		/// <summary>
		/// Instante absoluto de salida del cantón.
		/// </summary>
		public TimeSpan TimeExit
		{
			get { return mvarTimeExit; }
		}

		public double DurationSeconds
		{
			get { return (mvarTimeExit - mvarTimeEnter).TotalSeconds; }
		}

		/// <summary>
		/// True si los rectángulos se superponen en tiempo y en espacio (mismo eje).
		/// Intervalos semiabiertos [t0,t1) × [pk0,pkf).
		/// </summary>
		public bool Overlaps(CantonOccupationRect other)
		{
			if (other is null)
			{
				return false;
			}

			if (!string.Equals(mvarAxisId, other.mvarAxisId, StringComparison.Ordinal))
			{
				return false;
			}

			bool timeOverlap = mvarTimeEnter < other.mvarTimeExit && other.mvarTimeEnter < mvarTimeExit;
			bool spaceOverlap = mvarPkStart < other.mvarPkEnd && other.mvarPkStart < mvarPkEnd;
			return timeOverlap && spaceOverlap;
		}

		public override string ToString()
		{
			return mvarCirculationId
				+ " [" + mvarPkStart + "," + mvarPkEnd + ") × ["
				+ mvarTimeEnter + "," + mvarTimeExit + ")";
		}
	}
}

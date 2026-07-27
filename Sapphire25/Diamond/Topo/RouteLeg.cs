using System;

namespace Diamond.Topo
{
	/// <summary>
	/// Tramo de un <see cref="RouteView"/> sobre un eje físico.
	/// Mapea un intervalo de PK de ruta (continuo a lo largo de la vista) a un intervalo de PK del eje.
	/// </summary>
	public sealed class RouteLeg
	{
		private readonly Axis mvarAxis;
		private readonly long mvarAxisFromPk;
		private readonly long mvarAxisToPk;
		private readonly long mvarRoutePk0;
		private readonly long mvarLength;
		private readonly bool mvarAxisPkIncreasing;

		public RouteLeg(Axis axis, long axisFromPk, long axisToPk, long routePk0)
		{
			if (axis is null)
			{
				throw new ArgumentNullException(nameof(axis));
			}

			if (axisFromPk == axisToPk)
			{
				throw new ArgumentException("El tramo del eje debe tener longitud no nula.", nameof(axisToPk));
			}

			if (routePk0 < 0L)
			{
				throw new ArgumentOutOfRangeException(nameof(routePk0));
			}

			mvarAxis = axis;
			mvarAxisFromPk = axisFromPk;
			mvarAxisToPk = axisToPk;
			mvarRoutePk0 = routePk0;
			mvarAxisPkIncreasing = axisToPk > axisFromPk;
			long delta = axisToPk - axisFromPk;
			if (delta < 0L)
			{
				delta = -delta;
			}

			mvarLength = delta;
		}

		public Axis Axis
		{
			get { return mvarAxis; }
		}

		/// <summary>
		/// PK del eje al inicio del tramo (en el sentido de la ruta).
		/// </summary>
		public long AxisFromPk
		{
			get { return mvarAxisFromPk; }
		}

		/// <summary>
		/// PK del eje al final del tramo (en el sentido de la ruta).
		/// </summary>
		public long AxisToPk
		{
			get { return mvarAxisToPk; }
		}

		/// <summary>
		/// PK de ruta al inicio de este tramo.
		/// </summary>
		public long RoutePk0
		{
			get { return mvarRoutePk0; }
		}

		/// <summary>
		/// Longitud del tramo en metros (siempre ≥ 0).
		/// </summary>
		public long Length
		{
			get { return mvarLength; }
		}

		/// <summary>
		/// PK de ruta al final de este tramo (exclusivo en solapes; inclusivo como extremo).
		/// </summary>
		public long RoutePkEnd
		{
			get { return mvarRoutePk0 + mvarLength; }
		}

		/// <summary>
		/// True si el PK del eje crece al avanzar por la ruta.
		/// </summary>
		public bool AxisPkIncreasing
		{
			get { return mvarAxisPkIncreasing; }
		}

		/// <summary>
		/// True si el PK de ruta cae en este tramo (el final de un tramo intermedio se cede al siguiente).
		/// </summary>
		public bool ContainsRoutePk(long routePk, bool includeEnd)
		{
			if (routePk < mvarRoutePk0)
			{
				return false;
			}

			if (includeEnd)
			{
				return routePk <= RoutePkEnd;
			}

			return routePk < RoutePkEnd;
		}

		/// <summary>
		/// True si el PK de eje cae en el intervalo físico de este tramo.
		/// </summary>
		public bool ContainsAxisPk(long axisPk)
		{
			long min = mvarAxisFromPk < mvarAxisToPk ? mvarAxisFromPk : mvarAxisToPk;
			long max = mvarAxisFromPk > mvarAxisToPk ? mvarAxisFromPk : mvarAxisToPk;
			return axisPk >= min && axisPk <= max;
		}

		public long AxisPkFromRoutePk(long routePk)
		{
			long offset = routePk - mvarRoutePk0;
			if (offset < 0L)
			{
				offset = 0L;
			}

			if (offset > mvarLength)
			{
				offset = mvarLength;
			}

			if (mvarAxisPkIncreasing)
			{
				return mvarAxisFromPk + offset;
			}

			return mvarAxisFromPk - offset;
		}

		public long RoutePkFromAxisPk(long axisPk)
		{
			long offset;
			if (mvarAxisPkIncreasing)
			{
				offset = axisPk - mvarAxisFromPk;
			}
			else
			{
				offset = mvarAxisFromPk - axisPk;
			}

			if (offset < 0L)
			{
				offset = 0L;
			}

			if (offset > mvarLength)
			{
				offset = mvarLength;
			}

			return mvarRoutePk0 + offset;
		}

		public override string ToString()
		{
			return mvarAxis.Id
				+ " [" + mvarAxisFromPk + "→" + mvarAxisToPk + "]"
				+ " @R" + mvarRoutePk0;
		}
	}
}

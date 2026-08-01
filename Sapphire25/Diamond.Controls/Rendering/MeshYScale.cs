using System;
using System.Collections.Generic;
using Diamond.Basis;
using Diamond.Topo;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Mapeo PK de ruta ↔ coordenada Y del plot.
	/// En modo <see cref="MeshYScaleMode.SteppedSingular"/> usa frontiers de
	/// <see cref="VectorFlex{T,TAxis}.Frontiers"/> de las capas de velocidad
	/// más estaciones/apeaderos de la vista, colocados equidistantes.
	/// </summary>
	public sealed class MeshYScale
	{
		private readonly MeshYScaleMode mvarMode;
		private readonly long mvarPkMin;
		private readonly long mvarPkMax;
		/// <summary>Puntos de ruptura ordenados (modo escalonado); al menos 2.</summary>
		private readonly long[] mcolBreaks;

		private MeshYScale(MeshYScaleMode mode, long pkMin, long pkMax, long[] breaks)
		{
			mvarMode = mode;
			mvarPkMin = pkMin;
			mvarPkMax = pkMax;
			mcolBreaks = breaks;
		}

		public MeshYScaleMode Mode
		{
			get { return mvarMode; }
		}

		public long PkMin
		{
			get { return mvarPkMin; }
		}

		public long PkMax
		{
			get { return mvarPkMax; }
		}

		/// <summary>Puntos singulares de la escala (vacío en modo lineal).</summary>
		public IReadOnlyList<long> Breaks
		{
			get { return mcolBreaks; }
		}

		public static MeshYScale Create(MeshYScaleMode mode, RouteView? view, long pkMin, long pkMax)
		{
			if (pkMax <= pkMin)
			{
				pkMax = pkMin + 1;
			}

			if (mode == MeshYScaleMode.LinearPk || view is null)
			{
				return new MeshYScale(MeshYScaleMode.LinearPk, pkMin, pkMax, Array.Empty<long>());
			}

			long[] breaks = BuildSingularBreaks(view, pkMin, pkMax);
			return new MeshYScale(MeshYScaleMode.SteppedSingular, pkMin, pkMax, breaks);
		}

		/// <summary>
		/// Y de pantalla: PK creciente hacia abajo del eje de datos → Y menor en el plot
		/// (PK alto arriba, como el diagrama clásico).
		/// </summary>
		public double PkToY(long pk, double plotTop, double plotH)
		{
			double u = PkToNormalized(pk);
			return plotTop + (1.0 - u) * plotH;
		}

		public long YToPk(double y, double plotTop, double plotH)
		{
			if (plotH < 1e-9)
			{
				return mvarPkMin;
			}

			double u = 1.0 - (y - plotTop) / plotH;
			if (u < 0.0)
			{
				u = 0.0;
			}

			if (u > 1.0)
			{
				u = 1.0;
			}

			return NormalizedToPk(u);
		}

		/// <summary>0 = pkMin (borde de datos), 1 = pkMax.</summary>
		public double PkToNormalized(long pk)
		{
			if (mvarMode == MeshYScaleMode.LinearPk || mcolBreaks.Length < 2)
			{
				double den = mvarPkMax - mvarPkMin;
				if (den < 1.0)
				{
					den = 1.0;
				}

				double u = (pk - mvarPkMin) / den;
				if (u < 0.0)
				{
					return 0.0;
				}

				if (u > 1.0)
				{
					return 1.0;
				}

				return u;
			}

			// Escalonado: índice continuo entre breaks, luego normalizar a [0,1].
			if (pk <= mcolBreaks[0])
			{
				return 0.0;
			}

			int last = mcolBreaks.Length - 1;
			if (pk >= mcolBreaks[last])
			{
				return 1.0;
			}

			int i = FindBreakIndexAtOrBefore(pk);
			if (i >= last)
			{
				return 1.0;
			}

			long a = mcolBreaks[i];
			long b = mcolBreaks[i + 1];
			double local;
			if (b <= a)
			{
				local = 0.0;
			}
			else
			{
				local = (double)(pk - a) / (b - a);
			}

			double index = i + local;
			return index / last;
		}

		public long NormalizedToPk(double u)
		{
			if (u < 0.0)
			{
				u = 0.0;
			}

			if (u > 1.0)
			{
				u = 1.0;
			}

			if (mvarMode == MeshYScaleMode.LinearPk || mcolBreaks.Length < 2)
			{
				double pk = mvarPkMin + u * (mvarPkMax - mvarPkMin);
				return (long)Math.Round(pk);
			}

			int last = mcolBreaks.Length - 1;
			double index = u * last;
			int i = (int)Math.Floor(index);
			if (i < 0)
			{
				i = 0;
			}

			if (i >= last)
			{
				return mcolBreaks[last];
			}

			double frac = index - i;
			long a = mcolBreaks[i];
			long b = mcolBreaks[i + 1];
			double pkInterp = a + frac * (b - a);
			return (long)Math.Round(pkInterp);
		}

		/// <summary>
		/// Frontiers de limitaciones (VectorFlex) + estaciones/apeaderos de la vista,
		/// acotados a [pkMin, pkMax], con extremos del intervalo.
		/// </summary>
		public static long[] BuildSingularBreaks(RouteView view, long pkMin, long pkMax)
		{
			SortedSet<long> set = new SortedSet<long>();
			set.Add(pkMin);
			set.Add(pkMax);

			// Estaciones y apeaderos de la vista (PK de ruta).
			int si = 0;
			while (si < view.Stations.Count)
			{
				long pk = view.Stations[si].PK;
				if (pk >= pkMin && pk <= pkMax)
				{
					set.Add(pk);
				}

				si++;
			}

			// Frontiers de cada capa de velocidad en cada tramo físico → PK de ruta.
			int li = 0;
			while (li < view.Legs.Count)
			{
				RouteLeg leg = view.Legs[li];
				CollectSpeedFrontiers(view, leg.Axis, leg.Axis.FixedLimits, pkMin, pkMax, set);
				CollectSpeedFrontiers(view, leg.Axis, leg.Axis.TemporaryLimits, pkMin, pkMax, set);
				CollectSpeedFrontiers(view, leg.Axis, leg.Axis.SessionLimits, pkMin, pkMax, set);
				li++;
			}

			// Si solo extremos (sin singulares intermedios), la escala se degrada a lineal.
			if (set.Count < 2)
			{
				return new[] { pkMin, pkMax };
			}

			long[] arr = new long[set.Count];
			set.CopyTo(arr);
			return arr;
		}

		private static void CollectSpeedFrontiers(
			RouteView view,
			Axis axis,
			SpeedLimitMap map,
			long pkMin,
			long pkMax,
			SortedSet<long> set)
		{
			if (map is null || map.SpeedCount == 0)
			{
				return;
			}

			foreach (KeyValuePair<int, AxisVectorFlex> pair in map.BySpeed)
			{
				AxisVectorFlex flex = pair.Value;
				IReadOnlyList<Punctual<long, LongAxis>> frontiers = flex.Frontiers();
				int fi = 0;
				while (fi < frontiers.Count)
				{
					long axisPk = frontiers[fi].PK;
					long routePk;
					if (view.TryMapAxisToRoute(axis, axisPk, out routePk)
						&& routePk >= pkMin
						&& routePk <= pkMax)
					{
						set.Add(routePk);
					}

					fi++;
				}
			}
		}

		private int FindBreakIndexAtOrBefore(long pk)
		{
			// Binary search: mayor índice con breaks[i] <= pk
			int lo = 0;
			int hi = mcolBreaks.Length - 1;
			int best = 0;
			while (lo <= hi)
			{
				int mid = lo + ((hi - lo) / 2);
				if (mcolBreaks[mid] <= pk)
				{
					best = mid;
					lo = mid + 1;
				}
				else
				{
					hi = mid - 1;
				}
			}

			return best;
		}
	}
}

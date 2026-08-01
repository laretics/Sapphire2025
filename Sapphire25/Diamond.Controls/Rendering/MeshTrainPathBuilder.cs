using System.Globalization;
using System.Text;
using Diamond.Motion;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Construye la traza de una circulación en el diagrama (tiempo × PK) como
	/// spline Catmull-Rom centrípeta convertida a cúbicas Bezier SVG.
	/// Incluye puntos clave en origen, destino y paradas para no aplanar
	/// aceleraciones, frenados ni dwells horizontales.
	/// </summary>
	public static class MeshTrainPathBuilder
	{
		/// <summary>α de la Catmull-Rom centrípeta (0.5): menos overshoot que la uniforme.</summary>
		private const double CentripetalAlpha = 0.5;

		/// <summary>Separación mínima (s) entre muestras de tiempo relativas.</summary>
		private const double MinRelTimeEps = 0.05;

		public readonly struct Point
		{
			private readonly double mvarX;
			private readonly double mvarY;

			public Point(double x, double y)
			{
				mvarX = x;
				mvarY = y;
			}

			public double X
			{
				get { return mvarX; }
			}

			public double Y
			{
				get { return mvarY; }
			}
		}

		/// <summary>
		/// Atributo <c>d</c> del path SVG de la traza (vacío si no hay puntos proyectables).
		/// </summary>
		public static string BuildSvgPath(
			Circulation circulation,
			RouteView displayView,
			long pkMin,
			long pkMax,
			double t0,
			double t1,
			double plotLeft,
			double plotTop,
			double plotW,
			double plotH,
			MeshYScale yScale,
			int maxSamples,
			bool wantLabel,
			out double labelX,
			out double labelY)
		{
			labelX = double.NaN;
			labelY = double.NaN;

			List<Point> points = CollectControlPoints(
				circulation,
				displayView,
				pkMin,
				pkMax,
				t0,
				t1,
				plotLeft,
				plotTop,
				plotW,
				plotH,
				yScale,
				maxSamples,
				wantLabel,
				out labelX,
				out labelY);

			return ToSvgPath(points);
		}

		/// <summary>
		/// Puntos de control en espacio SVG (para hit-test denso o depuración).
		/// </summary>
		public static List<Point> CollectControlPoints(
			Circulation circulation,
			RouteView displayView,
			long pkMin,
			long pkMax,
			double t0,
			double t1,
			double plotLeft,
			double plotTop,
			double plotW,
			double plotH,
			MeshYScale yScale,
			int maxSamples,
			bool wantLabel,
			out double labelX,
			out double labelY)
		{
			labelX = double.NaN;
			labelY = double.NaN;
			List<Point> points = new List<Point>();

			if (circulation is null || displayView is null || yScale is null)
			{
				return points;
			}

			Asimilation asim = circulation.Asimilation;
			double tripSec = asim.TotalTime.TotalSeconds;
			if (tripSec <= 0.0)
			{
				return points;
			}

			double depSec = circulation.Departure.TotalSeconds;
			double timeSpanSec = t1 - t0;
			if (timeSpanSec < 1.0)
			{
				timeSpanSec = 1.0;
			}

			// Margen temporal (~2 % o 30 s) para que el clip lateral no deje el trazo a medias.
			double timeMarginSec = timeSpanSec * 0.02;
			if (timeMarginSec < 30.0)
			{
				timeMarginSec = 30.0;
			}

			if (timeMarginSec > timeSpanSec * 0.15)
			{
				timeMarginSec = timeSpanSec * 0.15;
			}

			double relStart = (t0 - timeMarginSec) - depSec;
			double relEnd = (t1 + timeMarginSec) - depSec;
			if (relStart < 0.0)
			{
				relStart = 0.0;
			}

			if (relEnd > tripSec)
			{
				relEnd = tripSec;
			}

			if (relEnd < relStart)
			{
				return points;
			}

			int budget = maxSamples < 8 ? 8 : maxSamples;
			List<double> relTimes = BuildSampleRelTimes(asim, relStart, relEnd, timeSpanSec, plotW, budget);

			int interiorCount = 0;
			double firstInteriorX = 0.0;
			double firstInteriorY = 0.0;
			double lastInteriorX = 0.0;
			double lastInteriorY = 0.0;

			int i = 0;
			while (i < relTimes.Count)
			{
				double relSec = relTimes[i];
				long asimPk = asim.PKByTime(TimeSpan.FromSeconds(relSec));
				long pk;
				if (!displayView.TryMapRoutePkFrom(asim.View, asimPk, out pk))
				{
					i++;
					continue;
				}

				double absSec = depSec + relSec;
				double x = plotLeft + (absSec - t0) / timeSpanSec * plotW;
				double y = yScale.PkToY(pk, plotTop, plotH);
				points.Add(new Point(x, y));

				bool interior = absSec >= t0 && absSec <= t1 && pk >= pkMin && pk <= pkMax;
				if (interior)
				{
					if (interiorCount == 0)
					{
						firstInteriorX = x;
						firstInteriorY = y;
					}

					lastInteriorX = x;
					lastInteriorY = y;
					interiorCount++;
				}

				i++;
			}

			if (wantLabel && interiorCount > 0)
			{
				labelX = (firstInteriorX + lastInteriorX) * 0.5;
				labelY = (firstInteriorY + lastInteriorY) * 0.5;
			}

			return points;
		}

		/// <summary>
		/// Convierte puntos de control en path SVG con cúbicas Bezier (Catmull-Rom centrípeta).
		/// </summary>
		public static string ToSvgPath(IReadOnlyList<Point> points)
		{
			if (points is null || points.Count == 0)
			{
				return string.Empty;
			}

			if (points.Count == 1)
			{
				return "M"
					+ Fmt(points[0].X) + "," + Fmt(points[0].Y);
			}

			if (points.Count == 2)
			{
				return "M"
					+ Fmt(points[0].X) + "," + Fmt(points[0].Y)
					+ " L"
					+ Fmt(points[1].X) + "," + Fmt(points[1].Y);
			}

			StringBuilder sb = new StringBuilder(points.Count * 48);
			sb.Append('M');
			sb.Append(Fmt(points[0].X));
			sb.Append(',');
			sb.Append(Fmt(points[0].Y));

			// Extremos: duplicar primer y último punto (tangente natural).
			int last = points.Count - 1;
			int i = 0;
			while (i < last)
			{
				Point p0 = i == 0 ? points[0] : points[i - 1];
				Point p1 = points[i];
				Point p2 = points[i + 1];
				Point p3 = i + 1 >= last ? points[last] : points[i + 2];

				double c1x, c1y, c2x, c2y;
				CentripetalSegmentControls(
					p0.X, p0.Y, p1.X, p1.Y, p2.X, p2.Y, p3.X, p3.Y,
					out c1x, out c1y, out c2x, out c2y);

				sb.Append(" C");
				sb.Append(Fmt(c1x));
				sb.Append(',');
				sb.Append(Fmt(c1y));
				sb.Append(' ');
				sb.Append(Fmt(c2x));
				sb.Append(',');
				sb.Append(Fmt(c2y));
				sb.Append(' ');
				sb.Append(Fmt(p2.X));
				sb.Append(',');
				sb.Append(Fmt(p2.Y));

				i++;
			}

			return sb.ToString();
		}

		/// <summary>
		/// Controles de la cúbica Bezier del segmento centrípeto P1→P2
		/// (vecinos P0 y P3; α = <see cref="CentripetalAlpha"/>).
		/// </summary>
		public static void CentripetalSegmentControls(
			double p0x, double p0y,
			double p1x, double p1y,
			double p2x, double p2y,
			double p3x, double p3y,
			out double c1x, out double c1y,
			out double c2x, out double c2y)
		{
			double t0 = 0.0;
			double t1 = t0 + Math.Pow(Distance(p0x, p0y, p1x, p1y), CentripetalAlpha);
			double t2 = t1 + Math.Pow(Distance(p1x, p1y, p2x, p2y), CentripetalAlpha);
			double t3 = t2 + Math.Pow(Distance(p2x, p2y, p3x, p3y), CentripetalAlpha);

			// Puntos coincidentes: caer en segmento lineal.
			if (t1 - t0 < 1e-9)
			{
				t1 = t0 + 1.0;
			}

			if (t2 - t1 < 1e-9)
			{
				t2 = t1 + 1.0;
			}

			if (t3 - t2 < 1e-9)
			{
				t3 = t2 + 1.0;
			}

			double dt = t2 - t1;
			c1x = p1x + (p2x - p0x) / (t2 - t0) * dt / 3.0;
			c1y = p1y + (p2y - p0y) / (t2 - t0) * dt / 3.0;
			c2x = p2x - (p3x - p1x) / (t3 - t1) * dt / 3.0;
			c2y = p2y - (p3y - p1y) / (t3 - t1) * dt / 3.0;
		}

		/// <summary>
		/// Tiempos relativos a muestrear: extremos, paradas (llegada/salida) y relleno uniforme
		/// acotado al presupuesto (orientado a ~1 punto cada ~6 px de anchura de traza).
		/// </summary>
		private static List<double> BuildSampleRelTimes(
			Asimilation asim,
			double relStart,
			double relEnd,
			double timeSpanSec,
			double plotW,
			int maxSamples)
		{
			double span = relEnd - relStart;
			if (span < 0.0)
			{
				return new List<double>();
			}

			// Presupuesto orientado a píxeles (más denso que la polilínea antigua).
			double approxWidthPx = (span / timeSpanSec) * plotW;
			int byPixels = (int)Math.Ceiling(approxWidthPx / 6.0);
			if (byPixels < 16)
			{
				byPixels = 16;
			}

			int budget = byPixels;
			if (budget > maxSamples)
			{
				budget = maxSamples;
			}

			if (budget < 8)
			{
				budget = 8;
			}

			// —— Puntos clave (siempre que quepan) ——
			List<double> keys = new List<double>(16);
			AddRelTime(keys, relStart);
			AddRelTime(keys, relEnd);

			// Extremos del perfil (si caen en la ventana).
			AddRelTime(keys, 0.0);
			AddRelTime(keys, asim.TotalTime.TotalSeconds);

			int si = 0;
			while (si < asim.Stops.Count)
			{
				AsimilationStop stop = asim.Stops[si];
				TimeSpan? arr = asim.TimeArriveByPK(stop.PK);
				TimeSpan? dep = asim.TimeDepartByPK(stop.PK);
				if (arr.HasValue)
				{
					AddRelTime(keys, arr.Value.TotalSeconds);
				}

				if (dep.HasValue)
				{
					AddRelTime(keys, dep.Value.TotalSeconds);
				}

				si++;
			}

			// Filtrar a [relStart, relEnd] y ordenar únicos.
			List<double> keyInWindow = new List<double>(keys.Count);
			int ki = 0;
			while (ki < keys.Count)
			{
				double t = keys[ki];
				if (t >= relStart - MinRelTimeEps && t <= relEnd + MinRelTimeEps)
				{
					if (t < relStart)
					{
						t = relStart;
					}

					if (t > relEnd)
					{
						t = relEnd;
					}

					AddRelTimeSortedUnique(keyInWindow, t);
				}

				ki++;
			}

			// —— Relleno uniforme con el presupuesto restante ——
			List<double> samples = new List<double>(budget + 4);
			int k = 0;
			while (k < keyInWindow.Count)
			{
				samples.Add(keyInWindow[k]);
				k++;
			}

			int remaining = budget - samples.Count;
			if (remaining < 2)
			{
				// Asegurar al menos una malla gruesa si solo hay pocos keys.
				remaining = Math.Max(0, 8 - samples.Count);
			}

			if (remaining > 0 && span > MinRelTimeEps)
			{
				int steps = remaining + 1;
				int s = 1;
				while (s < steps)
				{
					double u = (double)s / steps;
					AddRelTimeSortedUnique(samples, relStart + span * u);
					s++;
				}
			}

			// Si aún hay demasiados (keys densos), recortar manteniendo extremos y espaciado.
			if (samples.Count > budget)
			{
				samples = ThinKeepEnds(samples, budget);
			}

			// Garantizar extremos exactos.
			if (samples.Count == 0 || samples[0] > relStart + MinRelTimeEps)
			{
				samples.Insert(0, relStart);
			}
			else
			{
				samples[0] = relStart;
			}

			if (samples[samples.Count - 1] < relEnd - MinRelTimeEps)
			{
				samples.Add(relEnd);
			}
			else
			{
				samples[samples.Count - 1] = relEnd;
			}

			return samples;
		}

		private static List<double> ThinKeepEnds(List<double> sorted, int budget)
		{
			if (sorted.Count <= budget || budget < 2)
			{
				return sorted;
			}

			List<double> result = new List<double>(budget);
			result.Add(sorted[0]);
			int i = 1;
			while (i < budget - 1)
			{
				double u = (double)i / (budget - 1);
				int idx = (int)Math.Round(u * (sorted.Count - 1));
				if (idx <= 0)
				{
					idx = 1;
				}

				if (idx >= sorted.Count - 1)
				{
					idx = sorted.Count - 2;
				}

				double t = sorted[idx];
				if (t - result[result.Count - 1] >= MinRelTimeEps)
				{
					result.Add(t);
				}

				i++;
			}

			double last = sorted[sorted.Count - 1];
			if (last - result[result.Count - 1] < MinRelTimeEps && result.Count > 1)
			{
				result[result.Count - 1] = last;
			}
			else
			{
				result.Add(last);
			}

			return result;
		}

		private static void AddRelTime(List<double> list, double t)
		{
			if (double.IsNaN(t) || double.IsInfinity(t))
			{
				return;
			}

			list.Add(t);
		}

		private static void AddRelTimeSortedUnique(List<double> sorted, double t)
		{
			if (double.IsNaN(t) || double.IsInfinity(t))
			{
				return;
			}

			int lo = 0;
			int hi = sorted.Count;
			while (lo < hi)
			{
				int mid = (lo + hi) / 2;
				if (sorted[mid] < t)
				{
					lo = mid + 1;
				}
				else
				{
					hi = mid;
				}
			}

			if (lo > 0 && t - sorted[lo - 1] < MinRelTimeEps)
			{
				return;
			}

			if (lo < sorted.Count && sorted[lo] - t < MinRelTimeEps)
			{
				return;
			}

			sorted.Insert(lo, t);
		}

		private static double Distance(double x0, double y0, double x1, double y1)
		{
			double dx = x1 - x0;
			double dy = y1 - y0;
			return Math.Sqrt(dx * dx + dy * dy);
		}

		private static string Fmt(double v)
		{
			return v.ToString("0.##", CultureInfo.InvariantCulture);
		}
	}
}

using Diamond.Motion;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Selección de circulación por proximidad del cursor a la traza en coordenadas SVG.
	/// </summary>
	public static class MeshCirculationHitTest
	{
		/// <summary>
		/// Radio de influencia por defecto en unidades del viewBox SVG (~píxeles lógicos).
		/// </summary>
		public const double DefaultInfluenceRadiusSvg = 14.0;

		/// <summary>
		/// Devuelve la circulación más cercana al punto SVG si está dentro del radio de influencia.
		/// </summary>
		public static Circulation? TryPickNearest(
			Mesh mesh,
			RouteView view,
			TimeSpan timeStart,
			TimeSpan timeEnd,
			long pkMin,
			long pkMax,
			int width,
			int height,
			MeshSvgDrawOptions options,
			double svgX,
			double svgY,
			double influenceRadiusSvg = DefaultInfluenceRadiusSvg)
		{
			if (mesh is null || view is null || influenceRadiusSvg <= 0.0)
			{
				return null;
			}

			bool externalStations = options.ExternalStationColumn;
			double plotLeft = MeshSvgLayout.GetPlotLeft(externalStations);
			double plotTop = MeshSvgLayout.PlotTop;
			double plotW = MeshSvgLayout.GetPlotWidth(width, externalStations);
			double plotH = MeshSvgLayout.PlotHeight(height);
			MeshYScale yScale = MeshYScale.Create(options.YScaleMode, view, pkMin, pkMax);

			double t0 = timeStart.TotalSeconds;
			double t1 = timeEnd.TotalSeconds;
			if (t1 <= t0)
			{
				t1 = t0 + 3600.0;
			}

			// Solo tiene sentido seleccionar dentro del plot (con un poco de margen).
			if (svgX < plotLeft - influenceRadiusSvg
				|| svgX > plotLeft + plotW + influenceRadiusSvg
				|| svgY < plotTop - influenceRadiusSvg
				|| svgY > plotTop + plotH + influenceRadiusSvg)
			{
				return null;
			}

			double maxDistSq = influenceRadiusSvg * influenceRadiusSvg;
			Circulation? best = null;
			double bestDistSq = maxDistSq;

			// Muestreo denso para el hit-test (independiente del LOD de dibujo).
			int samples = 40;
			int ci = 0;
			while (ci < mesh.Circulations.Count)
			{
				Circulation c = mesh.Circulations[ci];
				double depSec = c.Departure.TotalSeconds;
				double arrSec = c.Arrival.TotalSeconds;
				if (arrSec < t0 || depSec > t1)
				{
					ci++;
					continue;
				}

				if (!MeshCantonGeometry.IsVisibleOnView(c.Asimilation, view))
				{
					ci++;
					continue;
				}

				double distSq = MinDistanceSquaredToPath(
					c, view, pkMin, pkMax, t0, t1,
					plotLeft, plotTop, plotW, plotH, yScale,
					svgX, svgY, samples);

				if (distSq < bestDistSq)
				{
					bestDistSq = distSq;
					best = c;
				}

				ci++;
			}

			return best;
		}

		private static double MinDistanceSquaredToPath(
			Circulation c,
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
			double px,
			double py,
			int maxSamples)
		{
			Asimilation asim = c.Asimilation;
			double tripSec = asim.TotalTime.TotalSeconds;
			if (tripSec <= 0.0)
			{
				return double.PositiveInfinity;
			}

			double depSec = c.Departure.TotalSeconds;
			double timeSpanSec = t1 - t0;
			if (timeSpanSec < 1.0)
			{
				timeSpanSec = 1.0;
			}

			// Misma ventana temporal que el render (con margen ligero).
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
				return double.PositiveInfinity;
			}

			int steps = maxSamples;
			if (steps < 12)
			{
				steps = 12;
			}

			double minDistSq = double.PositiveInfinity;
			bool hasPrev = false;
			double prevX = 0.0;
			double prevY = 0.0;

			int s = 0;
			while (s <= steps)
			{
				double u = (double)s / steps;
				double relSec = relStart + (relEnd - relStart) * u;
				long asimPk = asim.PKByTime(TimeSpan.FromSeconds(relSec));
				long pk;
				if (!displayView.TryMapRoutePkFrom(asim.View, asimPk, out pk))
				{
					hasPrev = false;
					s++;
					continue;
				}

				double absSec = depSec + relSec;
				double x = plotLeft + (absSec - t0) / timeSpanSec * plotW;
				double y = yScale.PkToY(pk, plotTop, plotH);

				// Distancia al vértice (y, si hay segmento, a la arista).
				double dx = x - px;
				double dy = y - py;
				double dVertex = dx * dx + dy * dy;
				if (dVertex < minDistSq)
				{
					minDistSq = dVertex;
				}

				if (hasPrev)
				{
					double dSeg = DistanceSquaredPointToSegment(px, py, prevX, prevY, x, y);
					if (dSeg < minDistSq)
					{
						minDistSq = dSeg;
					}
				}

				prevX = x;
				prevY = y;
				hasPrev = true;
				s++;
			}

			return minDistSq;
		}

		/// <summary>Distancia al cuadrado del punto P al segmento AB.</summary>
		private static double DistanceSquaredPointToSegment(
			double px, double py,
			double ax, double ay,
			double bx, double by)
		{
			double abx = bx - ax;
			double aby = by - ay;
			double apx = px - ax;
			double apy = py - ay;
			double abLenSq = abx * abx + aby * aby;
			if (abLenSq < 1e-12)
			{
				return apx * apx + apy * apy;
			}

			double t = (apx * abx + apy * aby) / abLenSq;
			if (t < 0.0)
			{
				t = 0.0;
			}
			else if (t > 1.0)
			{
				t = 1.0;
			}

			double cx = ax + t * abx;
			double cy = ay + t * aby;
			double dx = px - cx;
			double dy = py - cy;
			return dx * dx + dy * dy;
		}
	}
}

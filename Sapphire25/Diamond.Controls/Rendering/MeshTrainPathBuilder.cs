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
	/// Las tangentes usan la derivada no uniforme (no la cuerda P0–P2) y se clampean
	/// para no invertir el sentido del tramo en ida ni en vuelta.
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

		/// <summary>Banda donde se ancla el número de tren.</summary>
		public enum TrainLabelBand
		{
			/// <summary>Dentro del plot, a continuación del trazo.</summary>
			Plot = 0,
			/// <summary>Regla superior (borde superior del plot).</summary>
			TopRuler = 1,
			/// <summary>Regla inferior / barra de horas.</summary>
			BottomRuler = 2
		}

		/// <summary>
		/// Colocación de un número de tren: inicio o fin de la traza visible,
		/// en regla superior/inferior (intersección trazo–borde) o a continuación
		/// del segmento en el plot; siempre rotado por la pendiente del extremo.
		/// </summary>
		public readonly struct TrainLabelPlacement
		{
			private readonly double mvarX;
			private readonly double mvarY;
			private readonly double mvarAngleDeg;
			private readonly TrainLabelBand mvarBand;
			private readonly bool mvarValid;

			public static TrainLabelPlacement None
			{
				get { return default; }
			}

			public TrainLabelPlacement(double x, double y, double angleDeg, TrainLabelBand band)
			{
				mvarX = x;
				mvarY = y;
				mvarAngleDeg = angleDeg;
				mvarBand = band;
				mvarValid = true;
			}

			public bool IsValid
			{
				get { return mvarValid; }
			}

			public double X
			{
				get { return mvarX; }
			}

			public double Y
			{
				get { return mvarY; }
			}

			/// <summary>Ángulo SVG en grados (pendiente del extremo, texto legible).</summary>
			public double AngleDeg
			{
				get { return mvarAngleDeg; }
			}

			public TrainLabelBand Band
			{
				get { return mvarBand; }
			}

			/// <summary>True si está en la regla inferior (compat).</summary>
			public bool OnTimeRuler
			{
				get { return mvarBand == TrainLabelBand.BottomRuler; }
			}

			/// <summary>True si está en la regla superior.</summary>
			public bool OnTopRuler
			{
				get { return mvarBand == TrainLabelBand.TopRuler; }
			}
		}

		/// <summary>Tamaño de fuente usado al estimar el rectángulo del número.</summary>
		public const double TrainNumberFontSize = 11.0;

		/// <summary>Holgura (px) respecto al borde del plot para caber el texto en el plot.</summary>
		private const double LabelPlotPadding = 5.0;

		/// <summary>Distancia al borde izquierdo (eje Y / inicio temporal) para omitir el número.</summary>
		private const double AxisYOmitPx = 10.0;

		/// <summary>Proximidad al borde superior/inferior para anclar a la regla.</summary>
		private const double RulerGluePx = 14.0;

		/// <summary>Desplazamiento Y de la etiqueta en la regla superior (por encima del plot).</summary>
		private const double TopRulerLabelOffset = 10.0;

		/// <summary>Desplazamiento Y de la etiqueta en la regla inferior (bajo el plot).</summary>
		private const double BottomRulerLabelOffset = 14.0;

		/// <summary>
		/// Atributo <c>d</c> del path SVG de la traza (vacío si no hay puntos proyectables).
		/// </summary>
		/// <param name="useSpline">
		/// True: cúbicas Bezier (Catmull-Rom). False: polilínea por los puntos de control
		/// (más barato y estable en pan/zoom interactivo).
		/// </param>
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
			out double labelY,
			bool useSpline = true)
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

			return ToSvgPath(points, useSpline);
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

			// 1) Encontrar el tramo de tiempo de la asimilación que es proyectable sobre la vista
			//    (p. ej. trenes T3 en la vista T3+T2: solo Palma–Enllaç).
			//    mapRel0/mapRel1 quedan siempre rellenos: rango completo si el camino es el
			//    mismo/inverso, o el subtramo común (hasta Enllaç) si es proyección parcial.
			//    Importante: muestrear SOLO ese intervalo. Si se muestrea todo PMI→MAN con
			//    presupuesto limitado y Enllaç en skip, el último key mapeable cae en Inca y
			//    la traza se corta antes del fin del tramo común.
			double mapRel0;
			double mapRel1;
			TryFindMappableRelRange(
				asim, displayView, relStart, relEnd, out mapRel0, out mapRel1);

			// 2) Muestrear densamente el tramo proyectable (incluye extremos del solape).
			double sampleStart = mapRel0;
			double sampleEnd = mapRel1;
			if (sampleEnd < sampleStart)
			{
				return points;
			}

			List<double> relTimes = BuildSampleRelTimes(
				asim, sampleStart, sampleEnd, timeSpanSec, plotW, budget);

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

				// Clip espacial a la ventana visible (pkMin–pkMax de la vista).
				if (pk < pkMin)
				{
					pk = pkMin;
				}

				if (pk > pkMax)
				{
					pk = pkMax;
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

			// Compat: centro del tramo interior (la colocación fina usa ComputeLabelPlacement).
			if (wantLabel && interiorCount > 0)
			{
				labelX = (firstInteriorX + lastInteriorX) * 0.5;
				labelY = (firstInteriorY + lastInteriorY) * 0.5;
			}

			return points;
		}

		/// <summary>
		/// Compat: primera colocación de <see cref="ComputeLabelPlacements"/>.
		/// </summary>
		public static TrainLabelPlacement ComputeLabelPlacement(
			IReadOnlyList<Point> points,
			double plotLeft,
			double plotTop,
			double plotW,
			double plotH,
			string text)
		{
			List<TrainLabelPlacement> all = ComputeLabelPlacements(
				points, plotLeft, plotTop, plotW, plotH, text);
			if (all.Count == 0)
			{
				return TrainLabelPlacement.None;
			}

			return all[0];
		}

		/// <summary>
		/// Dos números por tren (inicio y fin de la representación visible):
		/// <list type="bullet">
		/// <item>Extremo en regla superior/inferior → número en esa regla, X = intersección, rotado.</item>
		/// <item>Extremo en el plot → a continuación del trazo, rotado por la pendiente.</item>
		/// <item>Extremo en el eje Y (borde izquierdo del plot) → no se imprime ese número.</item>
		/// </list>
		/// </summary>
		public static List<TrainLabelPlacement> ComputeLabelPlacements(
			IReadOnlyList<Point> points,
			double plotLeft,
			double plotTop,
			double plotW,
			double plotH,
			string text)
		{
			List<TrainLabelPlacement> result = new List<TrainLabelPlacement>(2);
			if (points is null || points.Count == 0 || plotW < 1.0 || plotH < 1.0)
			{
				return result;
			}

			string labelText = string.IsNullOrEmpty(text) ? "?" : text;
			double textW = EstimateTextWidth(labelText, TrainNumberFontSize);
			double textH = TrainNumberFontSize;

			// Inicio y fin de la representación.
			TryAddEndpointLabel(
				result, points, atStart: true,
				plotLeft, plotTop, plotW, plotH, textW, textH);
			TryAddEndpointLabel(
				result, points, atStart: false,
				plotLeft, plotTop, plotW, plotH, textW, textH);

			return result;
		}

		/// <summary>
		/// Añade la etiqueta del extremo inicial o final si no cae en el eje Y.
		/// </summary>
		private static void TryAddEndpointLabel(
			List<TrainLabelPlacement> result,
			IReadOnlyList<Point> points,
			bool atStart,
			double plotLeft,
			double plotTop,
			double plotW,
			double plotH,
			double textW,
			double textH)
		{
			Point anchor;
			double pathUx;
			double pathUy;
			double outUx;
			double outUy;
			if (!TryGetEndpointGeometry(points, atStart, out anchor, out pathUx, out pathUy, out outUx, out outUy))
			{
				return;
			}

			// Caso especial: extremo en el eje Y (borde izquierdo del plot) → no imprimir.
			if (anchor.X <= plotLeft + AxisYOmitPx)
			{
				return;
			}

			// Ángulo siempre según la pendiente del trazo en ese extremo (dirección de marcha).
			double angleDeg = ReadableAngleDeg(pathUx, pathUy);

			// Pegado a la regla superior → X de intersección, Y en banda superior.
			if (anchor.Y <= plotTop + RulerGluePx)
			{
				double x = Clamp(anchor.X, plotLeft + 2.0, plotLeft + plotW - 2.0);
				double y = plotTop - TopRulerLabelOffset;
				result.Add(new TrainLabelPlacement(x, y, angleDeg, TrainLabelBand.TopRuler));
				return;
			}

			// Pegado a la regla inferior → X de intersección, Y en barra de horas.
			if (anchor.Y >= plotTop + plotH - RulerGluePx)
			{
				double x = Clamp(anchor.X, plotLeft + 2.0, plotLeft + plotW - 2.0);
				double y = plotTop + plotH + BottomRulerLabelOffset;
				result.Add(new TrainLabelPlacement(x, y, angleDeg, TrainLabelBand.BottomRuler));
				return;
			}

			// Zona representable: a continuación del trazo (hacia fuera del segmento).
			double offset = 8.0 + textW * 0.5;
			double lx = anchor.X + outUx * offset;
			double ly = anchor.Y + outUy * offset;

			if (!LabelFitsInPlot(lx, ly, textW, textH, angleDeg, plotLeft, plotTop, plotW, plotH))
			{
				// Si no cabe desplazado, intentar sobre el propio extremo (aún en plot).
				lx = anchor.X;
				ly = anchor.Y;
				if (!LabelFitsInPlot(lx, ly, textW, textH, angleDeg, plotLeft, plotTop, plotW, plotH))
				{
					return;
				}
			}

			result.Add(new TrainLabelPlacement(lx, ly, angleDeg, TrainLabelBand.Plot));
		}

		/// <summary>
		/// Geometría del extremo: ancla, dirección de marcha (pathU) y hacia fuera del trazo (outU).
		/// </summary>
		private static bool TryGetEndpointGeometry(
			IReadOnlyList<Point> points,
			bool atStart,
			out Point anchor,
			out double pathUx,
			out double pathUy,
			out double outUx,
			out double outUy)
		{
			anchor = default;
			pathUx = 1.0;
			pathUy = 0.0;
			outUx = 1.0;
			outUy = 0.0;

			if (points is null || points.Count == 0)
			{
				return false;
			}

			if (points.Count == 1)
			{
				anchor = points[0];
				pathUx = 1.0;
				pathUy = 0.0;
				outUx = atStart ? -1.0 : 1.0;
				outUy = 0.0;
				return true;
			}

			if (atStart)
			{
				anchor = points[0];
				int j = 1;
				while (j < points.Count && SegmentLength2(points[0], points[j]) < 4.0)
				{
					j++;
				}

				if (j >= points.Count)
				{
					j = 1;
				}

				Point p1 = points[j];
				double dx = p1.X - anchor.X;
				double dy = p1.Y - anchor.Y;
				Normalize(ref dx, ref dy);
				pathUx = dx;
				pathUy = dy;
				// Hacia fuera del trazo = opuesto a la marcha.
				outUx = -dx;
				outUy = -dy;
			}
			else
			{
				anchor = points[points.Count - 1];
				int j = points.Count - 2;
				while (j >= 0 && SegmentLength2(points[j], anchor) < 4.0)
				{
					j--;
				}

				if (j < 0)
				{
					j = points.Count - 2;
				}

				Point pPrev = points[j];
				double dx = anchor.X - pPrev.X;
				double dy = anchor.Y - pPrev.Y;
				Normalize(ref dx, ref dy);
				pathUx = dx;
				pathUy = dy;
				outUx = dx;
				outUy = dy;
			}

			return true;
		}

		private static void Normalize(ref double dx, ref double dy)
		{
			double len = Math.Sqrt(dx * dx + dy * dy);
			if (len < 1e-6)
			{
				dx = 1.0;
				dy = 0.0;
				return;
			}

			dx /= len;
			dy /= len;
		}

		private static double Clamp(double v, double min, double max)
		{
			if (v < min)
			{
				return min;
			}

			if (v > max)
			{
				return max;
			}

			return v;
		}

		private static double SegmentLength2(Point a, Point b)
		{
			double dx = b.X - a.X;
			double dy = b.Y - a.Y;
			return dx * dx + dy * dy;
		}

		/// <summary>
		/// Ángulo en grados con texto legible (no boca abajo): [-90, 90].
		/// </summary>
		public static double ReadableAngleDeg(double ux, double uy)
		{
			double deg = Math.Atan2(uy, ux) * (180.0 / Math.PI);
			if (deg > 90.0)
			{
				deg -= 180.0;
			}
			else if (deg < -90.0)
			{
				deg += 180.0;
			}

			return deg;
		}

		private static bool LabelFitsInPlot(
			double cx,
			double cy,
			double textW,
			double textH,
			double angleDeg,
			double plotLeft,
			double plotTop,
			double plotW,
			double plotH)
		{
			double rad = angleDeg * (Math.PI / 180.0);
			double c = Math.Abs(Math.Cos(rad));
			double s = Math.Abs(Math.Sin(rad));
			double halfW = textW * 0.5;
			double halfH = textH * 0.5;
			// AABB del texto rotado.
			double extX = halfW * c + halfH * s;
			double extY = halfW * s + halfH * c;
			double pad = LabelPlotPadding;

			return cx - extX >= plotLeft + pad
				&& cx + extX <= plotLeft + plotW - pad
				&& cy - extY >= plotTop + pad
				&& cy + extY <= plotTop + plotH - pad;
		}

		private static double EstimateTextWidth(string text, double fontSize)
		{
			// Aprox. monoespaciada cómoda para dígitos/letras de número de tren.
			int n = text.Length;
			if (n < 1)
			{
				n = 1;
			}

			return n * fontSize * 0.62;
		}

		/// <summary>
		/// Localiza el intervalo de tiempo relativo [mapRel0, mapRel1] de la asimilación
		/// que proyecta sobre <paramref name="displayView"/>. Si todo el tramo es proyectable
		/// (mismo corredor / inverso), devuelve false en el sentido de “no es parcial”.
		/// Usa búsqueda binaria en los extremos para no cortar antes del fin del tramo común
		/// (p. ej. PMI→MAN en T3+T2 debe llegar a Enllaç, no pararse en Inca).
		/// </summary>
		private static bool TryFindMappableRelRange(
			Asimilation asim,
			RouteView displayView,
			double relStart,
			double relEnd,
			out double mapRel0,
			out double mapRel1)
		{
			mapRel0 = relStart;
			mapRel1 = relEnd;

			// Mismo corredor o inverso: toda la asimilación es proyectable.
			if (displayView.IsSameOrReversePath(asim.View))
			{
				return false;
			}

			if (relEnd <= relStart)
			{
				return true;
			}

			// Primer instante mapeable (búsqueda desde el inicio).
			if (!TryMapRel(asim, displayView, relStart))
			{
				if (!TryFindFirstMappable(asim, displayView, relStart, relEnd, out mapRel0))
				{
					mapRel1 = mapRel0;
					return true;
				}
			}
			else
			{
				mapRel0 = relStart;
			}

			// Último instante mapeable (búsqueda desde el fin hacia atrás, con bisección).
			if (TryMapRel(asim, displayView, relEnd))
			{
				mapRel1 = relEnd;
			}
			else if (!TryFindLastMappable(asim, displayView, mapRel0, relEnd, out mapRel1))
			{
				mapRel1 = mapRel0;
			}

			if (mapRel1 < mapRel0)
			{
				mapRel1 = mapRel0;
			}

			double spanAll = relEnd - relStart;
			double spanMap = mapRel1 - mapRel0;
			return spanAll > 1.0 && spanMap < spanAll * 0.98;
		}

		private static bool TryMapRel(Asimilation asim, RouteView displayView, double relSec)
		{
			long asimPk = asim.PKByTime(TimeSpan.FromSeconds(relSec));
			long displayPk;
			return displayView.TryMapRoutePkFrom(asim.View, asimPk, out displayPk);
		}

		/// <summary>Primer rel en (lo, hi] que mapea (bisección; lo no mapea o es el inicio).</summary>
		private static bool TryFindFirstMappable(
			Asimilation asim,
			RouteView displayView,
			double lo,
			double hi,
			out double first)
		{
			first = lo;
			if (TryMapRel(asim, displayView, hi) && !TryMapRel(asim, displayView, lo))
			{
				// ok
			}
			else if (TryMapRel(asim, displayView, lo))
			{
				first = lo;
				return true;
			}
			else if (!TryMapRel(asim, displayView, hi))
			{
				return false;
			}

			int iter = 0;
			while (iter < 40 && hi - lo > 0.05)
			{
				double mid = 0.5 * (lo + hi);
				if (TryMapRel(asim, displayView, mid))
				{
					hi = mid;
				}
				else
				{
					lo = mid;
				}

				iter++;
			}

			first = hi;
			return TryMapRel(asim, displayView, first);
		}

		/// <summary>
		/// Último rel en [lo, hi) que mapea. <paramref name="lo"/> debe mapear; <paramref name="hi"/> no.
		/// </summary>
		private static bool TryFindLastMappable(
			Asimilation asim,
			RouteView displayView,
			double lo,
			double hi,
			out double last)
		{
			last = lo;
			if (!TryMapRel(asim, displayView, lo))
			{
				return false;
			}

			if (TryMapRel(asim, displayView, hi))
			{
				last = hi;
				return true;
			}

			// Bisección: mantener lo mapeable, hi no mapeable.
			int iter = 0;
			while (iter < 48 && hi - lo > 0.05)
			{
				double mid = 0.5 * (lo + hi);
				if (TryMapRel(asim, displayView, mid))
				{
					lo = mid;
				}
				else
				{
					hi = mid;
				}

				iter++;
			}

			last = lo;
			return true;
		}

		/// <summary>
		/// Convierte puntos de control en path SVG.
		/// Con <paramref name="useSpline"/>: cúbicas Bezier (Catmull-Rom centrípeta).
		/// Sin spline: polilínea <c>M … L …</c> (LOD de interacción).
		/// </summary>
		public static string ToSvgPath(IReadOnlyList<Point> points, bool useSpline = true)
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

			if (points.Count == 2 || !useSpline)
			{
				return ToPolylineSvgPath(points);
			}

			StringBuilder sb = new StringBuilder(points.Count * 48);
			sb.Append('M');
			sb.Append(Fmt(points[0].X));
			sb.Append(',');
			sb.Append(Fmt(points[0].Y));

			// Extremos abiertos: reflexión del vecino (mejor que duplicar: no aplana la tangente).
			int last = points.Count - 1;
			int i = 0;
			while (i < last)
			{
				Point p1 = points[i];
				Point p2 = points[i + 1];
				Point p0 = i == 0
					? Reflect(p1, p2)
					: points[i - 1];
				Point p3 = i + 1 >= last
					? Reflect(p2, p1)
					: points[i + 2];

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
		/// Path SVG como polilínea (solo M/L): barato y estable al re-muestrear en pan/zoom.
		/// </summary>
		public static string ToPolylineSvgPath(IReadOnlyList<Point> points)
		{
			if (points is null || points.Count == 0)
			{
				return string.Empty;
			}

			StringBuilder sb = new StringBuilder(points.Count * 24);
			sb.Append('M');
			sb.Append(Fmt(points[0].X));
			sb.Append(',');
			sb.Append(Fmt(points[0].Y));

			int i = 1;
			while (i < points.Count)
			{
				sb.Append(" L");
				sb.Append(Fmt(points[i].X));
				sb.Append(',');
				sb.Append(Fmt(points[i].Y));
				i++;
			}

			return sb.ToString();
		}

		/// <summary>
		/// Refleja <paramref name="other"/> sobre <paramref name="center"/>: 2·center − other.
		/// Usado para vecinos fantasma en extremos abiertos de la spline.
		/// </summary>
		private static Point Reflect(Point center, Point other)
		{
			return new Point(2.0 * center.X - other.X, 2.0 * center.Y - other.Y);
		}

		/// <summary>
		/// Controles de la cúbica Bezier del segmento centrípeto P1→P2
		/// (vecinos P0 y P3; α = <see cref="CentripetalAlpha"/>).
		/// Tangentes según la derivada Catmull-Rom no uniforme (no el atajo de cuerda P0–P2),
		/// con clamp para no invertir el sentido del tramo (crítico en ida vs vuelta).
		/// </summary>
		public static void CentripetalSegmentControls(
			double p0x, double p0y,
			double p1x, double p1y,
			double p2x, double p2y,
			double p3x, double p3y,
			out double c1x, out double c1y,
			out double c2x, out double c2y)
		{
			double t01 = Math.Pow(Distance(p0x, p0y, p1x, p1y), CentripetalAlpha);
			double t12 = Math.Pow(Distance(p1x, p1y, p2x, p2y), CentripetalAlpha);
			double t23 = Math.Pow(Distance(p2x, p2y, p3x, p3y), CentripetalAlpha);

			// Segmentos degenerados → Bezier lineal P1–P2.
			if (t12 < 1e-9)
			{
				c1x = p1x + (p2x - p1x) / 3.0;
				c1y = p1y + (p2y - p1y) / 3.0;
				c2x = p1x + 2.0 * (p2x - p1x) / 3.0;
				c2y = p1y + 2.0 * (p2y - p1y) / 3.0;
				return;
			}

			if (t01 < 1e-9)
			{
				t01 = t12;
			}

			if (t23 < 1e-9)
			{
				t23 = t12;
			}

			// Tangentes m1 en P1 y m2 en P2 (param u∈[0,1] del segmento), α-céntripeta.
			// m1 = (P2−P1) + t12·((P1−P0)/t01 − (P2−P0)/(t01+t12))
			// m2 = (P2−P1) + t12·((P3−P2)/t23 − (P3−P1)/(t12+t23))
			double t01t12 = t01 + t12;
			double t12t23 = t12 + t23;

			double m1x = (p2x - p1x) + t12 * ((p1x - p0x) / t01 - (p2x - p0x) / t01t12);
			double m1y = (p2y - p1y) + t12 * ((p1y - p0y) / t01 - (p2y - p0y) / t01t12);
			double m2x = (p2x - p1x) + t12 * ((p3x - p2x) / t23 - (p3x - p1x) / t12t23);
			double m2y = (p2y - p1y) + t12 * ((p3y - p2y) / t23 - (p3y - p1y) / t12t23);

			// Bezier: c1 = P1 + m1/3, c2 = P2 − m2/3
			c1x = p1x + m1x / 3.0;
			c1y = p1y + m1y / 3.0;
			c2x = p2x - m2x / 3.0;
			c2y = p2y - m2y / 3.0;

			// Evitar handles que inviertan el sentido del tramo (p. ej. vuelta vs ida
			// con overshoot de la Catmull-Rom en el plano tiempo×PK).
			ClampHandleToSegment(p1x, p1y, p2x, p2y, ref c1x, ref c1y, towardEnd: true);
			ClampHandleToSegment(p1x, p1y, p2x, p2y, ref c2x, ref c2y, towardEnd: false);
		}

		/// <summary>
		/// Proyecta el handle sobre el tramo P1→P2 si tira hacia atrás (proyección &lt; 0)
		/// y mantiene la componente de tiempo monótona (X entre P1 y P2).
		/// </summary>
		private static void ClampHandleToSegment(
			double p1x, double p1y,
			double p2x, double p2y,
			ref double cx, ref double cy,
			bool towardEnd)
		{
			double segDx = p2x - p1x;
			double segDy = p2y - p1y;
			double len2 = segDx * segDx + segDy * segDy;
			if (len2 < 1e-12)
			{
				cx = p1x;
				cy = p1y;
				return;
			}

			// Vector desde el ancla del handle (P1 si towardEnd, P2 si no).
			double ax = towardEnd ? p1x : p2x;
			double ay = towardEnd ? p1y : p2y;
			double hx = cx - ax;
			double hy = cy - ay;

			// Sentido de marcha a lo largo del tramo desde el ancla:
			// desde P1 → +seg; desde P2 → −seg (hacia el interior del tramo).
			double dirX = towardEnd ? segDx : -segDx;
			double dirY = towardEnd ? segDy : -segDy;
			double proj = (hx * dirX + hy * dirY) / len2;

			if (proj < 0.0)
			{
				// Invertido: colapsar sobre la cuerda hacia el interior.
				double amount = Math.Min(1.0 / 3.0, 0.5);
				cx = ax + dirX * amount;
				cy = ay + dirY * amount;
			}

			// Tiempo (X) monótono en diagramas marcha: el handle no sale del intervalo [P1x, P2x].
			double xMin = p1x < p2x ? p1x : p2x;
			double xMax = p1x > p2x ? p1x : p2x;
			if (cx < xMin)
			{
				cx = xMin;
			}

			if (cx > xMax)
			{
				cx = xMax;
			}
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

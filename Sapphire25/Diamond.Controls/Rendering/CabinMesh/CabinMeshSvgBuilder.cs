using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Diamond.Controls.Rendering;
using Diamond.Motion;
using Diamond.Project;
using Diamond.Topo;

namespace Diamond.Controls.Rendering.CabinMesh
{
	/// <summary>
	/// Genera el SVG de la malla de cabina y datos de hit-test por circulación.
	/// </summary>
	public static class CabinMeshSvgBuilder
	{
		public sealed class HitSegment
		{
			public HitSegment(Circulation circulation, IReadOnlyList<(double X, double Y)> points)
			{
				Circulation = circulation;
				Points = points;
			}

			public Circulation Circulation { get; }

			public IReadOnlyList<(double X, double Y)> Points { get; }
		}

		public sealed class Result
		{
			public Result(string svgMarkup, IReadOnlyList<HitSegment> hits)
			{
				SvgMarkup = svgMarkup;
				Hits = hits;
			}

			public string SvgMarkup { get; }

			public IReadOnlyList<HitSegment> Hits { get; }
		}

		/// <param name="activeTrainVmaxKmh">
		/// Techo del tren activo (km/h). Limitaciones ≥ este valor no se dibujan.
		/// 0 = sin filtro (p. ej. sin tren seleccionado).
		/// </param>
		/// <param name="currentSpeedKmh">
		/// Velocidad actual (km/h). La línea de tendencia sólo se dibuja si es &gt; 5.
		/// </param>
		public static Result Build(
			CabinMeshLayout layout,
			CabinMeshPalette palette,
			RouteView? view,
			IReadOnlyList<Circulation>? dayCirculations,
			Circulation? active,
			bool nightMode,
			int activeTrainVmaxKmh = 0,
			TopoLayout? topo = null,
			double currentSpeedKmh = 0)
		{
			StringBuilder sb = new StringBuilder(8192);
			List<HitSegment> hits = new List<HitSegment>();

			string w = F(layout.Width);
			string h = F(layout.Height);
			sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" class=\"cabin-mesh-svg\" width=\"")
				.Append(w)
				.Append("\" height=\"")
				.Append(h)
				.Append("\" viewBox=\"0 0 ")
				.Append(w)
				.Append(' ')
				.Append(h)
				.Append("\" preserveAspectRatio=\"none\">");

			// Máscara horizontal: extremos de líneas horizontales → transparente.
			sb.Append("<defs>")
				.Append("<linearGradient id=\"cabinMeshHFade\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"0\">")
				.Append("<stop offset=\"0%\" stop-color=\"#fff\" stop-opacity=\"0\"/>")
				.Append("<stop offset=\"7%\" stop-color=\"#fff\" stop-opacity=\"1\"/>")
				.Append("<stop offset=\"93%\" stop-color=\"#fff\" stop-opacity=\"1\"/>")
				.Append("<stop offset=\"100%\" stop-color=\"#fff\" stop-opacity=\"0\"/>")
				.Append("</linearGradient>")
				.Append("<mask id=\"cabinMeshHMask\" maskUnits=\"userSpaceOnUse\" x=\"0\" y=\"0\" width=\"")
				.Append(w)
				.Append("\" height=\"")
				.Append(h)
				.Append("\">")
				.Append("<rect x=\"0\" y=\"0\" width=\"")
				.Append(w)
				.Append("\" height=\"")
				.Append(h)
				.Append("\" fill=\"url(#cabinMeshHFade)\"/>")
				.Append("</mask>")
				.Append("</defs>");

			// Capas: tiempo → estaciones (fade H) → PK hectométricos → límites V → trenes
			// → PK tren (fade H) → ahora → tendencia → marcador.
			AppendTimeGrid(sb, layout, palette);
			if (view is not null)
			{
				AppendStations(sb, layout, palette, view);
				AppendHectometerPks(sb, layout, palette, view);
				AppendSpeedLimits(sb, layout, palette, view, activeTrainVmaxKmh, nightMode);
			}

			List<(Circulation Cir, bool Active, List<(double TimeSec, long Pk)> Keys)> drawn =
				new List<(Circulation, bool, List<(double, long)>)>();
			if (dayCirculations is not null && view is not null)
			{
				Dictionary<string, RouteView?> viewCache = new Dictionary<string, RouteView?>(
					StringComparer.Ordinal);
				int i = 0;
				while (i < dayCirculations.Count)
				{
					Circulation cir = dayCirculations[i];
					i++;
					bool isActive = active is not null
						&& (ReferenceEquals(cir, active)
							|| string.Equals(cir.Id, active.Id, StringComparison.Ordinal));
					if (isActive)
					{
						continue;
					}

					List<(double TimeSec, long Pk)> keys = BuildProjectedKeys(
						cir, view, topo, viewCache);
					if (keys.Count >= 2)
					{
						drawn.Add((cir, false, keys));
					}
				}

				if (active is not null)
				{
					List<(double TimeSec, long Pk)> activeKeys = BuildProjectedKeys(
						active, view, topo, viewCache);
					if (activeKeys.Count >= 2)
					{
						drawn.Add((active, true, activeKeys));
					}
				}

				int d = 0;
				while (d < drawn.Count)
				{
					AppendCirculationPath(
						sb, hits, layout, palette, drawn[d].Cir, drawn[d].Keys, drawn[d].Active, nightMode);
					d++;
				}

				// Números encima de todas las trazas, anclados al tramo visible.
				d = 0;
				while (d < drawn.Count)
				{
					AppendTrainNumber(
						sb, layout, palette, drawn[d].Cir, drawn[d].Keys, drawn[d].Active, nightMode);
					d++;
				}
			}

			double nowX = layout.XFromTimeSeconds(layout.NowSeconds);

			// PK del tren: horizontal a TrainY, mismos extremos difuminados que las estaciones.
			sb.Append("<g class=\"cabin-mesh-train-pk\" mask=\"url(#cabinMeshHMask)\">")
				.Append("<line x1=\"0\" y1=\"")
				.Append(F(layout.TrainY))
				.Append("\" x2=\"")
				.Append(w)
				.Append("\" y2=\"")
				.Append(F(layout.TrainY))
				.Append("\" stroke=\"")
				.Append(palette.NowLine)
				.Append("\" stroke-width=\"1.8\" opacity=\"0.8\"/>")
				.Append("</g>");

			// Línea de “ahora” (centro X).
			sb.Append("<line class=\"cabin-mesh-now\" x1=\"")
				.Append(F(nowX))
				.Append("\" y1=\"0\" x2=\"")
				.Append(F(nowX))
				.Append("\" y2=\"")
				.Append(h)
				.Append("\" stroke=\"")
				.Append(palette.NowLine)
				.Append("\" stroke-width=\"1.5\" stroke-dasharray=\"4 3\" opacity=\"0.85\"/>");

			AppendTrendLine(sb, layout, palette, currentSpeedKmh);

			// Marcador de posición del tren (intersección ahora × PK).
			sb.Append("<circle class=\"cabin-mesh-train-pos\" cx=\"")
				.Append(F(nowX))
				.Append("\" cy=\"")
				.Append(F(layout.TrainY))
				.Append("\" r=\"5\" fill=\"")
				.Append(palette.NowLine)
				.Append("\" opacity=\"0.9\"/>");

			sb.Append("</svg>");
			return new Result(sb.ToString(), hits);
		}

		/// <summary>
		/// La circulación se dibuja solo si su ViewId / PathSignature coincide con la vista actual
		/// (p. ej. no pintar trenes T3 sobre el eje M1).
		/// </summary>
		public static bool BelongsToView(Circulation cir, RouteView view)
		{
			if (cir is null || view is null)
			{
				return false;
			}

			string viewId = (cir.Asimilation.ViewId ?? string.Empty).Trim();
			string pathSig = (cir.Asimilation.PathSignature ?? string.Empty).Trim();
			string currentId = (view.Id ?? string.Empty).Trim();

			if (viewId.Length > 0
				&& string.Equals(viewId, currentId, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (pathSig.Length > 0
				&& (string.Equals(pathSig, currentId, StringComparison.OrdinalIgnoreCase)
					|| string.Equals(pathSig, view.PathSignature(), StringComparison.OrdinalIgnoreCase)))
			{
				return true;
			}

			// Vista mono-eje M1: aceptar ViewId "M1" o multi que contenga solo ese eje.
			if (view.Legs.Count == 1)
			{
				string axisId = view.Legs[0].Axis.Id;
				if (viewId.Length == 0 && pathSig.Length == 0)
				{
					// Sin ViewId: no asumir; evita mezclar corredores.
					return false;
				}

				if (string.Equals(viewId, axisId, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}

				// Multi-eje "T3+T2" no pertenece a vista solo M1.
				if (viewId.IndexOf('+') >= 0 || viewId.IndexOf('|') >= 0)
				{
					return ViewIdUsesOnlyAxes(viewId, axisId);
				}
			}
			else
			{
				// Vista multi-eje: el ViewId del tren debe usar el mismo conjunto de ejes (orden flexible).
				if (viewId.Length > 0 && ViewIdsShareSameAxes(viewId, currentId))
				{
					return true;
				}
			}

			return false;
		}

		private static bool ViewIdUsesOnlyAxes(string viewId, string singleAxisId)
		{
			string[] parts = viewId.Split(
				new[] { '+', '|', ',', ';' },
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			if (parts.Length != 1)
			{
				return false;
			}

			return string.Equals(parts[0], singleAxisId, StringComparison.OrdinalIgnoreCase);
		}

		private static bool ViewIdsShareSameAxes(string a, string b)
		{
			HashSet<string> setA = SplitAxes(a);
			HashSet<string> setB = SplitAxes(b);
			if (setA.Count == 0 || setB.Count == 0 || setA.Count != setB.Count)
			{
				return false;
			}

			foreach (string id in setA)
			{
				if (!setB.Contains(id))
				{
					return false;
				}
			}

			return true;
		}

		private static HashSet<string> SplitAxes(string viewId)
		{
			HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			string[] parts = viewId.Split(
				new[] { '+', '|', ',', ';' },
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			int i = 0;
			while (i < parts.Length)
			{
				string axisId = RouteViewResolver.AxisIdFromPart(parts[i]);
				if (axisId.Length > 0)
				{
					set.Add(axisId);
				}

				i++;
			}

			return set;
		}

		/// <summary>
		/// Línea de tendencia: punto-raya-punto desde (ahora, PK tren) a la derecha.
		/// Pendiente ∝ velocidad actual. Sólo si v &gt; 5 km/h.
		/// </summary>
		private static void AppendTrendLine(
			StringBuilder sb,
			CabinMeshLayout layout,
			CabinMeshPalette palette,
			double currentSpeedKmh)
		{
			double x0;
			double y0;
			double x1;
			double y1;
			if (!layout.TryGetTrendLine(currentSpeedKmh, out x0, out y0, out x1, out y1))
			{
				return;
			}

			sb.Append("<line class=\"cabin-mesh-trend\" x1=\"")
				.Append(F(x0))
				.Append("\" y1=\"")
				.Append(F(y0))
				.Append("\" x2=\"")
				.Append(F(x1))
				.Append("\" y2=\"")
				.Append(F(y1))
				.Append("\" stroke=\"")
				.Append(palette.NowLine)
				.Append("\" stroke-width=\"1.7\" stroke-linecap=\"round\" ")
				.Append("stroke-dasharray=\"2 5 10 5\" opacity=\"0.9\"/>");
		}

		private static void AppendTimeGrid(StringBuilder sb, CabinMeshLayout layout, CabinMeshPalette palette)
		{
			double min = layout.TimeMinSeconds;
			double max = layout.TimeMaxSeconds;

			// Marcas cada 5 min; resalte en :00 y :30 (medias horas).
			double t0 = Math.Floor(min / 300.0) * 300.0;
			double t = t0;
			while (t <= max + 1.0)
			{
				if (t >= min - 1.0)
				{
					double x = layout.XFromTimeSeconds(t);
					double modHour = ((t % 3600.0) + 3600.0) % 3600.0;
					bool isHour = modHour < 1.0 || Math.Abs(modHour - 3600.0) < 1.0;
					bool isHalf = Math.Abs(modHour - 1800.0) < 1.0;
					bool isMajor = isHour || isHalf;
					string stroke = isMajor ? palette.TimeLineHour : palette.TimeLineMinute;
					string width = isHour ? "1.2" : (isHalf ? "1.0" : "0.7");
					sb.Append("<line x1=\"")
						.Append(F(x))
						.Append("\" y1=\"0\" x2=\"")
						.Append(F(x))
						.Append("\" y2=\"")
						.Append(F(layout.Height))
						.Append("\" stroke=\"")
						.Append(stroke)
						.Append("\" stroke-width=\"")
						.Append(width)
						.Append("\" opacity=\"0.55\"/>");

					if (isMajor)
					{
						int totalMin = ((int)Math.Floor(t / 60.0) % (24 * 60) + 24 * 60) % (24 * 60);
						int hour = totalMin / 60;
						int minute = totalMin % 60;
						sb.Append("<text x=\"")
							.Append(F(x + 3))
							.Append("\" y=\"14\" fill=\"")
							.Append(palette.TimeLabel)
							.Append("\" font-size=\"11\" font-family=\"Segoe UI,sans-serif\">")
							.Append(hour.ToString("00", CultureInfo.InvariantCulture))
							.Append(':')
							.Append(minute.ToString("00", CultureInfo.InvariantCulture))
							.Append("</text>");
					}
				}

				t += 300.0;
			}
		}

		/// <summary>Desplazamiento X de etiquetas de estación (fuera del fade izquierdo ~7%).</summary>
		private const double StationLabelLeftPx = 36.0;

		/// <summary>PK hectométricos, a la derecha de las estaciones.</summary>
		private const double HectometerLabelLeftPx = 72.0;

		/// <summary>Cajas de limitación de velocidad, más a la derecha.</summary>
		private const double SpeedLimitLeftPx = 108.0;

		private const double SpeedBoxWidth = 28.0;
		private const double SpeedBoxHeight = 14.0;

		private static void AppendStations(
			StringBuilder sb,
			CabinMeshLayout layout,
			CabinMeshPalette palette,
			RouteView view)
		{
			// Grupo con máscara: solo las líneas horizontales se difuminan en los extremos.
			sb.Append("<g class=\"cabin-mesh-stations\" mask=\"url(#cabinMeshHMask)\">");
			int i = 0;
			while (i < view.Stations.Count)
			{
				StationOnRoute st = view.Stations[i];
				if (!layout.IsRoutePkVisible(st.PK))
				{
					i++;
					continue;
				}

				double y = layout.YFromRoutePk(st.PK);
				string stroke = (i % 2 == 0) ? palette.StationLinePrimary : palette.StationLineSecondary;
				sb.Append("<line x1=\"0\" y1=\"")
					.Append(F(y))
					.Append("\" x2=\"")
					.Append(F(layout.Width))
					.Append("\" y2=\"")
					.Append(F(y))
					.Append("\" stroke=\"")
					.Append(stroke)
					.Append("\" stroke-width=\"1\" opacity=\"0.65\"/>");

				i++;
			}

			sb.Append("</g>");

			// Etiquetas fuera de la máscara (más a la derecha, legibles).
			i = 0;
			while (i < view.Stations.Count)
			{
				StationOnRoute st = view.Stations[i];
				if (!layout.IsRoutePkVisible(st.PK))
				{
					i++;
					continue;
				}

				double y = layout.YFromRoutePk(st.PK);
				string name = st.Station.Avr.Length > 0 ? st.Station.Avr : st.Station.Name;
				if (name.Length > 0)
				{
					sb.Append("<text x=\"")
						.Append(F(StationLabelLeftPx))
						.Append("\" y=\"")
						.Append(F(y - 3))
						.Append("\" fill=\"")
						.Append(palette.StationLabel)
						.Append("\" font-size=\"10\" font-family=\"Segoe UI,sans-serif\">")
						.Append(System.Security.SecurityElement.Escape(name))
						.Append("</text>");
				}

				i++;
			}
		}

		/// <summary>
		/// Hectómetros del eje físico (PK de vía), no el PK de ruta de la vista.
		/// En trenes descendentes los números bajan al avanzar.
		/// </summary>
		private static void AppendHectometerPks(
			StringBuilder sb,
			CabinMeshLayout layout,
			CabinMeshPalette palette,
			RouteView view)
		{
			sb.Append("<g class=\"cabin-mesh-hectometers\">");
			int li = 0;
			while (li < view.Legs.Count)
			{
				RouteLeg leg = view.Legs[li];
				long axisMin = leg.AxisFromPk < leg.AxisToPk ? leg.AxisFromPk : leg.AxisToPk;
				long axisMax = leg.AxisFromPk > leg.AxisToPk ? leg.AxisFromPk : leg.AxisToPk;
				long h = (axisMin / 100L) * 100L;
				if (h < axisMin)
				{
					h += 100L;
				}

				while (h <= axisMax)
				{
					if (leg.ContainsAxisPk(h)
						&& view.TryMapAxisToRoute(leg.Axis, h, out long routePk)
						&& layout.IsRoutePkVisible(routePk))
					{
						double y = layout.YFromRoutePk(routePk);
						double km = h / 1000.0;
						string label = km.ToString("0.0", CultureInfo.InvariantCulture);
						sb.Append("<text x=\"")
							.Append(F(HectometerLabelLeftPx))
							.Append("\" y=\"")
							.Append(F(y + 3))
							.Append("\" fill=\"")
							.Append(palette.TimeLabel)
							.Append("\" font-size=\"9\" font-family=\"Segoe UI,sans-serif\" opacity=\"0.85\">")
							.Append(label)
							.Append("</text>");
					}

					h += 100L;
				}

				li++;
			}

			sb.Append("</g>");
		}

		/// <summary>
		/// Rectángulos grises con la V de limitación por tramos constantes en la ventana.
		/// Omite límites ≥ Vmax del tren activo (si se indicó).
		/// </summary>
		private static void AppendSpeedLimits(
			StringBuilder sb,
			CabinMeshLayout layout,
			CabinMeshPalette palette,
			RouteView view,
			int activeTrainVmaxKmh,
			bool nightMode)
		{
			long lo = Math.Min(layout.PkBehind, layout.PkAhead);
			long hi = Math.Max(layout.PkBehind, layout.PkAhead);
			long viewLo = Math.Min(view.PK, view.PKEnd);
			long viewHi = Math.Max(view.PK, view.PKEnd);
			long start = Math.Max(lo, viewLo);
			long end = Math.Min(hi, viewHi);
			if (end <= start)
			{
				return;
			}

			// Muestreo cada 25 m y fusión de tramos con la misma V efectiva.
			const long Step = 25L;
			List<(long From, long To, int Speed)> segments = new List<(long, long, int)>();
			int? runSpeed = null;
			long runFrom = start;
			long pk = start;
			while (pk <= end)
			{
				int? v = view.GetEffectiveSpeedLimit(pk);
				// Si solo es el Vmax del eje genérico y no hay capa, aún se muestra si < techo tren.
				if (!v.HasValue)
				{
					if (runSpeed.HasValue)
					{
						segments.Add((runFrom, pk, runSpeed.Value));
						runSpeed = null;
					}
				}
				else if (activeTrainVmaxKmh > 0 && v.Value >= activeTrainVmaxKmh)
				{
					// No representar techos ≥ Vmax del tren.
					if (runSpeed.HasValue)
					{
						segments.Add((runFrom, pk, runSpeed.Value));
						runSpeed = null;
					}
				}
				else if (!runSpeed.HasValue)
				{
					runSpeed = v.Value;
					runFrom = pk;
				}
				else if (runSpeed.Value != v.Value)
				{
					segments.Add((runFrom, pk, runSpeed.Value));
					runSpeed = v.Value;
					runFrom = pk;
				}

				if (pk == end)
				{
					break;
				}

				long next = pk + Step;
				if (next > end)
				{
					next = end;
				}

				pk = next;
			}

			if (runSpeed.HasValue)
			{
				segments.Add((runFrom, end, runSpeed.Value));
			}

			string boxFillFixed = nightMode ? "#e8e0d8" : "#6a6a6a";
			string boxStrokeFixed = nightMode ? "#c9a27a" : "#4a4a4a";
			string textFillFixed = nightMode ? "#000000" : "#ffffff";

			sb.Append("<g class=\"cabin-mesh-speed-limits\">");
			int i = 0;
			while (i < segments.Count)
			{
				(long from, long to, int speed) = segments[i];
				long mid = (from + to) / 2L;
				if (!layout.IsRoutePkVisible(mid))
				{
					i++;
					continue;
				}

				double y0 = layout.YFromRoutePk(from);
				double y1 = layout.YFromRoutePk(to);
				double yTop = y0 < y1 ? y0 : y1;
				double yBot = y0 > y1 ? y0 : y1;
				double spanPx = yBot - yTop;
				double boxH = spanPx < SpeedBoxHeight ? SpeedBoxHeight : spanPx;
				double boxY = spanPx < SpeedBoxHeight
					? ((yTop + yBot) * 0.5 - boxH * 0.5)
					: yTop;
				double textY = boxY + boxH * 0.5 + 3.5;
				double boxX = SpeedLimitLeftPx;

				int? tempSpeed = view.GetTemporarySpeedLimit(mid);
				string boxFill = boxFillFixed;
				string boxStroke = boxStrokeFixed;
				string textFill = textFillFixed;
				if (tempSpeed.HasValue)
				{
					boxFill = TemporaryLimitMeshColors.ForSpeed(tempSpeed.Value);
					boxStroke = tempSpeed.Value < TemporaryLimitMeshColors.SpeedThresholdKmh
						? "#c43c00"
						: "#b88600";
					textFill = "#000000";
				}

				sb.Append("<rect x=\"")
					.Append(F(boxX))
					.Append("\" y=\"")
					.Append(F(boxY))
					.Append("\" width=\"")
					.Append(F(SpeedBoxWidth))
					.Append("\" height=\"")
					.Append(F(boxH))
					.Append("\" rx=\"2\" ry=\"2\" fill=\"")
					.Append(boxFill)
					.Append("\" stroke=\"")
					.Append(boxStroke)
					.Append("\" stroke-width=\"0.6\" opacity=\"0.9\"/>");

				sb.Append("<text x=\"")
					.Append(F(boxX + SpeedBoxWidth * 0.5))
					.Append("\" y=\"")
					.Append(F(textY))
					.Append("\" text-anchor=\"middle\" fill=\"")
					.Append(textFill)
					.Append("\" font-size=\"9\" font-weight=\"700\" font-family=\"Segoe UI,sans-serif\">")
					.Append(speed.ToString(CultureInfo.InvariantCulture))
					.Append("</text>");

				i++;
			}

			sb.Append("</g>");
		}

		private static void AppendCirculationPath(
			StringBuilder sb,
			List<HitSegment> hits,
			CabinMeshLayout layout,
			CabinMeshPalette palette,
			Circulation cir,
			List<(double TimeSec, long Pk)> keys,
			bool isActive,
			bool nightMode)
		{
			string color = palette.ResolveTrainColor(cir.Color, nightMode);
			double opacity = isActive ? 1.0 : palette.TrainInactiveOpacity;
			double strokeW = isActive ? 2.6 : 1.6;

			List<MeshTrainPathBuilder.Point> past = new List<MeshTrainPathBuilder.Point>();
			List<MeshTrainPathBuilder.Point> current = new List<MeshTrainPathBuilder.Point>();
			List<MeshTrainPathBuilder.Point> future = new List<MeshTrainPathBuilder.Point>();
			List<(double X, double Y)> hitPts = new List<(double, double)>();

			int s = 0;
			while (s < keys.Count)
			{
				double t = keys[s].TimeSec;
				long pk = keys[s].Pk;
				double x = layout.XFromTimeSeconds(t);
				double y = layout.YFromRoutePk(pk);
				MeshTrainPathBuilder.Point pt = new MeshTrainPathBuilder.Point(x, y);
				hitPts.Add((x, y));

				if (!isActive)
				{
					future.Add(pt);
				}
				else if (t < layout.NowSeconds - 1.0)
				{
					past.Add(pt);
				}
				else if (t > layout.NowSeconds + 1.0)
				{
					if (future.Count == 0)
					{
						if (current.Count > 0)
						{
							future.Add(current[current.Count - 1]);
						}
						else if (past.Count > 0)
						{
							future.Add(past[past.Count - 1]);
						}
					}

					future.Add(pt);
				}
				else
				{
					if (current.Count == 0 && past.Count > 0)
					{
						current.Add(past[past.Count - 1]);
					}

					current.Add(pt);
				}

				s++;
			}

			if (!isActive)
			{
				AppendBezierPath(sb, future, color, strokeW, opacity, dashed: false);
			}
			else
			{
				AppendBezierPath(sb, past, color, strokeW, opacity, dashed: true);
				AppendBezierPath(sb, current, color, strokeW, opacity, dashed: true);
				AppendBezierPath(sb, future, color, strokeW, opacity, dashed: false);
			}

			if (hitPts.Count >= 2)
			{
				hits.Add(new HitSegment(cir, hitPts));
			}
		}

		private static void AppendTrainNumber(
			StringBuilder sb,
			CabinMeshLayout layout,
			CabinMeshPalette palette,
			Circulation cir,
			List<(double TimeSec, long Pk)> keys,
			bool isActive,
			bool nightMode)
		{
			string label = cir.HasServiceNumber ? cir.ServiceNumber : cir.Id;
			if (string.IsNullOrEmpty(label) || keys.Count == 0)
			{
				return;
			}

			List<(double X, double Y)> pts = new List<(double, double)>();
			int i = 0;
			while (i < keys.Count)
			{
				double x = layout.XFromTimeSeconds(keys[i].TimeSec);
				double y = layout.YFromRoutePk(keys[i].Pk);
				pts.Add((x, y));
				i++;
			}

			int topIdx;
			int botIdx;
			PickInsetLabelPoints(pts, layout.Width, layout.Height, out topIdx, out botIdx);
			if (topIdx < 0 && botIdx < 0)
			{
				return;
			}

			string color = palette.ResolveTrainColor(cir.Color, nightMode);
			string halo = nightMode ? "#000000" : "#ffffff";
			double opacity = isActive ? 1.0 : Math.Max(palette.TrainInactiveOpacity, 0.88);
			if (topIdx >= 0)
			{
				EmitTrainNumber(
					sb, pts[topIdx].X, pts[topIdx].Y, label, color, halo, opacity, isActive);
			}

			if (botIdx >= 0 && botIdx != topIdx)
			{
				double dx = pts[botIdx].X - (topIdx >= 0 ? pts[topIdx].X : pts[botIdx].X);
				double dy = pts[botIdx].Y - (topIdx >= 0 ? pts[topIdx].Y : pts[botIdx].Y);
				if (topIdx < 0 || (dx * dx + dy * dy) > 28.0 * 28.0)
				{
					EmitTrainNumber(
						sb, pts[botIdx].X, pts[botIdx].Y, label, color, halo, opacity, isActive);
				}
			}
		}

		/// <summary>
		/// Elige vértices en el 2.º/3.º segmento desde el extremo superior
		/// y en el penúltimo/antepenúltimo desde el inferior, fuera del fade.
		/// </summary>
		private static void PickInsetLabelPoints(
			List<(double X, double Y)> pts,
			double width,
			double height,
			out int topIdx,
			out int botIdx)
		{
			topIdx = -1;
			botIdx = -1;
			if (pts.Count == 0)
			{
				return;
			}

			// El host difumina 0–14 % y 86–100 %; nos quedamos en la banda opaca.
			double yMin = height * 0.16;
			double yMax = height * 0.84;
			List<int> vis = new List<int>();
			int i = 0;
			while (i < pts.Count)
			{
				double x = pts[i].X;
				double y = pts[i].Y;
				if (x >= 0.0 && x <= width && y >= yMin && y <= yMax)
				{
					vis.Add(i);
				}

				i++;
			}

			if (vis.Count == 0)
			{
				i = 0;
				while (i < pts.Count)
				{
					if (pts[i].X >= 0.0 && pts[i].X <= width
						&& pts[i].Y >= 0.0 && pts[i].Y <= height)
					{
						vis.Add(i);
					}

					i++;
				}
			}

			if (vis.Count == 0)
			{
				return;
			}

			int fromStart = InsetAlong(vis.Count, fromEnd: false);
			int fromEnd = InsetAlong(vis.Count, fromEnd: true);
			int startPt = vis[fromStart];
			int endPt = vis[fromEnd];
			if (pts[startPt].Y <= pts[endPt].Y)
			{
				topIdx = startPt;
				botIdx = endPt;
			}
			else
			{
				topIdx = endPt;
				botIdx = startPt;
			}
		}

		/// <summary>
		/// 3.er vértice (2.º–3.er segmento) si hay holgura; si no, el 2.º; si no, el extremo.
		/// </summary>
		private static int InsetAlong(int count, bool fromEnd)
		{
			int step;
			if (count >= 7)
			{
				step = 3;
			}
			else if (count >= 5)
			{
				step = 2;
			}
			else if (count >= 3)
			{
				step = 1;
			}
			else
			{
				step = 0;
			}

			if (fromEnd)
			{
				int i = count - 1 - step;
				return i < 0 ? 0 : i;
			}

			return step >= count ? count - 1 : step;
		}

		private static void EmitTrainNumber(
			StringBuilder sb,
			double x,
			double y,
			string label,
			string color,
			string halo,
			double opacity,
			bool isActive)
		{
			sb.Append("<text class=\"cabin-mesh-train-num\" x=\"")
				.Append(F(x + 5))
				.Append("\" y=\"")
				.Append(F(y - 5))
				.Append("\" fill=\"")
				.Append(color)
				.Append("\" stroke=\"")
				.Append(halo)
				.Append("\" stroke-width=\"3\" paint-order=\"stroke\" font-size=\"")
				.Append(isActive ? "13" : "11")
				.Append("\" font-weight=\"")
				.Append(isActive ? "700" : "600")
				.Append("\" font-family=\"Segoe UI,sans-serif\" opacity=\"")
				.Append(F(opacity))
				.Append("\">")
				.Append(System.Security.SecurityElement.Escape(label))
				.Append("</text>");
		}

		/// <summary>
		/// Muestras en PK de la vista de pantalla. Proyecta trenes de otros
		/// corredores (p. ej. T3 sobre T3+T2) y omite tramos que no solapan.
		/// </summary>
		private static List<(double TimeSec, long Pk)> BuildProjectedKeys(
			Circulation cir,
			RouteView display,
			TopoLayout? topo,
			Dictionary<string, RouteView?> viewCache)
		{
			List<(double TimeSec, long Pk)> keys = new List<(double, long)>();
			if (cir.Calls.Count < 1)
			{
				return keys;
			}

			RouteView? trainView = ResolveTrainView(cir, display, topo, viewCache);
			List<(TimedCall Call, long DisplayPk)> mapped = new List<(TimedCall, long)>();
			int i = 0;
			while (i < cir.Calls.Count)
			{
				TimedCall call = cir.Calls[i];
				long displayPk;
				if (TryMapCallToDisplay(display, trainView, call, out displayPk))
				{
					mapped.Add((call, displayPk));
				}

				i++;
			}

			i = 0;
			while (i < mapped.Count)
			{
				TimedCall c = mapped[i].Call;
				long pk = mapped[i].DisplayPk;
				keys.Add((c.Arrival.TotalSeconds, pk));
				if (c.Departure > c.Arrival)
				{
					keys.Add((c.Departure.TotalSeconds, pk));
				}

				if (i + 1 < mapped.Count)
				{
					TimedCall next = mapped[i + 1].Call;
					long pk1 = mapped[i + 1].DisplayPk;
					double t0 = c.Departure.TotalSeconds;
					double t1 = next.Arrival.TotalSeconds;
					double dt = t1 - t0;
					if (dt > 1.0)
					{
						int n = (int)Math.Max(2, Math.Floor(dt / 20.0));
						if (n > 24)
						{
							n = 24;
						}

						int k = 1;
						while (k < n)
						{
							double u = (double)k / n;
							double t = t0 + u * dt;
							long ipk = pk + (long)Math.Round((pk1 - pk) * u);
							keys.Add((t, ipk));
							k++;
						}
					}
				}

				i++;
			}

			return keys;
		}

		private static RouteView? ResolveTrainView(
			Circulation cir,
			RouteView display,
			TopoLayout? topo,
			Dictionary<string, RouteView?> cache)
		{
			string sig = (cir.Asimilation.PathSignature ?? string.Empty).Trim();
			string vid = (cir.Asimilation.ViewId ?? string.Empty).Trim();
			if (sig.Length > 0
				&& string.Equals(sig, display.PathSignature(), StringComparison.Ordinal))
			{
				return display;
			}

			string key = sig.Length > 0 ? sig : vid;
			if (key.Length > 0 && cache.TryGetValue(key, out RouteView? cached))
			{
				return cached;
			}

			RouteView? resolved = null;
			if (topo is not null)
			{
				resolved = RouteViewResolver.TryForCabinCirculation(
					topo,
					vid,
					sig,
					cir.Origin.Id,
					cir.Destination.Id,
					cir.Origin.Avr,
					cir.Destination.Avr);
			}

			if (key.Length > 0)
			{
				cache[key] = resolved;
			}

			return resolved;
		}

		private static bool TryMapCallToDisplay(
			RouteView display,
			RouteView? trainView,
			TimedCall call,
			out long displayPk)
		{
			displayPk = 0;
			if (trainView is not null
				&& (ReferenceEquals(trainView, display) || trainView.IsSamePath(display)))
			{
				displayPk = call.Pk;
				return true;
			}

			StationOnRoute? onDisplay = display.FindStationByRef(
				call.Station.Id,
				call.Station.Avr,
				call.Station.Name);
			if (onDisplay is not null)
			{
				displayPk = onDisplay.PK;
				return true;
			}

			if (trainView is not null
				&& display.TryMapRoutePkFrom(trainView, call.Pk, out displayPk))
			{
				return true;
			}

			return false;
		}

		private static void AppendBezierPath(
			StringBuilder sb,
			List<MeshTrainPathBuilder.Point> points,
			string color,
			double strokeW,
			double opacity,
			bool dashed)
		{
			if (points is null || points.Count < 2)
			{
				return;
			}

			string d = MeshTrainPathBuilder.ToSvgPath(points, useSpline: true);
			if (d.Length == 0)
			{
				return;
			}

			sb.Append("<path d=\"")
				.Append(d)
				.Append("\" fill=\"none\" stroke=\"")
				.Append(color)
				.Append("\" stroke-width=\"")
				.Append(F(strokeW))
				.Append("\" stroke-linecap=\"round\" stroke-linejoin=\"round\" opacity=\"")
				.Append(F(opacity))
				.Append('"');
			if (dashed)
			{
				sb.Append(" stroke-dasharray=\"4 5\"");
			}

			sb.Append("/>");
		}

		/// <summary>
		/// Circulación más cercana al punto de pantalla, o null si ninguna a &lt; maxDist.
		/// </summary>
		public static Circulation? HitTest(
			IReadOnlyList<HitSegment> hits,
			double x,
			double y,
			double maxDistPx = 14.0)
		{
			Circulation? best = null;
			double bestD = maxDistPx;
			int i = 0;
			while (i < hits.Count)
			{
				HitSegment seg = hits[i];
				double d = MinDistanceToPolyline(seg.Points, x, y);
				if (d < bestD)
				{
					bestD = d;
					best = seg.Circulation;
				}

				i++;
			}

			return best;
		}

		private static double MinDistanceToPolyline(IReadOnlyList<(double X, double Y)> pts, double x, double y)
		{
			double best = double.PositiveInfinity;
			int i = 0;
			while (i < pts.Count - 1)
			{
				double d = DistPointToSegment(x, y, pts[i].X, pts[i].Y, pts[i + 1].X, pts[i + 1].Y);
				if (d < best)
				{
					best = d;
				}

				i++;
			}

			return best;
		}

		private static double DistPointToSegment(
			double px,
			double py,
			double x1,
			double y1,
			double x2,
			double y2)
		{
			double dx = x2 - x1;
			double dy = y2 - y1;
			double len2 = dx * dx + dy * dy;
			if (len2 < 1e-9)
			{
				double ex = px - x1;
				double ey = py - y1;
				return Math.Sqrt(ex * ex + ey * ey);
			}

			double t = ((px - x1) * dx + (py - y1) * dy) / len2;
			t = Math.Clamp(t, 0.0, 1.0);
			double qx = x1 + t * dx;
			double qy = y1 + t * dy;
			double ox = px - qx;
			double oy = py - qy;
			return Math.Sqrt(ox * ox + oy * oy);
		}

		private static string F(double v)
		{
			return v.ToString("0.##", CultureInfo.InvariantCulture);
		}
	}
}

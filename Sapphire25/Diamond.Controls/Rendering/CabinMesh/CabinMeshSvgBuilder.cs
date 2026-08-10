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
		public static Result Build(
			CabinMeshLayout layout,
			CabinMeshPalette palette,
			RouteView? view,
			IReadOnlyList<Circulation>? dayCirculations,
			Circulation? active,
			bool nightMode,
			int activeTrainVmaxKmh = 0)
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

			// Capas: tiempo → estaciones (fade H) → PK hectométricos → límites V → trenes → ahora.
			AppendTimeGrid(sb, layout, palette);
			if (view is not null)
			{
				AppendStations(sb, layout, palette, view);
				AppendHectometerPks(sb, layout, palette, view);
				AppendSpeedLimits(sb, layout, palette, view, activeTrainVmaxKmh);
			}

			if (dayCirculations is not null && view is not null)
			{
				int i = 0;
				while (i < dayCirculations.Count)
				{
					Circulation cir = dayCirculations[i];
					if (!BelongsToView(cir, view))
					{
						i++;
						continue;
					}

					bool isActive = active is not null && ReferenceEquals(cir, active);
					if (!isActive && active is not null
						&& string.Equals(cir.Id, active.Id, StringComparison.Ordinal))
					{
						isActive = true;
					}

					if (!isActive)
					{
						AppendCirculation(sb, hits, layout, palette, cir, isActive: false, nightMode);
					}

					i++;
				}

				// Tren activo solo si pertenece a la vista (si no, no pintar “T3 sobre M1”).
				if (active is not null && BelongsToView(active, view))
				{
					AppendCirculation(sb, hits, layout, palette, active, isActive: true, nightMode);
				}
			}

			// Línea de “ahora” (centro X).
			double nowX = layout.XFromTimeSeconds(layout.NowSeconds);
			sb.Append("<line class=\"cabin-mesh-now\" x1=\"")
				.Append(F(nowX))
				.Append("\" y1=\"0\" x2=\"")
				.Append(F(nowX))
				.Append("\" y2=\"")
				.Append(h)
				.Append("\" stroke=\"")
				.Append(palette.NowLine)
				.Append("\" stroke-width=\"1.5\" stroke-dasharray=\"4 3\" opacity=\"0.85\"/>");

			// Marcador de posición del tren (centro geométrico del control en Y=TrainY).
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
				&& string.Equals(pathSig, currentId, StringComparison.OrdinalIgnoreCase))
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
				if (parts[i].Length > 0)
				{
					set.Add(parts[i]);
				}

				i++;
			}

			return set;
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
		/// Una etiqueta de PK por hectómetro visible (p. ej. 3.3, 3.4, 3.5).
		/// </summary>
		private static void AppendHectometerPks(
			StringBuilder sb,
			CabinMeshLayout layout,
			CabinMeshPalette palette,
			RouteView view)
		{
			long lo = Math.Min(layout.PkBehind, layout.PkAhead);
			long hi = Math.Max(layout.PkBehind, layout.PkAhead);

			// Alinear al hectómetro inferior (múltiplo de 100 m).
			long pk = (lo / 100L) * 100L;
			if (pk < lo)
			{
				pk += 100L;
			}

			// Rango del eje de la vista (no dibujar fuera del recorrido físico).
			long viewLo = Math.Min(view.PK, view.PKEnd);
			long viewHi = Math.Max(view.PK, view.PKEnd);

			sb.Append("<g class=\"cabin-mesh-hectometers\">");
			while (pk <= hi)
			{
				if (pk >= viewLo && pk <= viewHi && layout.IsRoutePkVisible(pk))
				{
					double y = layout.YFromRoutePk(pk);
					// km con un decimal: 3300 m → "3.3"
					double km = pk / 1000.0;
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

				pk += 100L;
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
			int activeTrainVmaxKmh)
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

			string boxFill = nightModeGray(palette);
			string boxStroke = palette.TimeLineMinute;
			string textFill = palette.StationLabel;

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

				// Altura del tramo en pantalla (para no solapar cajas si el tramo es corto).
				double y0 = layout.YFromRoutePk(from);
				double y1 = layout.YFromRoutePk(to);
				double yMid = layout.YFromRoutePk(mid);
				double spanPx = Math.Abs(y1 - y0);
				if (spanPx < SpeedBoxHeight * 0.55)
				{
					// Tramo muy corto: aún se pinta la caja centrada.
				}

				double boxY = yMid - SpeedBoxHeight * 0.5;
				double boxX = SpeedLimitLeftPx;

				sb.Append("<rect x=\"")
					.Append(F(boxX))
					.Append("\" y=\"")
					.Append(F(boxY))
					.Append("\" width=\"")
					.Append(F(SpeedBoxWidth))
					.Append("\" height=\"")
					.Append(F(SpeedBoxHeight))
					.Append("\" rx=\"2\" ry=\"2\" fill=\"")
					.Append(boxFill)
					.Append("\" stroke=\"")
					.Append(boxStroke)
					.Append("\" stroke-width=\"0.6\" opacity=\"0.88\"/>");

				sb.Append("<text x=\"")
					.Append(F(boxX + SpeedBoxWidth * 0.5))
					.Append("\" y=\"")
					.Append(F(yMid + 3.5))
					.Append("\" text-anchor=\"middle\" fill=\"")
					.Append(textFill)
					.Append("\" font-size=\"9\" font-weight=\"600\" font-family=\"Segoe UI,sans-serif\">")
					.Append(speed.ToString(CultureInfo.InvariantCulture))
					.Append("</text>");

				i++;
			}

			sb.Append("</g>");
		}

		private static string nightModeGray(CabinMeshPalette palette)
		{
			// Fondo de caja: gris medio legible en día y noche.
			// (La paleta no trae un “panel fill”; usamos un gris fijo neutro.)
			return "#8a8a8a";
		}

		private static void AppendCirculation(
			StringBuilder sb,
			List<HitSegment> hits,
			CabinMeshLayout layout,
			CabinMeshPalette palette,
			Circulation cir,
			bool isActive,
			bool nightMode)
		{
			if (cir.Calls.Count < 1)
			{
				return;
			}

			// Claves densas (llegada/salida + muestras intermedias) → Bezier Catmull-Rom.
			List<(double TimeSec, long Pk)> keys = BuildDenseKeys(cir);
			if (keys.Count < 2)
			{
				return;
			}

			string color = palette.ResolveTrainColor(cir.Color, nightMode);
			double opacity = isActive ? 1.0 : palette.TrainInactiveOpacity;
			double strokeW = isActive ? 2.6 : 1.6;

			// Tres tramos estilísticos del tren activo: pasado / actual / futuro.
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
					future.Add(pt); // reutilizamos "future" como trazo completo sólido
				}
				else if (t < layout.NowSeconds - 1.0)
				{
					past.Add(pt);
				}
				else if (t > layout.NowSeconds + 1.0)
				{
					// Empalme: copiar último punto del tramo actual/pasado.
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

			// Etiqueta del tren.
			if (hitPts.Count > 0)
			{
				string label = cir.HasServiceNumber ? cir.ServiceNumber : cir.Id;
				if (label.Length > 0)
				{
					int mid = hitPts.Count / 2;
					sb.Append("<text x=\"")
						.Append(F(hitPts[mid].X + 4))
						.Append("\" y=\"")
						.Append(F(hitPts[mid].Y - 4))
						.Append("\" fill=\"")
						.Append(color)
						.Append("\" font-size=\"")
						.Append(isActive ? "12" : "10")
						.Append("\" font-weight=\"")
						.Append(isActive ? "600" : "400")
						.Append("\" font-family=\"Segoe UI,sans-serif\" opacity=\"")
						.Append(F(opacity))
						.Append("\">")
						.Append(System.Security.SecurityElement.Escape(label))
						.Append("</text>");
				}
			}
		}

		/// <summary>
		/// Muestras densas a lo largo de la circulación (paradas + interpolación del trayecto).
		/// </summary>
		private static List<(double TimeSec, long Pk)> BuildDenseKeys(Circulation cir)
		{
			List<(double TimeSec, long Pk)> keys = new List<(double, long)>();
			int i = 0;
			while (i < cir.Calls.Count)
			{
				TimedCall c = cir.Calls[i];
				keys.Add((c.Arrival.TotalSeconds, c.Pk));
				if (c.Departure > c.Arrival)
				{
					keys.Add((c.Departure.TotalSeconds, c.Pk));
				}

				// Interpolación hacia la siguiente parada (trayecto en marcha).
				if (i + 1 < cir.Calls.Count)
				{
					TimedCall next = cir.Calls[i + 1];
					double t0 = c.Departure.TotalSeconds;
					double t1 = next.Arrival.TotalSeconds;
					long pk0 = c.Pk;
					long pk1 = next.Pk;
					double dt = t1 - t0;
					if (dt > 1.0)
					{
						// ~1 muestra cada 20 s, mínimo 2 intermedias si el tramo es largo.
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
							long pk = pk0 + (long)Math.Round((pk1 - pk0) * u);
							keys.Add((t, pk));
							k++;
						}
					}
				}

				i++;
			}

			return keys;
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

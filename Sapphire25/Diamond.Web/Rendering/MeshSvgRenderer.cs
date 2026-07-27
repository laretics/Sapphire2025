using System.Globalization;
using System.Text;
using Diamond.Motion;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Web.Rendering
{
	/// <summary>
	/// SVG de malla: chrome fijo (estaciones, reloj, franjas V/#) + plot recortado
	/// a la ventana de datos (tiempo × PK) actualmente visible.
	/// </summary>
	public static class MeshSvgRenderer
	{
		public static string Render(
			Mesh mesh,
			Axis axis,
			TimeSpan timeStart,
			TimeSpan timeEnd,
			long pkMin,
			long pkMax,
			int width = 1200,
			int height = 720,
			bool showCantonOccupations = true)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(CultureInfo.InvariantCulture,
				$"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">");
			sb.Append(RenderContent(mesh, axis, timeStart, timeEnd, pkMin, pkMax, width, height, showCantonOccupations));
			sb.Append("</svg>");
			return sb.ToString();
		}

		/// <summary>
		/// Contenido SVG completo (chrome + plot). Sin transform de pan/zoom:
		/// la ventana se controla con timeStart/timeEnd/pkMin/pkMax.
		/// </summary>
		public static string RenderContent(
			Mesh mesh,
			Axis axis,
			TimeSpan timeStart,
			TimeSpan timeEnd,
			long pkMin,
			long pkMax,
			int width = 1200,
			int height = 720,
			bool showCantonOccupations = true)
		{
			if (mesh is null)
			{
				throw new ArgumentNullException(nameof(mesh));
			}

			if (axis is null)
			{
				throw new ArgumentNullException(nameof(axis));
			}

			NormalizePkRange(ref pkMin, ref pkMax);

			double plotLeft = MeshSvgLayout.PlotLeft;
			double plotTop = MeshSvgLayout.PlotTop;
			double plotW = MeshSvgLayout.PlotWidth(width);
			double plotH = MeshSvgLayout.PlotHeight(height);
			double speedStripX = MeshSvgLayout.SpeedStripX;
			double trackStripX = MeshSvgLayout.TrackStripX;
			double stripW = MeshSvgLayout.StripWidth;

			double t0 = timeStart.TotalSeconds;
			double t1 = timeEnd.TotalSeconds;
			if (t1 <= t0)
			{
				t1 = t0 + 3600;
			}

			StringBuilder sb = new StringBuilder();
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"0\" y=\"0\" width=\"{width}\" height=\"{height}\" fill=\"#0f1419\"/>");

			// —— Franjas V y # (solo PK visible) ——
			List<SpeedBand> speedBands = BuildSpeedBands(axis, pkMin, pkMax);
			SortedSet<int> speedsUsed = new SortedSet<int>();
			int bi = 0;
			while (bi < speedBands.Count)
			{
				SpeedBand band = speedBands[bi];
				speedsUsed.Add(band.SpeedKmh);
				AppendPkBandRect(
					sb, speedStripX, stripW,
					band.PkStart, band.PkEnd, pkMin, pkMax, plotTop, plotH,
					SpeedToColor(band.SpeedKmh),
					band.SpeedKmh + " km/h · " + FormatPk(band.PkStart) + "–" + FormatPk(band.PkEnd));
				bi++;
			}

			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{speedStripX}\" y=\"{plotTop}\" width=\"{stripW}\" height=\"{plotH}\" fill=\"none\" stroke=\"#64748b\" stroke-width=\"1\"/>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<text x=\"{speedStripX + stripW / 2}\" y=\"{plotTop - 8}\" fill=\"#94a3b8\" font-size=\"9\" font-family=\"Segoe UI,sans-serif\" text-anchor=\"middle\">V</text>");

			List<TrackBand> trackBands = BuildTrackBands(axis, pkMin, pkMax);
			int ti = 0;
			while (ti < trackBands.Count)
			{
				TrackBand band = trackBands[ti];
				string trackTitle = band.TrackCount >= 2
					? "Doble vía (" + band.TrackCount + ") · " + FormatPk(band.PkStart) + "–" + FormatPk(band.PkEnd)
					: "Vía única · " + FormatPk(band.PkStart) + "–" + FormatPk(band.PkEnd);
				AppendPkBandRect(
					sb, trackStripX, stripW,
					band.PkStart, band.PkEnd, pkMin, pkMax, plotTop, plotH,
					TrackCountToColor(band.TrackCount),
					trackTitle);
				ti++;
			}

			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{trackStripX}\" y=\"{plotTop}\" width=\"{stripW}\" height=\"{plotH}\" fill=\"none\" stroke=\"#64748b\" stroke-width=\"1\"/>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<text x=\"{trackStripX + stripW / 2}\" y=\"{plotTop - 8}\" fill=\"#94a3b8\" font-size=\"9\" font-family=\"Segoe UI,sans-serif\" text-anchor=\"middle\">#</text>");

			// —— Plot (fondo + clip) ——
			string clipId = "plotClip";
			sb.Append(CultureInfo.InvariantCulture,
				$"<defs><clipPath id=\"{clipId}\"><rect x=\"{plotLeft}\" y=\"{plotTop}\" width=\"{plotW}\" height=\"{plotH}\"/></clipPath></defs>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{plotLeft}\" y=\"{plotTop}\" width=\"{plotW}\" height=\"{plotH}\" fill=\"#1a2332\" stroke=\"#3d4f66\"/>");

			sb.Append(CultureInfo.InvariantCulture, $"<g clip-path=\"url(#{clipId})\">");

			// Grid horario (según ventana visible)
			DrawTimeGrid(sb, t0, t1, plotLeft, plotTop, plotW, plotH);

			// Ocupaciones de cantón
			if (showCantonOccupations)
			{
				DrawOccupations(sb, mesh, axis, pkMin, pkMax, t0, t1, plotLeft, plotTop, plotW, plotH);
			}

			// Circulaciones
			int drawn = DrawCirculations(sb, mesh, axis, pkMin, pkMax, t0, t1, plotLeft, plotTop, plotW, plotH);

			sb.Append("</g>");

			// —— Regla de estaciones (fija a la izquierda; contenido según PK visible) ——
			DrawStationRuler(sb, axis, pkMin, pkMax, plotLeft, plotTop, plotW, plotH, speedStripX);

			// —— Regla de tiempo (fija abajo; contenido según tiempo visible) ——
			DrawTimeRuler(sb, t0, t1, plotLeft, plotTop, plotW, plotH, height);

			// Marcos plot encima del clip
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{plotLeft}\" y=\"{plotTop}\" width=\"{plotW}\" height=\"{plotH}\" fill=\"none\" stroke=\"#3d4f66\" stroke-width=\"1.2\"/>");

			// Leyendas
			DrawSpeedLegend(sb, speedsUsed, plotLeft + plotW - 8, plotTop + 8);
			DrawTrackLegend(sb, plotLeft + plotW - 8, plotTop + 8 + 12 + Math.Max(1, speedsUsed.Count) * 14 + 16);

			string title = axis.Id + " " + axis.Name
				+ " — " + drawn + " circ. visibles"
				+ " · t " + FormatClock(TimeSpan.FromSeconds(t0)) + "–" + FormatClock(TimeSpan.FromSeconds(t1))
				+ " · PK " + FormatPk(pkMin) + "–" + FormatPk(pkMax);

			sb.Append(CultureInfo.InvariantCulture,
				$"<text x=\"{plotLeft}\" y=\"20\" fill=\"#e2e8f0\" font-size=\"13\" font-family=\"Segoe UI,sans-serif\">{Escape(title)}</text>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<text x=\"{(plotLeft + plotLeft + plotW) / 2}\" y=\"{height - 4}\" fill=\"#cbd5e1\" font-size=\"12\" font-family=\"Segoe UI,sans-serif\" text-anchor=\"middle\">Tiempo</text>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<text x=\"12\" y=\"{plotTop + plotH / 2}\" fill=\"#cbd5e1\" font-size=\"12\" font-family=\"Segoe UI,sans-serif\" text-anchor=\"middle\" transform=\"rotate(-90 12,{plotTop + plotH / 2})\">Estaciones</text>");

			return sb.ToString();
		}

		private static void DrawTimeGrid(
			StringBuilder sb,
			double t0,
			double t1,
			double plotLeft,
			double plotTop,
			double plotW,
			double plotH)
		{
			double spanHours = (t1 - t0) / 3600.0;
			double stepHours = ChooseTimeStepHours(spanHours);
			double stepSec = stepHours * 3600.0;
			double first = Math.Ceiling(t0 / stepSec) * stepSec;

			double t = first;
			while (t <= t1 + 0.01)
			{
				double x = plotLeft + (t - t0) / (t1 - t0) * plotW;
				bool major = Math.Abs(t / 3600.0 - Math.Round(t / 3600.0)) < 1e-6
					|| stepHours >= 1.0;
				string stroke = major ? "#334155" : "#243041";
				sb.Append(CultureInfo.InvariantCulture,
					$"<line x1=\"{x}\" y1=\"{plotTop}\" x2=\"{x}\" y2=\"{plotTop + plotH}\" stroke=\"{stroke}\" stroke-width=\"1\"/>");
				t += stepSec;
			}
		}

		private static void DrawTimeRuler(
			StringBuilder sb,
			double t0,
			double t1,
			double plotLeft,
			double plotTop,
			double plotW,
			double plotH,
			int svgHeight)
		{
			// Banda fija bajo el plot
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{plotLeft}\" y=\"{plotTop + plotH}\" width=\"{plotW}\" height=\"{MeshSvgLayout.MarginBottom - 4}\" fill=\"#0f1419\"/>");

			double spanHours = (t1 - t0) / 3600.0;
			double stepHours = ChooseTimeStepHours(spanHours);
			double stepSec = stepHours * 3600.0;
			double first = Math.Ceiling(t0 / stepSec) * stepSec;

			double t = first;
			while (t <= t1 + 0.01)
			{
				double x = plotLeft + (t - t0) / (t1 - t0) * plotW;
				if (x >= plotLeft - 1 && x <= plotLeft + plotW + 1)
				{
					sb.Append(CultureInfo.InvariantCulture,
						$"<line x1=\"{x}\" y1=\"{plotTop + plotH}\" x2=\"{x}\" y2=\"{plotTop + plotH + 6}\" stroke=\"#94a3b8\" stroke-width=\"1\"/>");
					sb.Append(CultureInfo.InvariantCulture,
						$"<text x=\"{x}\" y=\"{svgHeight - 14}\" fill=\"#9fb3c8\" font-size=\"11\" font-family=\"Segoe UI,sans-serif\" text-anchor=\"middle\">{FormatClock(TimeSpan.FromSeconds(t))}</text>");
				}

				t += stepSec;
			}
		}

		private static void DrawStationRuler(
			StringBuilder sb,
			Axis axis,
			long pkMin,
			long pkMax,
			double plotLeft,
			double plotTop,
			double plotW,
			double plotH,
			double speedStripX)
		{
			// Zona fija de etiquetas (no se recorta con el plot)
			List<StationMark> stations = BuildStationMarks(axis);
			int mi = 0;
			while (mi < stations.Count)
			{
				StationMark mark = stations[mi];
				if (mark.Pk >= pkMin && mark.Pk <= pkMax)
				{
					double y = PkToY(mark.Pk, pkMin, pkMax, plotTop, plotH);
					string lineColor = mark.IsPrincipal ? "#475569" : "#2a3544";
					string dash = mark.IsPrincipal ? "none" : "3 3";
					sb.Append(CultureInfo.InvariantCulture,
						$"<line x1=\"{plotLeft}\" y1=\"{y}\" x2=\"{plotLeft + plotW}\" y2=\"{y}\" stroke=\"{lineColor}\" stroke-width=\"1\" stroke-dasharray=\"{dash}\"/>");

					string fill = mark.IsPrincipal ? "#e2e8f0" : "#94a3b8";
					string fontWeight = mark.IsPrincipal ? "600" : "400";
					sb.Append(CultureInfo.InvariantCulture,
						$"<text x=\"{speedStripX - 6}\" y=\"{y + 3.5}\" fill=\"{fill}\" font-size=\"10\" font-weight=\"{fontWeight}\" font-family=\"Segoe UI,sans-serif\" text-anchor=\"end\">{Escape(mark.Label)}</text>");
				}

				mi++;
			}
		}

		private static void DrawOccupations(
			StringBuilder sb,
			Mesh mesh,
			Axis axis,
			long pkMin,
			long pkMax,
			double t0,
			double t1,
			double plotLeft,
			double plotTop,
			double plotW,
			double plotH)
		{
			IReadOnlyList<CantonOccupationRect> occupations = mesh.GetCantonOccupations(axis);
			int oi = 0;
			while (oi < occupations.Count)
			{
				CantonOccupationRect occ = occupations[oi];
				double x0 = plotLeft + (occ.TimeEnter.TotalSeconds - t0) / (t1 - t0) * plotW;
				double x1 = plotLeft + (occ.TimeExit.TotalSeconds - t0) / (t1 - t0) * plotW;
				if (x1 >= plotLeft && x0 <= plotLeft + plotW
					&& occ.PkEnd > pkMin && occ.PkStart < pkMax)
				{
					if (x0 < plotLeft)
					{
						x0 = plotLeft;
					}

					if (x1 > plotLeft + plotW)
					{
						x1 = plotLeft + plotW;
					}

					long visPk0 = Math.Max(occ.PkStart, pkMin);
					long visPk1 = Math.Min(occ.PkEnd, pkMax);
					double yTop = PkToY(visPk1, pkMin, pkMax, plotTop, plotH);
					double yBot = PkToY(visPk0, pkMin, pkMax, plotTop, plotH);
					double y = Math.Min(yTop, yBot);
					double h = Math.Abs(yBot - yTop);
					double w = x1 - x0;
					if (w > 0.5 && h > 0.5)
					{
						sb.Append(CultureInfo.InvariantCulture,
							$"<rect x=\"{x0}\" y=\"{y}\" width=\"{w}\" height=\"{h}\" fill=\"#38bdf8\" fill-opacity=\"0.12\" stroke=\"#38bdf8\" stroke-opacity=\"0.35\" stroke-width=\"0.8\">");
						sb.Append(CultureInfo.InvariantCulture,
							$"<title>{Escape(occ.CirculationId)} cantón [{FormatPk(occ.PkStart)}–{FormatPk(occ.PkEnd)})</title>");
						sb.Append("</rect>");
					}
				}

				oi++;
			}
		}

		private static int DrawCirculations(
			StringBuilder sb,
			Mesh mesh,
			Axis axis,
			long pkMin,
			long pkMax,
			double t0,
			double t1,
			double plotLeft,
			double plotTop,
			double plotW,
			double plotH)
		{
			string[] palette = new[] { "#38bdf8", "#a78bfa", "#34d399", "#fbbf24", "#f472b6", "#fb7185", "#2dd4bf", "#c084fc" };
			int drawn = 0;
			int ci = 0;
			while (ci < mesh.Circulations.Count)
			{
				Circulation c = mesh.Circulations[ci];
				if (!string.Equals(c.Asimilation.Axis.Id, axis.Id, StringComparison.Ordinal))
				{
					ci++;
					continue;
				}

				string color = palette[drawn % palette.Length];
				string points = BuildPolylinePoints(c, pkMin, pkMax, t0, t1, plotLeft, plotTop, plotW, plotH);
				if (points.Length > 0)
				{
					sb.Append(CultureInfo.InvariantCulture,
						$"<polyline fill=\"none\" stroke=\"{color}\" stroke-width=\"2\" points=\"{points}\">");
					sb.Append(CultureInfo.InvariantCulture,
						$"<title>{Escape(c.Id)} · salida {c.Departure}</title>");
					sb.Append("</polyline>");
					drawn++;
				}

				ci++;
			}

			return drawn;
		}

		private static double ChooseTimeStepHours(double spanHours)
		{
			if (spanHours <= 2)
			{
				return 0.25;
			}

			if (spanHours <= 6)
			{
				return 0.5;
			}

			if (spanHours <= 14)
			{
				return 1.0;
			}

			if (spanHours <= 30)
			{
				return 2.0;
			}

			return 4.0;
		}

		private static void NormalizePkRange(ref long pkMin, ref long pkMax)
		{
			if (pkMax < pkMin)
			{
				long swap = pkMin;
				pkMin = pkMax;
				pkMax = swap;
			}

			if (pkMax == pkMin)
			{
				pkMax = pkMin + 1;
			}
		}

		private static void AppendPkBandRect(
			StringBuilder sb,
			double x,
			double width,
			long pkStart,
			long pkEnd,
			long pkMin,
			long pkMax,
			double plotTop,
			double plotH,
			string color,
			string title)
		{
			// Intersección con ventana visible
			long a = Math.Max(pkStart, pkMin);
			long b = Math.Min(pkEnd, pkMax);
			if (b <= a)
			{
				return;
			}

			double yTop = PkToY(b, pkMin, pkMax, plotTop, plotH);
			double yBot = PkToY(a, pkMin, pkMax, plotTop, plotH);
			double y = Math.Min(yTop, yBot);
			double h = Math.Abs(yBot - yTop);
			if (h < 0.5)
			{
				h = 0.5;
			}

			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{x}\" y=\"{y}\" width=\"{width}\" height=\"{h}\" fill=\"{color}\" stroke=\"#0f1419\" stroke-width=\"0.35\">");
			sb.Append(CultureInfo.InvariantCulture,
				$"<title>{Escape(title)}</title>");
			sb.Append("</rect>");
		}

		private static List<SpeedBand> BuildSpeedBands(Axis axis, long pkMin, long pkMax)
		{
			List<SpeedBand> bands = new List<SpeedBand>();
			long step = ChooseSampleStep(pkMin, pkMax);
			int? currentSpeed = null;
			long runStart = pkMin;
			long pk = pkMin;

			while (pk < pkMax)
			{
				int speed = axis.GetEffectiveSpeedLimit(pk) ?? 0;
				if (!currentSpeed.HasValue)
				{
					currentSpeed = speed;
					runStart = pk;
				}
				else if (speed != currentSpeed.Value)
				{
					bands.Add(new SpeedBand(runStart, pk, currentSpeed.Value));
					runStart = pk;
					currentSpeed = speed;
				}

				long next = pk + step;
				if (next >= pkMax)
				{
					break;
				}

				pk = next;
			}

			if (currentSpeed.HasValue)
			{
				bands.Add(new SpeedBand(runStart, pkMax, currentSpeed.Value));
			}
			else
			{
				bands.Add(new SpeedBand(pkMin, pkMax, axis.Vmax > 0 ? axis.Vmax : 0));
			}

			return bands;
		}

		private static List<TrackBand> BuildTrackBands(Axis axis, long pkMin, long pkMax)
		{
			List<TrackBand> bands = new List<TrackBand>();
			long step = ChooseSampleStep(pkMin, pkMax);
			int? currentTracks = null;
			long runStart = pkMin;
			long pk = pkMin;

			while (pk < pkMax)
			{
				int tracks = axis.GetTrackCountAt(pk);
				if (!currentTracks.HasValue)
				{
					currentTracks = tracks;
					runStart = pk;
				}
				else if (tracks != currentTracks.Value)
				{
					bands.Add(new TrackBand(runStart, pk, currentTracks.Value));
					runStart = pk;
					currentTracks = tracks;
				}

				long next = pk + step;
				if (next >= pkMax)
				{
					break;
				}

				pk = next;
			}

			if (currentTracks.HasValue)
			{
				bands.Add(new TrackBand(runStart, pkMax, currentTracks.Value));
			}
			else
			{
				bands.Add(new TrackBand(pkMin, pkMax, axis.DefaultTrackCount));
			}

			return bands;
		}

		private static long ChooseSampleStep(long pkMin, long pkMax)
		{
			long span = pkMax - pkMin;
			long step = span > 50000 ? 50L : (span > 10000 ? 25L : 10L);
			return step < 5L ? 5L : step;
		}

		private static void DrawSpeedLegend(StringBuilder sb, SortedSet<int> speeds, double rightX, double topY)
		{
			if (speeds.Count == 0)
			{
				return;
			}

			double boxW = 58;
			double rowH = 14;
			double pad = 6;
			double headerH = 14;
			double boxH = pad + headerH + speeds.Count * rowH + pad;
			double boxX = rightX - boxW;
			double boxY = topY;

			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{boxX}\" y=\"{boxY}\" width=\"{boxW}\" height=\"{boxH}\" rx=\"3\" fill=\"#0f1419\" fill-opacity=\"0.88\" stroke=\"#475569\"/>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<text x=\"{boxX + pad}\" y=\"{boxY + pad + 9}\" fill=\"#94a3b8\" font-size=\"9\" font-family=\"Segoe UI,sans-serif\">V (km/h)</text>");

			int row = 0;
			foreach (int speed in speeds)
			{
				double y = boxY + pad + headerH + row * rowH + 10;
				sb.Append(CultureInfo.InvariantCulture,
					$"<rect x=\"{boxX + pad}\" y=\"{y - 8}\" width=\"10\" height=\"10\" rx=\"1\" fill=\"{SpeedToColor(speed)}\" stroke=\"#1e293b\"/>");
				sb.Append(CultureInfo.InvariantCulture,
					$"<text x=\"{boxX + pad + 14}\" y=\"{y}\" fill=\"#e2e8f0\" font-size=\"10\" font-family=\"Segoe UI,sans-serif\">{speed}</text>");
				row++;
			}
		}

		private static void DrawTrackLegend(StringBuilder sb, double rightX, double topY)
		{
			double boxW = 78;
			double boxH = 44;
			double pad = 6;
			double boxX = rightX - boxW;
			double boxY = topY;

			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{boxX}\" y=\"{boxY}\" width=\"{boxW}\" height=\"{boxH}\" rx=\"3\" fill=\"#0f1419\" fill-opacity=\"0.88\" stroke=\"#475569\"/>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<text x=\"{boxX + pad}\" y=\"{boxY + pad + 9}\" fill=\"#94a3b8\" font-size=\"9\" font-family=\"Segoe UI,sans-serif\">Vías</text>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{boxX + pad}\" y=\"{boxY + 20}\" width=\"10\" height=\"10\" rx=\"1\" fill=\"{TrackCountToColor(1)}\" stroke=\"#1e293b\"/>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<text x=\"{boxX + pad + 14}\" y=\"{boxY + 28}\" fill=\"#e2e8f0\" font-size=\"10\" font-family=\"Segoe UI,sans-serif\">única</text>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{boxX + pad}\" y=\"{boxY + 32}\" width=\"10\" height=\"10\" rx=\"1\" fill=\"{TrackCountToColor(2)}\" stroke=\"#1e293b\"/>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<text x=\"{boxX + pad + 14}\" y=\"{boxY + 40}\" fill=\"#e2e8f0\" font-size=\"10\" font-family=\"Segoe UI,sans-serif\">doble</text>");
		}

		private static string SpeedToColor(int speedKmh)
		{
			if (speedKmh <= 0)
			{
				return "#334155";
			}

			if (speedKmh <= 30)
			{
				return "#ef4444";
			}

			if (speedKmh <= 50)
			{
				return "#f97316";
			}

			if (speedKmh <= 80)
			{
				return "#eab308";
			}

			if (speedKmh <= 100)
			{
				return "#22c55e";
			}

			if (speedKmh <= 120)
			{
				return "#14b8a6";
			}

			return "#3b82f6";
		}

		private static string TrackCountToColor(int trackCount)
		{
			if (trackCount <= 1)
			{
				return "#a16207";
			}

			if (trackCount == 2)
			{
				return "#2563eb";
			}

			return "#7c3aed";
		}

		private static double PkToY(long pk, long pkMin, long pkMax, double plotTop, double plotH)
		{
			return plotTop + (1.0 - (double)(pk - pkMin) / (pkMax - pkMin)) * plotH;
		}

		private static List<StationMark> BuildStationMarks(Axis axis)
		{
			List<StationMark> marks = new List<StationMark>();
			HashSet<long> seenPk = new HashSet<long>();
			int index = 0;
			while (index < axis.Stations.Count)
			{
				StationOnAxis placement = axis.Stations[index];
				if (seenPk.Add(placement.PK))
				{
					bool principal = StationClassification.IsPrincipalStation(placement.Station);
					marks.Add(new StationMark(placement.PK, FormatStationLabel(placement.Station), principal));
				}

				index++;
			}

			marks.Sort(static (a, b) => a.Pk.CompareTo(b.Pk));
			return marks;
		}

		private static string FormatStationLabel(Station station)
		{
			if (!string.IsNullOrEmpty(station.Avr) && !string.IsNullOrEmpty(station.Name))
			{
				return station.Avr + " · " + station.Name;
			}

			if (!string.IsNullOrEmpty(station.Name))
			{
				return station.Name;
			}

			if (!string.IsNullOrEmpty(station.Avr))
			{
				return station.Avr;
			}

			return station.Id;
		}

		private static string BuildPolylinePoints(
			Circulation c,
			long pkMin,
			long pkMax,
			double t0,
			double t1,
			double plotLeft,
			double plotTop,
			double plotW,
			double plotH)
		{
			Asimilation asim = c.Asimilation;
			StringBuilder pts = new StringBuilder();
			const int steps = 160;
			int s = 0;
			while (s <= steps)
			{
				double u = (double)s / steps;
				TimeSpan rel = TimeSpan.FromSeconds(asim.TotalTime.TotalSeconds * u);
				long pk = asim.PKByTime(rel);
				double absSec = (c.Departure + rel).TotalSeconds;

				if (absSec >= t0 && absSec <= t1 && pk >= pkMin && pk <= pkMax)
				{
					double x = plotLeft + (absSec - t0) / (t1 - t0) * plotW;
					double y = PkToY(pk, pkMin, pkMax, plotTop, plotH);
					if (pts.Length > 0)
					{
						pts.Append(' ');
					}

					pts.Append(x.ToString("0.##", CultureInfo.InvariantCulture));
					pts.Append(',');
					pts.Append(y.ToString("0.##", CultureInfo.InvariantCulture));
				}
				else if (pts.Length > 0)
				{
					// Romper la polilínea al salir de la ventana
					pts.Append(' ');
				}

				s++;
			}

			return pts.ToString().Trim();
		}

		private static string FormatPk(long pk)
		{
			long abs = Math.Abs(pk);
			long km = abs / 1000L;
			long m = abs % 1000L;
			if (pk < 0)
			{
				return $"-{km}+{m:D3}";
			}

			return $"{km}+{m:D3}";
		}

		private static string FormatClock(TimeSpan ts)
		{
			int h = (int)ts.TotalHours;
			if (h < 0)
			{
				h = 0;
			}

			int m = ts.Minutes;
			if (m < 0)
			{
				m = 0;
			}

			return h.ToString("00", CultureInfo.InvariantCulture) + ":" + m.ToString("00", CultureInfo.InvariantCulture);
		}

		private static string Escape(string text)
		{
			return System.Security.SecurityElement.Escape(text) ?? string.Empty;
		}

		private readonly struct StationMark
		{
			public StationMark(long pk, string label, bool isPrincipal)
			{
				Pk = pk;
				Label = label;
				IsPrincipal = isPrincipal;
			}

			public long Pk { get; }
			public string Label { get; }
			public bool IsPrincipal { get; }
		}

		private readonly struct SpeedBand
		{
			public SpeedBand(long pkStart, long pkEnd, int speedKmh)
			{
				PkStart = pkStart;
				PkEnd = pkEnd;
				SpeedKmh = speedKmh;
			}

			public long PkStart { get; }
			public long PkEnd { get; }
			public int SpeedKmh { get; }
		}

		private readonly struct TrackBand
		{
			public TrackBand(long pkStart, long pkEnd, int trackCount)
			{
				PkStart = pkStart;
				PkEnd = pkEnd;
				TrackCount = trackCount;
			}

			public long PkStart { get; }
			public long PkEnd { get; }
			public int TrackCount { get; }
		}
	}
}

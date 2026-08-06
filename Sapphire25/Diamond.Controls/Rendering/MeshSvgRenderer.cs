using System.Globalization;
using System.Text;
using Diamond.Motion;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// SVG de malla: chrome fijo (estaciones, reloj, franjas V/#) + plot recortado
	/// a la ventana de datos (tiempo × PK) actualmente visible.
	/// </summary>
	public static class MeshSvgRenderer
	{
		/// <summary>Celda (px) para no superponer números de tren densos.</summary>
		private const double TrainNumberCellPx = 40.0;

		public static string Render(
			Mesh mesh,
			RouteView view,
			TimeSpan timeStart,
			TimeSpan timeEnd,
			long pkMin,
			long pkMax,
			int width = 1200,
			int height = 720,
			bool showCantonOccupations = true)
		{
			MeshSvgDrawOptions options = MeshSvgDrawOptions.Full;
			if (!showCantonOccupations)
			{
				options = new MeshSvgDrawOptions(
					showCantonOccupations: false,
					showTrainNumbers: options.ShowTrainNumbers,
					showConflicts: options.ShowConflicts,
					maxPolylineSamples: options.MaxPolylineSamples);
			}

			StringBuilder sb = new StringBuilder();
			sb.Append(CultureInfo.InvariantCulture,
				$"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">");
			sb.Append(RenderContent(mesh, view, timeStart, timeEnd, pkMin, pkMax, width, height, options));
			sb.Append("</svg>");
			return sb.ToString();
		}

		/// <summary>
		/// Atajo mono-eje.
		/// </summary>
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
			return Render(mesh, RouteView.FromAxis(axis), timeStart, timeEnd, pkMin, pkMax, width, height, showCantonOccupations);
		}

		/// <summary>
		/// Contenido SVG completo (chrome + plot). Sin transform de pan/zoom:
		/// la ventana se controla con timeStart/timeEnd/pkMin/pkMax.
		/// El eje vertical es el PK de ruta de <paramref name="view"/>.
		/// </summary>
		public static string RenderContent(
			Mesh mesh,
			RouteView view,
			TimeSpan timeStart,
			TimeSpan timeEnd,
			long pkMin,
			long pkMax,
			int width = 1200,
			int height = 720,
			bool showCantonOccupations = true)
		{
			MeshSvgDrawOptions options = MeshSvgDrawOptions.Full;
			if (!showCantonOccupations)
			{
				options = new MeshSvgDrawOptions(
					showCantonOccupations: false,
					showTrainNumbers: options.ShowTrainNumbers,
					showConflicts: options.ShowConflicts,
					maxPolylineSamples: options.MaxPolylineSamples);
			}

			return RenderContent(mesh, view, timeStart, timeEnd, pkMin, pkMax, width, height, options);
		}

		/// <summary>
		/// Contenido SVG con control fino de capas (p. ej. modo interactivo en pan/zoom).
		/// </summary>
		public static string RenderContent(
			Mesh mesh,
			RouteView view,
			TimeSpan timeStart,
			TimeSpan timeEnd,
			long pkMin,
			long pkMax,
			int width,
			int height,
			MeshSvgDrawOptions options)
		{
			if (mesh is null)
			{
				throw new ArgumentNullException(nameof(mesh));
			}

			if (view is null)
			{
				throw new ArgumentNullException(nameof(view));
			}

			NormalizePkRange(ref pkMin, ref pkMax);

			MeshSvgPalette palette = options.Palette;
			bool externalStations = options.ExternalStationColumn;
			double plotLeft = MeshSvgLayout.GetPlotLeft(externalStations);
			double plotTop = MeshSvgLayout.PlotTop;
			double plotW = MeshSvgLayout.GetPlotWidth(width, externalStations);
			double plotH = MeshSvgLayout.PlotHeight(height);
			double speedStripX = MeshSvgLayout.GetSpeedStripX(externalStations);
			double trackStripX = MeshSvgLayout.GetTrackStripX(externalStations);
			double stripW = MeshSvgLayout.StripWidth;
			MeshYScale yScale = MeshYScale.Create(options.YScaleMode, view, pkMin, pkMax);

			double t0 = timeStart.TotalSeconds;
			double t1 = timeEnd.TotalSeconds;
			if (t1 <= t0)
			{
				t1 = t0 + 3600;
			}

			StringBuilder sb = new StringBuilder(64 * 1024);
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"0\" y=\"0\" width=\"{width}\" height=\"{height}\" fill=\"{palette.Background}\"/>");

			// IDs únicos por render: si coexisten malla en pantalla + impresión en el DOM,
			// url(#plotClip) resolvería al primer id del documento (clip pequeño de pantalla)
			// y las trazas de impresión quedarían recortadas a ~mitad superior/izquierda.
			string svgUid = Guid.NewGuid().ToString("N");
			string clipId = "plotClip_" + svgUid;
			string nowGradId = "meshNowGrad_" + svgUid;
			sb.Append("<defs>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<clipPath id=\"{clipId}\"><rect x=\"{plotLeft}\" y=\"{plotTop}\" width=\"{plotW}\" height=\"{plotH}\"/></clipPath>");
			if (!palette.IsPaper)
			{
				sb.Append("<linearGradient id=\"");
				sb.Append(nowGradId);
				sb.Append("\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\">"
					+ "<stop offset=\"0%\" stop-color=\"#67e8f9\" stop-opacity=\"0.15\"/>"
					+ "<stop offset=\"50%\" stop-color=\"#22d3ee\" stop-opacity=\"0.95\"/>"
					+ "<stop offset=\"100%\" stop-color=\"#67e8f9\" stop-opacity=\"0.15\"/>"
					+ "</linearGradient>");
			}

			sb.Append("</defs>");

			// —— Franjas V y # (solo PK de ruta visible) ——
			SortedSet<int> speedsUsed = new SortedSet<int>();
			if (options.ShowSpeedStrip)
			{
				List<SpeedBand> speedBands = BuildSpeedBands(view, pkMin, pkMax);
				int bi = 0;
				while (bi < speedBands.Count)
				{
					SpeedBand band = speedBands[bi];
					speedsUsed.Add(band.SpeedKmh);
					AppendPkBandRect(
						sb, speedStripX, stripW,
						band.PkStart, band.PkEnd, pkMin, pkMax, plotTop, plotH, yScale,
						palette.MapUiColor(SpeedToColor(band.SpeedKmh)),
						band.SpeedKmh + " km/h · " + FormatPk(band.PkStart) + "–" + FormatPk(band.PkEnd),
						palette);
					bi++;
				}

				sb.Append(CultureInfo.InvariantCulture,
					$"<rect x=\"{speedStripX}\" y=\"{plotTop}\" width=\"{stripW}\" height=\"{plotH}\" fill=\"none\" stroke=\"{palette.StripBorder}\" stroke-width=\"1\"/>");
				sb.Append(CultureInfo.InvariantCulture,
					$"<text x=\"{speedStripX + stripW / 2}\" y=\"{plotTop - 8}\" fill=\"{palette.TextMuted}\" font-size=\"9\" font-family=\"Segoe UI,sans-serif\" text-anchor=\"middle\">V</text>");
			}

			if (options.ShowTrackStrip)
			{
				List<TrackBand> trackBands = BuildTrackBands(view, pkMin, pkMax);
				int ti = 0;
				while (ti < trackBands.Count)
				{
					TrackBand band = trackBands[ti];
					string trackTitle = band.TrackCount >= 2
						? "Doble vía (" + band.TrackCount + ") · " + FormatPk(band.PkStart) + "–" + FormatPk(band.PkEnd)
						: "Vía única · " + FormatPk(band.PkStart) + "–" + FormatPk(band.PkEnd);
					AppendPkBandRect(
						sb, trackStripX, stripW,
						band.PkStart, band.PkEnd, pkMin, pkMax, plotTop, plotH, yScale,
						palette.MapUiColor(TrackCountToColor(band.TrackCount)),
						trackTitle,
						palette);
					ti++;
				}

				sb.Append(CultureInfo.InvariantCulture,
					$"<rect x=\"{trackStripX}\" y=\"{plotTop}\" width=\"{stripW}\" height=\"{plotH}\" fill=\"none\" stroke=\"{palette.StripBorder}\" stroke-width=\"1\"/>");
				sb.Append(CultureInfo.InvariantCulture,
					$"<text x=\"{trackStripX + stripW / 2}\" y=\"{plotTop - 8}\" fill=\"{palette.TextMuted}\" font-size=\"9\" font-family=\"Segoe UI,sans-serif\" text-anchor=\"middle\">#</text>");
			}

			// —— Plot (fondo + clip) ——
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{plotLeft}\" y=\"{plotTop}\" width=\"{plotW}\" height=\"{plotH}\" fill=\"{palette.PlotBackground}\" stroke=\"{palette.PlotBorder}\"/>");

			sb.Append(CultureInfo.InvariantCulture, $"<g clip-path=\"url(#{clipId})\">");

			// Grid horario (según ventana visible)
			DrawTimeGrid(sb, t0, t1, plotLeft, plotTop, plotW, plotH, palette);

			// Ocupaciones de cantón (costosas: se omiten en pan/zoom interactivo)
			if (options.ShowCantonOccupations)
			{
				DrawOccupations(sb, mesh, view, pkMin, pkMax, t0, t1, plotLeft, plotTop, plotW, plotH, yScale);
			}

			// Circulaciones (trazas dentro del clip). Los números se dibujan fuera del clip
			// para poder anclarlos también a la barra de horas.
			int drawn = 0;
			List<NumberLabel> trainNumberLabels = new List<NumberLabel>();
			if (options.ShowTrainPaths || options.ShowTrainNumbers)
			{
				drawn = DrawCirculations(
					sb, mesh, view, pkMin, pkMax, t0, t1, plotLeft, plotTop, plotW, plotH, yScale, options,
					trainNumberLabels);
			}

			// Conflictos: intersección roja + icono de aviso (encima de trazas)
			if (options.ShowConflicts)
			{
				DrawConflicts(sb, mesh, view, pkMin, pkMax, t0, t1, plotLeft, plotTop, plotW, plotH, yScale);
			}

			// Línea de hora actual (dentro del clip del plot)
			if (options.ShowNowLine && options.NowTime.HasValue)
			{
				DrawNowLine(
					sb, options.NowTime.Value, t0, t1, plotLeft, plotTop, plotW, plotH,
					drawChrome: false, nowGradId: nowGradId);
			}

			sb.Append("</g>");

			// —— Regla de estaciones (fija a la izquierda; contenido según PK visible) ——
			DrawStationRuler(
				sb, view, pkMin, pkMax, plotLeft, plotTop, plotW, plotH, speedStripX, yScale,
				drawLabels: options.ShowStationLabels && !options.ExternalStationColumn,
				palette);

			// —— Regla de tiempo (fija abajo; contenido según tiempo visible) ——
			DrawTimeRuler(sb, t0, t1, plotLeft, plotTop, plotW, plotH, height, palette);

			// Números de tren (rotado a continuación del trazo, o en la barra de horas).
			if (options.ShowTrainNumbers && trainNumberLabels.Count > 0)
			{
				AppendTrainNumberLabels(sb, trainNumberLabels, palette);
			}

			// Marcos plot encima del clip
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{plotLeft}\" y=\"{plotTop}\" width=\"{plotW}\" height=\"{plotH}\" fill=\"none\" stroke=\"{palette.PlotBorder}\" stroke-width=\"1.2\"/>");

			// Cabecera/badge del reloj actual (fuera del clip, encima del marco)
			if (options.ShowNowLine && options.NowTime.HasValue && !palette.IsPaper)
			{
				DrawNowLine(
					sb, options.NowTime.Value, t0, t1, plotLeft, plotTop, plotW, plotH,
					drawChrome: true, nowGradId: nowGradId);
			}

			// Leyendas
			if (options.ShowSpeedStrip)
			{
				DrawSpeedLegend(sb, speedsUsed, plotLeft + plotW - 8, plotTop + 8, palette);
			}

			if (options.ShowTrackStrip)
			{
				double legendTop = plotTop + 8;
				if (options.ShowSpeedStrip)
				{
					legendTop = plotTop + 8 + 12 + Math.Max(1, speedsUsed.Count) * 14 + 16;
				}

				DrawTrackLegend(sb, plotLeft + plotW - 8, legendTop, palette);
			}

			string title = "View " + view.Id + " · " + view.Name
				+ " — " + drawn + " circ. visibles"
				+ " · t " + FormatClock(TimeSpan.FromSeconds(t0)) + "–" + FormatClock(TimeSpan.FromSeconds(t1))
				+ " · PK " + FormatPk(pkMin) + "–" + FormatPk(pkMax);

			sb.Append(CultureInfo.InvariantCulture,
				$"<text x=\"{plotLeft}\" y=\"20\" fill=\"{palette.TextPrimary}\" font-size=\"13\" font-family=\"Segoe UI,sans-serif\">{Escape(title)}</text>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<text x=\"{(plotLeft + plotLeft + plotW) / 2}\" y=\"{height - 4}\" fill=\"{palette.TextSecondary}\" font-size=\"12\" font-family=\"Segoe UI,sans-serif\" text-anchor=\"middle\">Tiempo</text>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<text x=\"12\" y=\"{plotTop + plotH / 2}\" fill=\"{palette.TextSecondary}\" font-size=\"12\" font-family=\"Segoe UI,sans-serif\" text-anchor=\"middle\" transform=\"rotate(-90 12,{plotTop + plotH / 2})\">Estaciones</text>");

			return sb.ToString();
		}

		/// <summary>
		/// Atajo mono-eje para contenido SVG.
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
			return RenderContent(
				mesh,
				RouteView.FromAxis(axis),
				timeStart,
				timeEnd,
				pkMin,
				pkMax,
				width,
				height,
				showCantonOccupations);
		}

		private static void DrawTimeGrid(
			StringBuilder sb,
			double t0,
			double t1,
			double plotLeft,
			double plotTop,
			double plotW,
			double plotH,
			MeshSvgPalette palette)
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
				string stroke = major ? palette.GridMajor : palette.GridMinor;
				sb.Append(CultureInfo.InvariantCulture,
					$"<line x1=\"{x}\" y1=\"{plotTop}\" x2=\"{x}\" y2=\"{plotTop + plotH}\" stroke=\"{stroke}\" stroke-width=\"1\"/>");
				t += stepSec;
			}
		}

		/// <summary>
		/// Línea vertical de "ahora". <paramref name="drawChrome"/> false = solo el trazo en el plot;
		/// true = cabecera/badge de hora (fuera del clip).
		/// </summary>
		private static void DrawNowLine(
			StringBuilder sb,
			TimeSpan now,
			double t0,
			double t1,
			double plotLeft,
			double plotTop,
			double plotW,
			double plotH,
			bool drawChrome,
			string nowGradId)
		{
			double nowSec = now.TotalSeconds;
			if (nowSec < t0 || nowSec > t1 || t1 <= t0)
			{
				return;
			}

			double x = plotLeft + (nowSec - t0) / (t1 - t0) * plotW;
			string xs = x.ToString("0.##", CultureInfo.InvariantCulture);
			string y0s = plotTop.ToString("0.##", CultureInfo.InvariantCulture);
			string y1s = (plotTop + plotH).ToString("0.##", CultureInfo.InvariantCulture);
			string clock = FormatClockWithSeconds(now);
			string gradRef = string.IsNullOrEmpty(nowGradId) ? "meshNowGrad" : nowGradId;

			if (!drawChrome)
			{
				// Halo suave + núcleo (estilo "faro" cyan)
				sb.Append(CultureInfo.InvariantCulture,
					$"<line x1=\"{xs}\" y1=\"{y0s}\" x2=\"{xs}\" y2=\"{y1s}\" stroke=\"#22d3ee\" stroke-width=\"10\" stroke-opacity=\"0.08\"/>");
				sb.Append(CultureInfo.InvariantCulture,
					$"<line x1=\"{xs}\" y1=\"{y0s}\" x2=\"{xs}\" y2=\"{y1s}\" stroke=\"#22d3ee\" stroke-width=\"3.5\" stroke-opacity=\"0.28\"/>");
				sb.Append(CultureInfo.InvariantCulture,
					$"<line x1=\"{xs}\" y1=\"{y0s}\" x2=\"{xs}\" y2=\"{y1s}\" stroke=\"url(#{gradRef})\" stroke-width=\"1.6\" stroke-dasharray=\"7 5\" stroke-linecap=\"round\"/>");
				// Marca inferior en forma de chevron
				double cy = plotTop + plotH - 2;
				sb.Append(CultureInfo.InvariantCulture,
					$"<polygon points=\"{xs},{(cy - 7).ToString("0.##", CultureInfo.InvariantCulture)} {(x - 5).ToString("0.##", CultureInfo.InvariantCulture)},{cy.ToString("0.##", CultureInfo.InvariantCulture)} {(x + 5).ToString("0.##", CultureInfo.InvariantCulture)},{cy.ToString("0.##", CultureInfo.InvariantCulture)}\" fill=\"#22d3ee\" fill-opacity=\"0.9\"/>");
				return;
			}

			// Badge superior con la hora
			const double badgeW = 64;
			const double badgeH = 18;
			double badgeX = x - badgeW * 0.5;
			double badgeY = plotTop - badgeH - 4;
			if (badgeX < plotLeft)
			{
				badgeX = plotLeft;
			}

			if (badgeX + badgeW > plotLeft + plotW)
			{
				badgeX = plotLeft + plotW - badgeW;
			}

			sb.Append(CultureInfo.InvariantCulture,
				$"<g class=\"mesh-now-chrome\">");
			// Pin superior
			sb.Append(CultureInfo.InvariantCulture,
				$"<circle cx=\"{xs}\" cy=\"{(plotTop - 1).ToString("0.##", CultureInfo.InvariantCulture)}\" r=\"3.5\" fill=\"#67e8f9\" stroke=\"#0e7490\" stroke-width=\"1\"/>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{badgeX.ToString("0.##", CultureInfo.InvariantCulture)}\" y=\"{badgeY.ToString("0.##", CultureInfo.InvariantCulture)}\" width=\"{badgeW.ToString("0.##", CultureInfo.InvariantCulture)}\" height=\"{badgeH.ToString("0.##", CultureInfo.InvariantCulture)}\" rx=\"5\" ry=\"5\" fill=\"#0c4a6e\" fill-opacity=\"0.92\" stroke=\"#22d3ee\" stroke-width=\"1\"/>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<text x=\"{(badgeX + badgeW * 0.5).ToString("0.##", CultureInfo.InvariantCulture)}\" y=\"{(badgeY + 12.5).ToString("0.##", CultureInfo.InvariantCulture)}\" fill=\"#ecfeff\" font-size=\"11\" font-weight=\"700\" font-family=\"Segoe UI,Consolas,monospace\" text-anchor=\"middle\">{Escape(clock)}</text>");
			sb.Append("</g>");
		}

		private static string FormatClockWithSeconds(TimeSpan ts)
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

			int s = ts.Seconds;
			if (s < 0)
			{
				s = 0;
			}

			return h.ToString("00", CultureInfo.InvariantCulture)
				+ ":" + m.ToString("00", CultureInfo.InvariantCulture)
				+ ":" + s.ToString("00", CultureInfo.InvariantCulture);
		}

		private static void DrawTimeRuler(
			StringBuilder sb,
			double t0,
			double t1,
			double plotLeft,
			double plotTop,
			double plotW,
			double plotH,
			int svgHeight,
			MeshSvgPalette palette)
		{
			// Banda fija bajo el plot
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{plotLeft}\" y=\"{plotTop + plotH}\" width=\"{plotW}\" height=\"{MeshSvgLayout.MarginBottom - 4}\" fill=\"{palette.Background}\"/>");

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
						$"<line x1=\"{x}\" y1=\"{plotTop + plotH}\" x2=\"{x}\" y2=\"{plotTop + plotH + 6}\" stroke=\"{palette.AxisTick}\" stroke-width=\"1\"/>");
					sb.Append(CultureInfo.InvariantCulture,
						$"<text x=\"{x}\" y=\"{svgHeight - 14}\" fill=\"{palette.TextClock}\" font-size=\"11\" font-family=\"Segoe UI,sans-serif\" text-anchor=\"middle\">{FormatClock(TimeSpan.FromSeconds(t))}</text>");
				}

				t += stepSec;
			}
		}

		private static void DrawStationRuler(
			StringBuilder sb,
			RouteView view,
			long pkMin,
			long pkMax,
			double plotLeft,
			double plotTop,
			double plotW,
			double plotH,
			double speedStripX,
			MeshYScale yScale,
			bool drawLabels,
			MeshSvgPalette palette)
		{
			IReadOnlyList<StationMark> stations = BuildStationMarks(view);
			int mi = 0;
			while (mi < stations.Count)
			{
				StationMark mark = stations[mi];
				if (mark.Pk >= pkMin && mark.Pk <= pkMax)
				{
					double y = PkToY(yScale, mark.Pk, plotTop, plotH);
					string lineColor = mark.IsPrincipal ? palette.StationLinePrincipal : palette.StationLineHalt;
					string dash = mark.IsPrincipal ? "none" : "3 3";
					sb.Append(CultureInfo.InvariantCulture,
						$"<line x1=\"{plotLeft}\" y1=\"{y}\" x2=\"{plotLeft + plotW}\" y2=\"{y}\" stroke=\"{lineColor}\" stroke-width=\"1\" stroke-dasharray=\"{dash}\"/>");

					if (drawLabels)
					{
						string fill = mark.IsPrincipal ? palette.TextPrimary : palette.TextMuted;
						string fontWeight = mark.IsPrincipal ? "600" : "400";
						sb.Append(CultureInfo.InvariantCulture,
							$"<text x=\"{speedStripX - 6}\" y=\"{y + 3.5}\" fill=\"{fill}\" font-size=\"10\" font-weight=\"{fontWeight}\" font-family=\"Segoe UI,sans-serif\" text-anchor=\"end\">{Escape(mark.Label)}</text>");
					}
				}

				mi++;
			}
		}

		private static void DrawOccupations(
			StringBuilder sb,
			Mesh mesh,
			RouteView view,
			long pkMin,
			long pkMax,
			double t0,
			double t1,
			double plotLeft,
			double plotTop,
			double plotW,
			double plotH,
			MeshYScale yScale)
		{
			IReadOnlyList<CantonOccupationRect> occupations = mesh.GetCantonOccupations(view);
			int oi = 0;
			while (oi < occupations.Count)
			{
				CantonOccupationRect occ = occupations[oi];
				double x;
				double y;
				double w;
				double h;
				if (TryMapOccupationToPlot(
					occ, pkMin, pkMax, t0, t1, plotLeft, plotTop, plotW, plotH, yScale,
					out x, out y, out w, out h))
				{
					sb.Append(CultureInfo.InvariantCulture,
						$"<rect x=\"{x.ToString("0.##", CultureInfo.InvariantCulture)}\" y=\"{y.ToString("0.##", CultureInfo.InvariantCulture)}\" width=\"{w.ToString("0.##", CultureInfo.InvariantCulture)}\" height=\"{h.ToString("0.##", CultureInfo.InvariantCulture)}\" fill=\"#38bdf8\" fill-opacity=\"0.12\" stroke=\"#38bdf8\" stroke-opacity=\"0.35\" stroke-width=\"0.8\">");
					sb.Append(CultureInfo.InvariantCulture,
						$"<title>{Escape(occ.CirculationId)} cantón [{FormatPk(occ.PkStart)}–{FormatPk(occ.PkEnd)})</title>");
					sb.Append("</rect>");
				}

				oi++;
			}
		}

		/// <summary>
		/// Intersecciones incompatibles en rojo + triángulo de aviso en el centro.
		/// </summary>
		private static void DrawConflicts(
			StringBuilder sb,
			Mesh mesh,
			RouteView view,
			long pkMin,
			long pkMax,
			double t0,
			double t1,
			double plotLeft,
			double plotTop,
			double plotW,
			double plotH,
			MeshYScale yScale)
		{
			IReadOnlyList<OccupationConflict> conflicts = mesh.GetHardConflicts(view);
			int index = 0;
			while (index < conflicts.Count)
			{
				OccupationConflict conflict = conflicts[index];
				CantonOccupationRect overlap = conflict.Intersection;
				double x;
				double y;
				double w;
				double h;
				if (TryMapOccupationToPlot(
					overlap, pkMin, pkMax, t0, t1, plotLeft, plotTop, plotW, plotH, yScale,
					out x, out y, out w, out h))
				{
					string tip = conflict.Kind + ": tren " + conflict.CirculationIdA
						+ " âˆ© tren " + conflict.CirculationIdB
						+ " · PK " + FormatPk(overlap.PkStart) + "–" + FormatPk(overlap.PkEnd)
						+ " · " + FormatClock(overlap.TimeEnter) + "–" + FormatClock(overlap.TimeExit);

					// Relleno rojo de la intersección
					sb.Append(CultureInfo.InvariantCulture,
						$"<rect x=\"{x.ToString("0.##", CultureInfo.InvariantCulture)}\" y=\"{y.ToString("0.##", CultureInfo.InvariantCulture)}\" width=\"{w.ToString("0.##", CultureInfo.InvariantCulture)}\" height=\"{h.ToString("0.##", CultureInfo.InvariantCulture)}\" fill=\"#ef4444\" fill-opacity=\"0.45\" stroke=\"#fecaca\" stroke-width=\"1.2\">");
					sb.Append(CultureInfo.InvariantCulture,
						$"<title>{Escape(tip)}</title>");
					sb.Append("</rect>");

					// Icono de aviso (triángulo + !) en el centro del solape
					double cx = x + w * 0.5;
					double cy = y + h * 0.5;
					AppendWarningIcon(sb, cx, cy, tip);
				}

				index++;
			}
		}

		private static void AppendWarningIcon(StringBuilder sb, double cx, double cy, string tip)
		{
			// Escala legible aunque el rectángulo de conflicto sea pequeño
			const double s = 9.0;
			string cxs = cx.ToString("0.##", CultureInfo.InvariantCulture);
			string cys = cy.ToString("0.##", CultureInfo.InvariantCulture);

			// Triángulo equilátero apuntando arriba, centrado en (cx, cy)
			double x1 = cx;
			double y1 = cy - s;
			double x2 = cx + s * 0.9;
			double y2 = cy + s * 0.75;
			double x3 = cx - s * 0.9;
			double y3 = cy + s * 0.75;

			sb.Append(CultureInfo.InvariantCulture,
				$"<g>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<polygon points=\"{x1.ToString("0.##", CultureInfo.InvariantCulture)},{y1.ToString("0.##", CultureInfo.InvariantCulture)} {x2.ToString("0.##", CultureInfo.InvariantCulture)},{y2.ToString("0.##", CultureInfo.InvariantCulture)} {x3.ToString("0.##", CultureInfo.InvariantCulture)},{y3.ToString("0.##", CultureInfo.InvariantCulture)}\" fill=\"#fbbf24\" stroke=\"#7f1d1d\" stroke-width=\"1.2\">");
			sb.Append(CultureInfo.InvariantCulture,
				$"<title>{Escape(tip)}</title>");
			sb.Append("</polygon>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<text x=\"{cxs}\" y=\"{(cy + 3.5).ToString("0.##", CultureInfo.InvariantCulture)}\" fill=\"#7f1d1d\" font-size=\"11\" font-weight=\"800\" font-family=\"Segoe UI,sans-serif\" text-anchor=\"middle\">!</text>");
			sb.Append("</g>");
		}

		private static bool TryMapOccupationToPlot(
			CantonOccupationRect occ,
			long pkMin,
			long pkMax,
			double t0,
			double t1,
			double plotLeft,
			double plotTop,
			double plotW,
			double plotH,
			MeshYScale yScale,
			out double x,
			out double y,
			out double w,
			out double h)
		{
			x = 0;
			y = 0;
			w = 0;
			h = 0;

			double x0 = plotLeft + (occ.TimeEnter.TotalSeconds - t0) / (t1 - t0) * plotW;
			double x1 = plotLeft + (occ.TimeExit.TotalSeconds - t0) / (t1 - t0) * plotW;
			if (x1 < plotLeft || x0 > plotLeft + plotW
				|| occ.PkEnd <= pkMin || occ.PkStart >= pkMax)
			{
				return false;
			}

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
			if (visPk1 <= visPk0)
			{
				return false;
			}

			double yTop = PkToY(yScale, visPk1, plotTop, plotH);
			double yBot = PkToY(yScale, visPk0, plotTop, plotH);
			y = Math.Min(yTop, yBot);
			h = Math.Abs(yBot - yTop);
			x = x0;
			w = x1 - x0;
			return w > 0.5 && h > 0.5;
		}

		private static int DrawCirculations(
			StringBuilder sb,
			Mesh mesh,
			RouteView view,
			long pkMin,
			long pkMax,
			double t0,
			double t1,
			double plotLeft,
			double plotTop,
			double plotW,
			double plotH,
			MeshYScale yScale,
			MeshSvgDrawOptions options,
			List<NumberLabel> labelsOut)
		{
			// Un color por asimilación (mismo perfil de marcha → mismo color).
			Dictionary<Asimilation, string> colorByAsim = BuildAsimilationColorMap(mesh);

			// Circulación seleccionada: se dibuja al final (encima del resto).
			string? selectedPath = null;
			string? selectedColor = null;
			string? selectedTip = null;

			int drawn = 0;
			int ci = 0;
			while (ci < mesh.Circulations.Count)
			{
				Circulation c = mesh.Circulations[ci];

				// Cull barato por tiempo antes de proyectar la traza.
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

				MeshSvgPalette palette = options.Palette;
				// Color del script (requisito) tiene prioridad sobre la paleta por asimilación.
				string color;
				if (c.HasColor)
				{
					color = palette.MapTrainColor(c.Color);
				}
				else if (!colorByAsim.TryGetValue(c.Asimilation, out color!))
				{
					color = palette.DefaultTrain;
				}
				else
				{
					color = palette.MapTrainColor(color);
				}

				bool wantLabel = options.ShowTrainNumbers && c.HasServiceNumber;
				// Puntos de control una sola vez: path + colocación del número.
				List<MeshTrainPathBuilder.Point> points = MeshTrainPathBuilder.CollectControlPoints(
					c, view, pkMin, pkMax, t0, t1, plotLeft, plotTop, plotW, plotH, yScale,
					options.MaxPolylineSamples,
					wantLabel: false,
					out _, out _);

				string pathD = points.Count > 0
					? MeshTrainPathBuilder.ToSvgPath(points, options.UseSplinePaths)
					: string.Empty;

				List<MeshTrainPathBuilder.TrainLabelPlacement> placements =
					new List<MeshTrainPathBuilder.TrainLabelPlacement>();
				if (wantLabel && points.Count > 0)
				{
					placements = MeshTrainPathBuilder.ComputeLabelPlacements(
						points, plotLeft, plotTop, plotW, plotH, c.ServiceNumber);
				}

				if (pathD.Length > 0 || placements.Count > 0)
				{
					// Solo número de plantilla (4923…); no Id técnico de planificación.
					string numberText = c.HasServiceNumber ? c.ServiceNumber : string.Empty;
					string tip = (numberText.Length > 0 ? numberText : "Tren")
						+ " · salida " + FormatClock(c.Departure)
						+ " · ll. " + FormatClock(c.Arrival);

					bool selected = options.SelectedTechnicalId is not null
						&& string.Equals(c.TechnicalId, options.SelectedTechnicalId, StringComparison.Ordinal);

					if (options.ShowTrainPaths && pathD.Length > 0)
					{
						if (selected)
						{
							selectedPath = pathD;
							selectedColor = color;
							selectedTip = tip + " · seleccionado";
						}
						else
						{
							sb.Append(CultureInfo.InvariantCulture,
								$"<path fill=\"none\" stroke=\"{color}\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" d=\"{pathD}\">");
							sb.Append(CultureInfo.InvariantCulture,
								$"<title>{Escape(tip)}</title>");
							sb.Append("</path>");
						}

						drawn++;
					}
					else if (!options.ShowTrainPaths && wantLabel && placements.Count > 0)
					{
						// Solo etiquetas: cuenta como visible para el título.
						drawn++;
					}

					if (wantLabel && placements.Count > 0)
					{
						string labelColor = selected ? options.Palette.SelectionLabel : color;
						int pi = 0;
						while (pi < placements.Count)
						{
							MeshTrainPathBuilder.TrainLabelPlacement placement = placements[pi];
							if (placement.IsValid)
							{
								labelsOut.Add(new NumberLabel(
									placement.X,
									placement.Y,
									numberText,
									labelColor,
									placement.AngleDeg,
									placement.Band));
							}

							pi++;
						}
					}
				}

				ci++;
			}

			if (options.ShowTrainPaths
				&& selectedPath is not null
				&& selectedColor is not null)
			{
				MeshSvgPalette palette = options.Palette;
				sb.Append(CultureInfo.InvariantCulture,
					$"<path fill=\"none\" stroke=\"{palette.SelectionHalo}\" stroke-width=\"7\" stroke-linecap=\"round\" stroke-linejoin=\"round\" opacity=\"0.9\" d=\"{selectedPath}\" pointer-events=\"none\"/>");
				sb.Append(CultureInfo.InvariantCulture,
					$"<path fill=\"none\" stroke=\"{selectedColor}\" stroke-width=\"3.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\" d=\"{selectedPath}\">");
				sb.Append(CultureInfo.InvariantCulture,
					$"<title>{Escape(selectedTip ?? string.Empty)}</title>");
				sb.Append("</path>");
			}

			return drawn;
		}

		/// <summary>
		/// Dibuja números de tren con culling espacial barato para no saturar el SVG.
		/// Reglas superior/inferior: colisión solo en X (por banda). Plot: celda 2D.
		/// </summary>
		private static void AppendTrainNumberLabels(StringBuilder sb, List<NumberLabel> labels, MeshSvgPalette palette)
		{
			if (labels.Count == 0)
			{
				return;
			}

			// Hash de celdas ocupadas: evita O(n²) y limita textos superpuestos.
			HashSet<long> occupiedCells = new HashSet<long>();
			int li = 0;
			while (li < labels.Count)
			{
				NumberLabel lab = labels[li];
				long key;
				if (lab.Band == MeshTrainPathBuilder.TrainLabelBand.TopRuler
					|| lab.Band == MeshTrainPathBuilder.TrainLabelBand.BottomRuler)
				{
					// Reglas: colisión solo en X; prefijo de banda para no mezclar superior/inferior.
					int cellX = (int)Math.Floor(lab.X / (TrainNumberCellPx * 0.85));
					long bandTag = lab.Band == MeshTrainPathBuilder.TrainLabelBand.TopRuler
						? 1L << 60
						: 2L << 60;
					key = bandTag ^ (uint)cellX;
				}
				else
				{
					int cellX = (int)Math.Floor(lab.X / TrainNumberCellPx);
					int cellY = (int)Math.Floor(lab.Y / TrainNumberCellPx);
					key = ((long)cellX << 32) ^ (uint)cellY;
				}

				if (!occupiedCells.Add(key))
				{
					li++;
					continue;
				}

				string xStr = lab.X.ToString("0.#", CultureInfo.InvariantCulture);
				string yStr = lab.Y.ToString("0.#", CultureInfo.InvariantCulture);
				string fontSize = MeshTrainPathBuilder.TrainNumberFontSize.ToString("0.#", CultureInfo.InvariantCulture);

				// Halo + rotación según pendiente del trazo en el extremo.
				if (Math.Abs(lab.AngleDeg) < 0.5)
				{
					sb.Append(CultureInfo.InvariantCulture,
						$"<text x=\"{xStr}\" y=\"{yStr}\" fill=\"{lab.Color}\" stroke=\"{palette.LabelHalo}\" stroke-width=\"2\" paint-order=\"stroke fill\" font-size=\"{fontSize}\" font-weight=\"700\" font-family=\"Segoe UI,sans-serif\" text-anchor=\"middle\" dominant-baseline=\"middle\">{Escape(lab.Text)}</text>");
				}
				else
				{
					string ang = lab.AngleDeg.ToString("0.##", CultureInfo.InvariantCulture);
					sb.Append(CultureInfo.InvariantCulture,
						$"<text x=\"{xStr}\" y=\"{yStr}\" fill=\"{lab.Color}\" stroke=\"{palette.LabelHalo}\" stroke-width=\"2\" paint-order=\"stroke fill\" font-size=\"{fontSize}\" font-weight=\"700\" font-family=\"Segoe UI,sans-serif\" text-anchor=\"middle\" dominant-baseline=\"middle\" transform=\"rotate({ang} {xStr} {yStr})\">{Escape(lab.Text)}</text>");
				}

				li++;
			}
		}

		/// <summary>
		/// Asigna un color estable a cada instancia de <see cref="Asimilation"/> de la malla
		/// (orden del catálogo de asimilaciones del mesh).
		/// </summary>
		private static Dictionary<Asimilation, string> BuildAsimilationColorMap(Mesh mesh)
		{
			string[] palette = new[]
			{
				"#38bdf8", "#a78bfa", "#34d399", "#fbbf24",
				"#f472b6", "#fb7185", "#2dd4bf", "#c084fc",
				"#60a5fa", "#f59e0b", "#4ade80", "#e879f9"
			};

			Dictionary<Asimilation, string> map = new Dictionary<Asimilation, string>();
			int index = 0;
			while (index < mesh.Asimilations.Count)
			{
				Asimilation asim = mesh.Asimilations[index];
				if (!map.ContainsKey(asim))
				{
					map[asim] = palette[map.Count % palette.Length];
				}

				index++;
			}

			// Circulaciones cuya asimilación no esté en el catálogo (por si acaso).
			int ci = 0;
			while (ci < mesh.Circulations.Count)
			{
				Asimilation asim = mesh.Circulations[ci].Asimilation;
				if (!map.ContainsKey(asim))
				{
					map[asim] = palette[map.Count % palette.Length];
				}

				ci++;
			}

			return map;
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
			MeshYScale yScale,
			string color,
			string title,
			MeshSvgPalette palette)
		{
			// Intersección con ventana visible
			long a = Math.Max(pkStart, pkMin);
			long b = Math.Min(pkEnd, pkMax);
			if (b <= a)
			{
				return;
			}

			double yTop = PkToY(yScale, b, plotTop, plotH);
			double yBot = PkToY(yScale, a, plotTop, plotH);
			double y = Math.Min(yTop, yBot);
			double h = Math.Abs(yBot - yTop);
			if (h < 0.5)
			{
				h = 0.5;
			}

			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{x}\" y=\"{y}\" width=\"{width}\" height=\"{h}\" fill=\"{color}\" stroke=\"{palette.BandStroke}\" stroke-width=\"0.35\">");
			sb.Append(CultureInfo.InvariantCulture,
				$"<title>{Escape(title)}</title>");
			sb.Append("</rect>");
		}

		private static List<SpeedBand> BuildSpeedBands(RouteView view, long pkMin, long pkMax)
		{
			List<SpeedBand> bands = new List<SpeedBand>();
			long step = ChooseSampleStep(pkMin, pkMax);
			int? currentSpeed = null;
			long runStart = pkMin;
			long pk = pkMin;

			while (pk < pkMax)
			{
				int speed = view.GetEffectiveSpeedLimit(pk) ?? 0;
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
				bands.Add(new SpeedBand(pkMin, pkMax, view.Vmax > 0 ? view.Vmax : 0));
			}

			return bands;
		}

		private static List<TrackBand> BuildTrackBands(RouteView view, long pkMin, long pkMax)
		{
			List<TrackBand> bands = new List<TrackBand>();
			long step = ChooseSampleStep(pkMin, pkMax);
			int? currentTracks = null;
			long runStart = pkMin;
			long pk = pkMin;

			while (pk < pkMax)
			{
				int tracks = view.GetTrackCountAt(pk);
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
				bands.Add(new TrackBand(pkMin, pkMax, 1));
			}

			return bands;
		}

		private static long ChooseSampleStep(long pkMin, long pkMax)
		{
			long span = pkMax - pkMin;
			long step = span > 50000 ? 50L : (span > 10000 ? 25L : 10L);
			return step < 5L ? 5L : step;
		}

		private static void DrawSpeedLegend(StringBuilder sb, SortedSet<int> speeds, double rightX, double topY, MeshSvgPalette palette)
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
			string boxOpacity = palette.IsPaper ? "0.96" : "0.88";

			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{boxX}\" y=\"{boxY}\" width=\"{boxW}\" height=\"{boxH}\" rx=\"3\" fill=\"{palette.LegendBoxFill}\" fill-opacity=\"{boxOpacity}\" stroke=\"{palette.LegendBoxStroke}\"/>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<text x=\"{boxX + pad}\" y=\"{boxY + pad + 9}\" fill=\"{palette.TextMuted}\" font-size=\"9\" font-family=\"Segoe UI,sans-serif\">V (km/h)</text>");

			int row = 0;
			foreach (int speed in speeds)
			{
				double y = boxY + pad + headerH + row * rowH + 10;
				sb.Append(CultureInfo.InvariantCulture,
					$"<rect x=\"{boxX + pad}\" y=\"{y - 8}\" width=\"10\" height=\"10\" rx=\"1\" fill=\"{palette.MapUiColor(SpeedToColor(speed))}\" stroke=\"{palette.LegendBoxStroke}\"/>");
				sb.Append(CultureInfo.InvariantCulture,
					$"<text x=\"{boxX + pad + 14}\" y=\"{y}\" fill=\"{palette.TextPrimary}\" font-size=\"10\" font-family=\"Segoe UI,sans-serif\">{speed}</text>");
				row++;
			}
		}

		private static void DrawTrackLegend(StringBuilder sb, double rightX, double topY, MeshSvgPalette palette)
		{
			double boxW = 78;
			double boxH = 44;
			double pad = 6;
			double boxX = rightX - boxW;
			double boxY = topY;
			string boxOpacity = palette.IsPaper ? "0.96" : "0.88";

			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{boxX}\" y=\"{boxY}\" width=\"{boxW}\" height=\"{boxH}\" rx=\"3\" fill=\"{palette.LegendBoxFill}\" fill-opacity=\"{boxOpacity}\" stroke=\"{palette.LegendBoxStroke}\"/>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<text x=\"{boxX + pad}\" y=\"{boxY + pad + 9}\" fill=\"{palette.TextMuted}\" font-size=\"9\" font-family=\"Segoe UI,sans-serif\">Vías</text>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{boxX + pad}\" y=\"{boxY + 20}\" width=\"10\" height=\"10\" rx=\"1\" fill=\"{palette.MapUiColor(TrackCountToColor(1))}\" stroke=\"{palette.LegendBoxStroke}\"/>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<text x=\"{boxX + pad + 14}\" y=\"{boxY + 28}\" fill=\"{palette.TextPrimary}\" font-size=\"10\" font-family=\"Segoe UI,sans-serif\">única</text>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{boxX + pad}\" y=\"{boxY + 32}\" width=\"10\" height=\"10\" rx=\"1\" fill=\"{palette.MapUiColor(TrackCountToColor(2))}\" stroke=\"{palette.LegendBoxStroke}\"/>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<text x=\"{boxX + pad + 14}\" y=\"{boxY + 40}\" fill=\"{palette.TextPrimary}\" font-size=\"10\" font-family=\"Segoe UI,sans-serif\">doble</text>");
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

		private static double PkToY(MeshYScale scale, long pk, double plotTop, double plotH)
		{
			return scale.PkToY(pk, plotTop, plotH);
		}

		/// <summary>Marcas de estación para el control <c>StationRuler</c> (y el SVG integrado).</summary>
		public static IReadOnlyList<StationMark> BuildStationMarks(RouteView view)
		{
			List<StationMark> marks = new List<StationMark>();
			HashSet<long> seenPk = new HashSet<long>();
			int index = 0;
			while (index < view.Stations.Count)
			{
				StationOnRoute placement = view.Stations[index];
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

		private readonly struct NumberLabel
		{
			private readonly double mvarX;
			private readonly double mvarY;
			private readonly string mvarText;
			private readonly string mvarColor;
			private readonly double mvarAngleDeg;
			private readonly MeshTrainPathBuilder.TrainLabelBand mvarBand;

			public NumberLabel(
				double x,
				double y,
				string text,
				string color,
				double angleDeg,
				MeshTrainPathBuilder.TrainLabelBand band)
			{
				mvarX = x;
				mvarY = y;
				mvarText = text;
				mvarColor = color;
				mvarAngleDeg = angleDeg;
				mvarBand = band;
			}

			public double X
			{
				get { return mvarX; }
			}

			public double Y
			{
				get { return mvarY; }
			}

			public string Text
			{
				get { return mvarText; }
			}

			public string Color
			{
				get { return mvarColor; }
			}

			public double AngleDeg
			{
				get { return mvarAngleDeg; }
			}

			public MeshTrainPathBuilder.TrainLabelBand Band
			{
				get { return mvarBand; }
			}
		}

		/// <summary>Marca de estación en el eje PK de la vista.</summary>
		public readonly struct StationMark
		{
			private readonly long mvarPk;
			private readonly string mvarLabel;
			private readonly bool mvarIsPrincipal;

			public StationMark(long pk, string label, bool isPrincipal)
			{
				mvarPk = pk;
				mvarLabel = label;
				mvarIsPrincipal = isPrincipal;
			}

			public long Pk
			{
				get { return mvarPk; }
			}

			public string Label
			{
				get { return mvarLabel; }
			}

			public bool IsPrincipal
			{
				get { return mvarIsPrincipal; }
			}
		}

		private readonly struct SpeedBand
		{
			private readonly long mvarPkStart;
			private readonly long mvarPkEnd;
			private readonly int mvarSpeedKmh;

			public SpeedBand(long pkStart, long pkEnd, int speedKmh)
			{
				mvarPkStart = pkStart;
				mvarPkEnd = pkEnd;
				mvarSpeedKmh = speedKmh;
			}

			public long PkStart
			{
				get { return mvarPkStart; }
			}

			public long PkEnd
			{
				get { return mvarPkEnd; }
			}

			public int SpeedKmh
			{
				get { return mvarSpeedKmh; }
			}
		}

		private readonly struct TrackBand
		{
			private readonly long mvarPkStart;
			private readonly long mvarPkEnd;
			private readonly int mvarTrackCount;

			public TrackBand(long pkStart, long pkEnd, int trackCount)
			{
				mvarPkStart = pkStart;
				mvarPkEnd = pkEnd;
				mvarTrackCount = trackCount;
			}

			public long PkStart
			{
				get { return mvarPkStart; }
			}

			public long PkEnd
			{
				get { return mvarPkEnd; }
			}

			public int TrackCount
			{
				get { return mvarTrackCount; }
			}
		}
	}
}

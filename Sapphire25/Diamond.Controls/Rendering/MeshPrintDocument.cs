using System;
using System.Globalization;
using System.Text;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Documento de impresión de malla en una sola hoja A3 apaisada:
	/// el diagrama llena toda la superficie; el cajetín DIN se superpone
	/// (plantilla de plano), sin restar área al plot.
	/// </summary>
	public static class MeshPrintDocument
	{
		/// <summary>
		/// Ancho lógico del SVG de malla (aprox. 5 px/mm sobre 420 mm de A3).
		/// </summary>
		public const int DefaultPrintWidth = 2100;

		/// <summary>
		/// Alto lógico del SVG a hoja completa A3 apaisada (297 mm a 5 px/mm).
		/// El cajetín no reduce este alto: se dibuja encima.
		/// </summary>
		public const int DefaultPrintDrawingHeight = 1485;

		/// <summary>Alto de la hoja de impresión (igual al dibujo a página completa).</summary>
		public const int DefaultPrintHeight = DefaultPrintDrawingHeight;

		public sealed class CoverInfo
		{
			public string PlanName { get; set; } = string.Empty;
			public string ViewId { get; set; } = string.Empty;
			public string ViewName { get; set; } = string.Empty;
			public string PathSignature { get; set; } = string.Empty;
			public DayOfWeek? PlanningDay { get; set; }
			public TimeSpan TimeStart { get; set; }
			public TimeSpan TimeEnd { get; set; }
			public long PkMin { get; set; }
			public long PkMax { get; set; }
			public int CirculationCount { get; set; }
			public int VisibleCirculationCount { get; set; }
			public int AsimilationCount { get; set; }
			public int WarningCount { get; set; }
			public int ErrorCount { get; set; }
			public string YScaleLabel { get; set; } = string.Empty;
			public string LayersSummary { get; set; } = string.Empty;
			public DateTime PrintedAt { get; set; } = DateTime.Now;
			public string Notes { get; set; } = string.Empty;
		}

		/// <summary>
		/// SVG de malla en tema papel, estaciones integradas, misma ventana tiempo/PK.
		/// Dimensiones por defecto ajustadas al área de dibujo sobre el cajetín DIN.
		/// </summary>
		public static string BuildMeshSvg(
			Mesh mesh,
			RouteView view,
			TimeSpan timeStart,
			TimeSpan timeEnd,
			long pkMin,
			long pkMax,
			MeshSvgDrawOptions screenOptions,
			int width = DefaultPrintWidth,
			int height = DefaultPrintDrawingHeight)
		{
			if (mesh is null)
			{
				throw new ArgumentNullException(nameof(mesh));
			}

			if (view is null)
			{
				throw new ArgumentNullException(nameof(view));
			}

			// Papel: sin “ahora”, sin relleno pesado de cantones (ahorro tóner);
			// estaciones dentro del SVG; más muestras de traza.
			MeshSvgDrawOptions options = MeshSvgDrawOptions.Create(
				showCantonOccupations: false,
				showTrainPaths: screenOptions.ShowTrainPaths,
				showTrainNumbers: screenOptions.ShowTrainNumbers,
				showConflicts: screenOptions.ShowConflicts,
				showSpeedStrip: screenOptions.ShowSpeedStrip,
				showTrackStrip: screenOptions.ShowTrackStrip,
				showNowLine: false,
				nowTime: null,
				maxPolylineSamples: 128,
				showStationLabels: true,
				externalStationColumn: false,
				yScaleMode: screenOptions.YScaleMode,
				selectedTechnicalId: null,
				paperTheme: true);

			string content = MeshSvgRenderer.RenderContent(
				mesh, view, timeStart, timeEnd, pkMin, pkMax, width, height, options);

			// Hoja completa: el SVG llena 420×297; el cajetín se superpone en CSS.
			return string.Format(
				CultureInfo.InvariantCulture,
				"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{0}\" height=\"{1}\" viewBox=\"0 0 {0} {1}\" class=\"diamond-mesh-print-svg\" preserveAspectRatio=\"none\">{2}</svg>",
				width,
				height,
				content);
		}

		/// <summary>
		/// Cajetín inferior tipo DIN (misma hoja que el dibujo): identificación del plano.
		/// </summary>
		public static string BuildTitleBlockHtml(CoverInfo info)
		{
			if (info is null)
			{
				throw new ArgumentNullException(nameof(info));
			}

			string plan = string.IsNullOrWhiteSpace(info.PlanName) ? "Plan sin nombre" : info.PlanName.Trim();
			string day = info.PlanningDay.HasValue
				? FormatDay(info.PlanningDay.Value)
				: "—";
			string printed = info.PrintedAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("es-ES"));
			string zone = string.IsNullOrWhiteSpace(info.ViewId) ? "—" : info.ViewId.Trim();
			if (!string.IsNullOrWhiteSpace(info.ViewName))
			{
				zone = zone + " · " + info.ViewName.Trim();
			}

			string window = FormatClock(info.TimeStart) + " – " + FormatClock(info.TimeEnd);
			string pkRange = FormatPk(info.PkMin) + " – " + FormatPk(info.PkMax);
			string scale = string.IsNullOrWhiteSpace(info.YScaleLabel) ? "—" : info.YScaleLabel.Trim();
			string layers = string.IsNullOrWhiteSpace(info.LayersSummary) ? "—" : info.LayersSummary.Trim();
			string notes = string.IsNullOrWhiteSpace(info.Notes) ? "—" : info.Notes.Trim();
			string path = string.IsNullOrWhiteSpace(info.PathSignature) ? "—" : info.PathSignature.Trim();

			StringBuilder sb = new StringBuilder(3072);
			sb.Append("<div class=\"diamond-mesh-print-titleblock\" aria-label=\"Cajetín del plano\">");

			// Fila principal: marca | título + meta | recuentos | hoja
			sb.Append("<div class=\"diamond-mesh-print-tb-grid\">");

			sb.Append("<div class=\"diamond-mesh-print-tb-brand\">");
			sb.Append("<div class=\"diamond-mesh-print-tb-brand-name\">Diamond</div>");
			sb.Append("<div class=\"diamond-mesh-print-tb-brand-kind\">Malla horaria</div>");
			sb.Append("<div class=\"diamond-mesh-print-tb-brand-fmt\">A3 · apaisado</div>");
			sb.Append("</div>");

			sb.Append("<div class=\"diamond-mesh-print-tb-main\">");
			sb.Append("<div class=\"diamond-mesh-print-tb-title\">");
			sb.Append(Escape(plan));
			sb.Append("</div>");
			sb.Append("<table class=\"diamond-mesh-print-tb-fields\">");
			AppendTitleField(sb, "Vista / zona", zone);
			AppendTitleField(sb, "Día planif.", day);
			AppendTitleField(sb, "Ventana", window);
			AppendTitleField(sb, "Rango PK", pkRange);
			AppendTitleField(sb, "Escala", scale);
			AppendTitleField(sb, "Camino", path);
			AppendTitleField(sb, "Capas", layers);
			AppendTitleField(sb, "Notas", notes);
			sb.Append("</table>");
			sb.Append("</div>");

			sb.Append("<div class=\"diamond-mesh-print-tb-stats\">");
			AppendStat(sb, "Circulaciones", info.CirculationCount.ToString(CultureInfo.InvariantCulture));
			AppendStat(sb, "En ventana", info.VisibleCirculationCount.ToString(CultureInfo.InvariantCulture));
			AppendStat(sb, "Asimilaciones", info.AsimilationCount.ToString(CultureInfo.InvariantCulture));
			AppendStat(
				sb,
				"Avisos / err.",
				info.WarningCount.ToString(CultureInfo.InvariantCulture)
				+ " / "
				+ info.ErrorCount.ToString(CultureInfo.InvariantCulture));
			AppendStat(sb, "Impreso", printed);
			sb.Append("</div>");

			sb.Append("<div class=\"diamond-mesh-print-tb-sheet\">");
			sb.Append("<div class=\"diamond-mesh-print-tb-sheet-label\">Hoja</div>");
			sb.Append("<div class=\"diamond-mesh-print-tb-sheet-num\">1 / 1</div>");
			sb.Append("<div class=\"diamond-mesh-print-tb-sheet-rev\">Rev. —</div>");
			sb.Append("</div>");

			sb.Append("</div>"); // grid
			sb.Append("</div>"); // titleblock
			return sb.ToString();
		}

		/// <summary>
		/// Compatibilidad: genera el cajetín DIN (antes carátula a página completa).
		/// </summary>
		public static string BuildCoverHtml(CoverInfo info)
		{
			return BuildTitleBlockHtml(info);
		}

		public static int CountVisibleCirculations(
			Mesh mesh,
			RouteView view,
			TimeSpan timeStart,
			TimeSpan timeEnd)
		{
			if (mesh is null || view is null)
			{
				return 0;
			}

			double t0 = timeStart.TotalSeconds;
			double t1 = timeEnd.TotalSeconds;
			int n = 0;
			int i = 0;
			while (i < mesh.Circulations.Count)
			{
				Circulation c = mesh.Circulations[i];
				double dep = c.Departure.TotalSeconds;
				double arr = c.Arrival.TotalSeconds;
				if (arr >= t0 && dep <= t1
					&& MeshCantonGeometry.IsVisibleOnView(c.Asimilation, view))
				{
					n++;
				}

				i++;
			}

			return n;
		}

		private static void AppendTitleField(StringBuilder sb, string label, string value)
		{
			sb.Append("<tr><th>");
			sb.Append(Escape(label));
			sb.Append("</th><td>");
			sb.Append(Escape(value));
			sb.Append("</td></tr>");
		}

		private static void AppendStat(StringBuilder sb, string label, string value)
		{
			sb.Append("<div class=\"diamond-mesh-print-tb-stat\">");
			sb.Append("<span class=\"diamond-mesh-print-tb-stat-lab\">");
			sb.Append(Escape(label));
			sb.Append("</span>");
			sb.Append("<span class=\"diamond-mesh-print-tb-stat-val\">");
			sb.Append(Escape(value));
			sb.Append("</span>");
			sb.Append("</div>");
		}

		private static string Escape(string text)
		{
			return System.Security.SecurityElement.Escape(text) ?? string.Empty;
		}

		private static string FormatClock(TimeSpan ts)
		{
			if (ts < TimeSpan.Zero)
			{
				ts = TimeSpan.Zero;
			}

			int h = (int)ts.TotalHours;
			int m = ts.Minutes;
			return h.ToString("00", CultureInfo.InvariantCulture)
				+ ":"
				+ m.ToString("00", CultureInfo.InvariantCulture);
		}

		private static string FormatPk(long pk)
		{
			long abs = Math.Abs(pk);
			long km = abs / 1000L;
			long m = abs % 1000L;
			string body = km.ToString(CultureInfo.InvariantCulture)
				+ "+"
				+ m.ToString("D3", CultureInfo.InvariantCulture);
			return pk < 0 ? "-" + body : body;
		}

		private static string FormatDay(DayOfWeek day)
		{
			return day switch
			{
				DayOfWeek.Monday => "Lunes",
				DayOfWeek.Tuesday => "Martes",
				DayOfWeek.Wednesday => "Miércoles",
				DayOfWeek.Thursday => "Jueves",
				DayOfWeek.Friday => "Viernes",
				DayOfWeek.Saturday => "Sábado",
				DayOfWeek.Sunday => "Domingo",
				_ => day.ToString()
			};
		}
	}
}

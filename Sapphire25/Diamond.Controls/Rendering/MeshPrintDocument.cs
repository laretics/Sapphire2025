using System;
using System.Globalization;
using System.Text;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Documento de impresión de malla: una hoja apaisada (A3 o A4).
	/// El SVG se genera en coordenadas lógicas fijas y el CSS/JS lo escala
	/// al 100 % del área de página (plot + cajetín DIN en franja inferior).
	/// </summary>
	public static class MeshPrintDocument
	{
		/// <summary>
		/// Ancho lógico del SVG de malla (independiente del papel físico).
		/// </summary>
		public const int DefaultPrintWidth = 2100;

		/// <summary>
		/// Alto lógico de la hoja de referencia (proporción A3 apaisado).
		/// </summary>
		public const int DefaultPrintPageHeight = 1485;

		/// <summary>
		/// Fracción del alto de página reservada al cajetín DIN (~14 %).
		/// Suficiente para la rejilla compacta de campos en A3 y A4.
		/// </summary>
		public const double TitleBlockHeightRatio = 0.14;

		/// <summary>
		/// Alto del cajetín en coordenadas lógicas de la hoja de referencia.
		/// </summary>
		public const int TitleBlockHeight = (int)(DefaultPrintPageHeight * TitleBlockHeightRatio);

		/// <summary>
		/// Alto del SVG de malla = hoja lógica − cajetín (zona útil de dibujo).
		/// </summary>
		public const int DefaultPrintMeshHeight = DefaultPrintPageHeight - TitleBlockHeight;

		/// <summary>Alias: alto del SVG de malla (zona útil).</summary>
		public const int DefaultPrintDrawingHeight = DefaultPrintMeshHeight;

		/// <summary>Alias histórico del alto de malla.</summary>
		public const int DefaultPrintHeight = DefaultPrintMeshHeight;

		/// <summary>Ancho en mm del papel (apaisado).</summary>
		public static double PageWidthMm(MeshPrintPaperSize paper)
		{
			return paper == MeshPrintPaperSize.A4Landscape ? 297.0 : 420.0;
		}

		/// <summary>Alto en mm del papel (apaisado).</summary>
		public static double PageHeightMm(MeshPrintPaperSize paper)
		{
			return paper == MeshPrintPaperSize.A4Landscape ? 210.0 : 297.0;
		}

		/// <summary>Valor CSS <c>@page size</c> (p. ej. <c>A3 landscape</c>).</summary>
		public static string PageSizeCss(MeshPrintPaperSize paper)
		{
			return paper == MeshPrintPaperSize.A4Landscape
				? "A4 landscape"
				: "A3 landscape";
		}

		/// <summary>Etiqueta corta para UI.</summary>
		public static string PaperLabel(MeshPrintPaperSize paper)
		{
			return paper == MeshPrintPaperSize.A4Landscape
				? "A4 apaisado"
				: "A3 apaisado";
		}

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
		/// Por defecto usa solo la zona útil (<see cref="DefaultPrintMeshHeight"/>):
		/// el cajetín DIN ocupa el resto de la hoja y no tapa el plot.
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
			int height = DefaultPrintMeshHeight)
		{
			if (mesh is null)
			{
				throw new ArgumentNullException(nameof(mesh));
			}

			if (view is null)
			{
				throw new ArgumentNullException(nameof(view));
			}

			if (height < 200)
			{
				height = DefaultPrintMeshHeight;
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
				maxPolylineSamples: 160,
				showStationLabels: true,
				externalStationColumn: false,
				yScaleMode: screenOptions.YScaleMode,
				selectedTechnicalId: null,
				paperTheme: true);

			string content = MeshSvgRenderer.RenderContent(
				mesh, view, timeStart, timeEnd, pkMin, pkMax, width, height, options);

			// Solo la zona de dibujo (por encima del cajetín). El pie lo aporta el HTML.
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
			string path = string.IsNullOrWhiteSpace(info.PathSignature) ? "—" : info.PathSignature.Trim();
			// Notas del programa de malla; si no hay, el nombre del plan.
			string notes = string.IsNullOrWhiteSpace(info.Notes) ? plan : info.Notes.Trim();

			StringBuilder sb = new StringBuilder(3072);
			sb.Append("<div class=\"diamond-mesh-print-titleblock\" aria-label=\"Cajetín del plano\">");

			// Marca | título + rejilla 5+2+notas | stats | hoja
			sb.Append("<div class=\"diamond-mesh-print-tb-grid\">");

			sb.Append("<div class=\"diamond-mesh-print-tb-brand\">");
			sb.Append("<div class=\"diamond-mesh-print-tb-brand-name\">Diamond</div>");
			sb.Append("<div class=\"diamond-mesh-print-tb-brand-kind\">Malla</div>");
			sb.Append("</div>");

			sb.Append("<div class=\"diamond-mesh-print-tb-main\">");
			sb.Append("<div class=\"diamond-mesh-print-tb-title\">");
			sb.Append(Escape(plan));
			sb.Append("</div>");
			// Fila 1: Vista · Día · Ventana · PK · Escala (5 celdas iguales)
			// Fila 2: Camino · Capas (1 celda c/u) + Notas (3 celdas de ancho)
			sb.Append("<div class=\"diamond-mesh-print-tb-fields\">");
			AppendFieldCell(sb, "Vista", zone, "diamond-mesh-print-tb-cell");
			AppendFieldCell(sb, "Día", day, "diamond-mesh-print-tb-cell");
			AppendFieldCell(sb, "Ventana", window, "diamond-mesh-print-tb-cell");
			AppendFieldCell(sb, "PK", pkRange, "diamond-mesh-print-tb-cell");
			AppendFieldCell(sb, "Escala", scale, "diamond-mesh-print-tb-cell");
			AppendFieldCell(sb, "Camino", path, "diamond-mesh-print-tb-cell");
			AppendFieldCell(sb, "Capas", layers, "diamond-mesh-print-tb-cell");
			AppendFieldCell(sb, "Notas", notes, "diamond-mesh-print-tb-cell diamond-mesh-print-tb-cell-notes");
			sb.Append("</div>");
			sb.Append("</div>");

			sb.Append("<div class=\"diamond-mesh-print-tb-stats\">");
			AppendStat(sb, "Circ.", info.CirculationCount.ToString(CultureInfo.InvariantCulture));
			AppendStat(sb, "Ventana", info.VisibleCirculationCount.ToString(CultureInfo.InvariantCulture));
			AppendStat(sb, "Asim.", info.AsimilationCount.ToString(CultureInfo.InvariantCulture));
			AppendStat(
				sb,
				"Av/Err",
				info.WarningCount.ToString(CultureInfo.InvariantCulture)
				+ "/"
				+ info.ErrorCount.ToString(CultureInfo.InvariantCulture));
			AppendStat(sb, "Impreso", printed);
			sb.Append("</div>");

			sb.Append("<div class=\"diamond-mesh-print-tb-sheet\">");
			sb.Append("<div class=\"diamond-mesh-print-tb-sheet-label\">Hoja</div>");
			sb.Append("<div class=\"diamond-mesh-print-tb-sheet-num\">1/1</div>");
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

		private static void AppendFieldCell(StringBuilder sb, string label, string value, string cssClass)
		{
			sb.Append("<div class=\"");
			sb.Append(cssClass);
			sb.Append("\">");
			sb.Append("<span class=\"diamond-mesh-print-tb-cell-lab\">");
			sb.Append(Escape(label));
			sb.Append("</span>");
			sb.Append("<span class=\"diamond-mesh-print-tb-cell-val\" title=\"");
			sb.Append(Escape(value));
			sb.Append("\">");
			sb.Append(Escape(value));
			sb.Append("</span>");
			sb.Append("</div>");
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

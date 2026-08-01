using System;
using System.Globalization;
using System.Text;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Documento de impresión de malla: carátula + SVG de la ventana actual (tema papel).
	/// </summary>
	public static class MeshPrintDocument
	{
		/// <summary>Ancho lógico del SVG de malla (aprox. A3 apaisado a ~150 dpi).</summary>
		public const int DefaultPrintWidth = 2100;

		/// <summary>Alto lógico del SVG (420×297 mm → ratio ~1.414).</summary>
		public const int DefaultPrintHeight = 1485;

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
			int height = DefaultPrintHeight)
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

			return string.Format(
				CultureInfo.InvariantCulture,
				"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{0}\" height=\"{1}\" viewBox=\"0 0 {0} {1}\" class=\"diamond-mesh-print-svg\" preserveAspectRatio=\"xMidYMid meet\">{2}</svg>",
				width,
				height,
				content);
		}

		/// <summary>Marcado HTML de la carátula (una página de impresión).</summary>
		public static string BuildCoverHtml(CoverInfo info)
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
			string zone = info.ViewId;
			if (!string.IsNullOrWhiteSpace(info.ViewName))
			{
				zone = info.ViewId + " — " + info.ViewName;
			}

			StringBuilder sb = new StringBuilder(2048);
			sb.Append("<div class=\"diamond-mesh-print-cover-inner\">");
			sb.Append("<div class=\"diamond-mesh-print-brand\">Diamond · Malla horaria</div>");
			sb.Append("<h1 class=\"diamond-mesh-print-title\">");
			sb.Append(Escape(plan));
			sb.Append("</h1>");
			sb.Append("<p class=\"diamond-mesh-print-sub\">Documento de impresión · tema papel (oscuro sobre blanco)</p>");

			sb.Append("<table class=\"diamond-mesh-print-meta\">");
			AppendRow(sb, "Fecha de impresión", printed);
			AppendRow(sb, "Día de planificación", day);
			AppendRow(sb, "Zona / vista", zone);
			if (!string.IsNullOrWhiteSpace(info.PathSignature))
			{
				AppendRow(sb, "Firma de camino", info.PathSignature);
			}

			AppendRow(sb, "Ventana temporal",
				FormatClock(info.TimeStart) + " – " + FormatClock(info.TimeEnd));
			AppendRow(sb, "Rango PK",
				FormatPk(info.PkMin) + " – " + FormatPk(info.PkMax));
			AppendRow(sb, "Escala espacial",
				string.IsNullOrWhiteSpace(info.YScaleLabel) ? "—" : info.YScaleLabel);
			AppendRow(sb, "Circulaciones (malla)",
				info.CirculationCount.ToString(CultureInfo.InvariantCulture));
			AppendRow(sb, "Circulaciones en ventana",
				info.VisibleCirculationCount.ToString(CultureInfo.InvariantCulture));
			AppendRow(sb, "Asimilaciones",
				info.AsimilationCount.ToString(CultureInfo.InvariantCulture));
			AppendRow(sb, "Avisos / errores",
				info.WarningCount.ToString(CultureInfo.InvariantCulture)
				+ " / "
				+ info.ErrorCount.ToString(CultureInfo.InvariantCulture));
			if (!string.IsNullOrWhiteSpace(info.LayersSummary))
			{
				AppendRow(sb, "Capas dibujadas", info.LayersSummary);
			}

			if (!string.IsNullOrWhiteSpace(info.Notes))
			{
				AppendRow(sb, "Notas", info.Notes);
			}

			sb.Append("</table>");

			sb.Append("<div class=\"diamond-mesh-print-footer-note\">");
			sb.Append("La página siguiente reproduce la ventana de zoom actual de la malla. ");
			sb.Append("Colores de traza adaptados a impresión (matiz conservado; tonos claros→tinta oscura, oscuros→tinta media). ");
			sb.Append("Sin relleno de cantones ni línea «ahora» para reducir consumo de tóner.");
			sb.Append("</div>");
			sb.Append("</div>");
			return sb.ToString();
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

		private static void AppendRow(StringBuilder sb, string label, string value)
		{
			sb.Append("<tr><th>");
			sb.Append(Escape(label));
			sb.Append("</th><td>");
			sb.Append(Escape(value));
			sb.Append("</td></tr>");
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

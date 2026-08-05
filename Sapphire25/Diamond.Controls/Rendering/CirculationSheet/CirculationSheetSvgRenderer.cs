using System.Globalization;
using System.Text;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Ficha de marcha SVG (A4, B/N). Columnas desfasadas: VMx y tiempo concedido
	/// entre PKs; vía doble/única con celdas fusionadas.
	/// </summary>
	public static class CirculationSheetSvgRenderer
	{
		public const double PageWidth = 595.28;
		public const double PageHeight = 841.89;

		private const double MarginL = 26;
		private const double MarginR = 34;
		private const double MarginT = 20;
		private const double MarginB = 34;

		// Vía | St Km | VMx | Dependencia | Com | Hora | Conc. | Cruz
		private const double ColVia = 40;
		private const double ColStKm = 36;
		private const double ColVmx = 30;
		private const double ColDep = 200;
		private const double ColCom = 28;
		private const double ColHora = 42;
		private const double ColConc = 34;
		private const double ColCruz = 50;

		private const double HeaderBandH = 22;
		private const double SubHeaderH = 15;
		private const double ColHeaderH = 17;
		private const double MinRowH = 13;
		private const double MaxRowH = 17;

		private static double TableWidth
		{
			get
			{
				return ColVia + ColStKm + ColVmx + ColDep + ColCom + ColHora + ColConc + ColCruz;
			}
		}

		public static string RenderPage(CirculationSheetDocument document, CirculationSheetPage page)
		{
			if (document is null)
			{
				throw new ArgumentNullException(nameof(document));
			}

			if (page is null)
			{
				throw new ArgumentNullException(nameof(page));
			}

			IReadOnlyList<CirculationSheetFrontier> rows = page.Frontiers;
			StringBuilder sb = new StringBuilder(24 * 1024);
			// A4 vertical (portrait). preserveAspectRatio evita recortes raros al escalar en pantalla.
			sb.Append(CultureInfo.InvariantCulture,
				$"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(PageWidth)}\" height=\"{F(PageHeight)}\" viewBox=\"0 0 {F(PageWidth)} {F(PageHeight)}\" preserveAspectRatio=\"xMidYMin meet\" class=\"diamond-circ-sheet-svg\">");

			// Papel (sin color)
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"0\" y=\"0\" width=\"{F(PageWidth)}\" height=\"{F(PageHeight)}\" fill=\"#ffffff\"/>");

			double tableLeft = MarginL;
			double tableW = TableWidth;
			double tableRight = tableLeft + tableW;

			double y = MarginT;

			// Cabecera negra
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{F(tableLeft)}\" y=\"{F(y)}\" width=\"{F(tableW)}\" height=\"{F(HeaderBandH)}\" fill=\"#000\"/>");
			string titleLeft = string.IsNullOrEmpty(document.TrainTitle)
				? document.TrainNumber + ".-"
				: document.TrainNumber + ".- " + document.TrainTitle.ToUpperInvariant();
			sb.Append(Text(tableLeft + 6, y + HeaderBandH * 0.72, titleLeft, 12, "700", "#fff", "start"));
			string tipo = string.IsNullOrEmpty(document.MaterialType) ? string.Empty : "Tipo: " + document.MaterialType;
			sb.Append(Text(tableRight - 6, y + HeaderBandH * 0.72, tipo, 11, "600", "#fff", "end"));
			y += HeaderBandH;

			// Subcabecera
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{F(tableLeft)}\" y=\"{F(y)}\" width=\"{F(tableW)}\" height=\"{F(SubHeaderH)}\" fill=\"#eee\" stroke=\"#000\" stroke-width=\"0.8\"/>");
			sb.Append(Text(tableLeft + 6, y + SubHeaderH * 0.72, document.LocationLine, 8, "600", "#000", "start"));
			sb.Append(Text(tableRight - 6, y + SubHeaderH * 0.72, document.MarchId, 7.5, "500", "#000", "end"));
			y += SubHeaderH;

			// Cabecera columnas
			double colHeaderY = y;
			DrawColumnHeaders(sb, tableLeft, colHeaderY, ColHeaderH);
			y += ColHeaderH;
			double bodyTop = y;

			// Alto útil del cuerpo: no superar el pie de página (evita que el SVG
			// “crezca” de facto y el navegador parta la hoja en una 2.ª página en blanco).
			double bodyBottom = PageHeight - MarginB - 16;
			double availBody = bodyBottom - bodyTop;
			if (availBody < MinRowH)
			{
				availBody = MinRowH;
			}

			int n = Math.Max(1, rows.Count);
			// Si hay demasiadas filas para MinRowH, reducir altura de fila (nunca desbordar).
			double rowH = availBody / n;
			if (rowH > MaxRowH)
			{
				rowH = MaxRowH;
			}

			if (rowH < MinRowH && n * MinRowH <= availBody + 0.5)
			{
				rowH = MinRowH;
			}

			// Cuerpo siempre dentro del papel.
			if (n * rowH > availBody)
			{
				rowH = availBody / n;
			}

			double bodyH = rowH * Math.Max(rows.Count, 1);

			// Fondo columna VMx (gris claro, negrita)
			double vmxX = tableLeft + ColVia + ColStKm;
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{F(vmxX)}\" y=\"{F(bodyTop)}\" width=\"{F(ColVmx)}\" height=\"{F(bodyH)}\" fill=\"#e8e8e8\"/>");

			// Marco exterior
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{F(tableLeft)}\" y=\"{F(colHeaderY)}\" width=\"{F(tableW)}\" height=\"{F(ColHeaderH + bodyH)}\" fill=\"none\" stroke=\"#000\" stroke-width=\"1.15\"/>");

			if (rows.Count == 0)
			{
				sb.Append(Text(tableLeft + tableW * 0.5, bodyTop + 20, "(sin fronteras)", 10, "400", "#000", "middle"));
			}
			else
			{
				DrawBody(sb, tableLeft, bodyTop, rowH, rows);
			}

			// Verticales de columna (altura total tabla)
			DrawVerticals(sb, tableLeft, colHeaderY, ColHeaderH + bodyH);

			// Pie
			double footerY = PageHeight - MarginB + 2;
			double triX = tableLeft + 6;
			sb.Append(CultureInfo.InvariantCulture,
				$"<polygon points=\"{F(triX)},{F(footerY)} {F(triX + 9)},{F(footerY)} {F(triX + 4.5)},{F(footerY - 9)}\" fill=\"#000\"/>");
			sb.Append(Text(tableLeft + 24, footerY - 1, document.EditionLabel, 7, "400", "#000", "start"));
			string pageLabel = "Pág " + page.PageNumber.ToString(CultureInfo.InvariantCulture)
				+ " de " + page.PageCount.ToString(CultureInfo.InvariantCulture);
			sb.Append(Text(tableRight, footerY - 1, pageLabel, 8, "600", "#000", "end"));

			double sideX = tableRight + 12;
			double sideY = bodyTop + bodyH * 0.5;
			sb.Append(CultureInfo.InvariantCulture,
				$"<text x=\"{F(sideX)}\" y=\"{F(sideY)}\" fill=\"#000\" font-size=\"7.5\" font-weight=\"600\" font-family=\"Arial,Helvetica,sans-serif\" text-anchor=\"middle\" transform=\"rotate(90 {F(sideX)},{F(sideY)})\">DIRECCIÓN DE SECCIÓN</text>");

			sb.Append("</svg>");
			return sb.ToString();
		}

		public static IReadOnlyList<string> RenderAllPages(CirculationSheetDocument document)
		{
			if (document is null)
			{
				throw new ArgumentNullException(nameof(document));
			}

			List<string> list = new List<string>(document.Pages.Count);
			int i = 0;
			while (i < document.Pages.Count)
			{
				list.Add(RenderPage(document, document.Pages[i]));
				i++;
			}

			return list;
		}

		private static void DrawColumnHeaders(StringBuilder sb, double left, double y, double h)
		{
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{F(left)}\" y=\"{F(y)}\" width=\"{F(TableWidth)}\" height=\"{F(h)}\" fill=\"#ddd\"/>");
			double x = left;
			HeaderCell(sb, x, y, h, ColVia, "Vía");
			x += ColVia;
			HeaderCell(sb, x, y, h, ColStKm, "St Km");
			x += ColStKm;
			HeaderCell(sb, x, y, h, ColVmx, "VMx");
			x += ColVmx;
			HeaderCell(sb, x, y, h, ColDep, "Dependencia");
			x += ColDep;
			HeaderCell(sb, x, y, h, ColCom, "Com");
			x += ColCom;
			HeaderCell(sb, x, y, h, ColHora, "Hora");
			x += ColHora;
			HeaderCell(sb, x, y, h, ColConc, "Conc.");
			x += ColConc;
			HeaderCell(sb, x, y, h, ColCruz, "Cruz");
		}

		private static void HeaderCell(StringBuilder sb, double x, double y, double h, double w, string label)
		{
			sb.Append(CultureInfo.InvariantCulture,
				$"<line x1=\"{F(x)}\" y1=\"{F(y)}\" x2=\"{F(x)}\" y2=\"{F(y + h)}\" stroke=\"#000\" stroke-width=\"0.55\"/>");
			sb.Append(Text(x + w * 0.5, y + h * 0.7, label, 7, "700", "#000", "middle"));
		}

		private static void DrawVerticals(StringBuilder sb, double left, double y, double h)
		{
			double[] xs =
			{
				left,
				left + ColVia,
				left + ColVia + ColStKm,
				left + ColVia + ColStKm + ColVmx,
				left + ColVia + ColStKm + ColVmx + ColDep,
				left + ColVia + ColStKm + ColVmx + ColDep + ColCom,
				left + ColVia + ColStKm + ColVmx + ColDep + ColCom + ColHora,
				left + ColVia + ColStKm + ColVmx + ColDep + ColCom + ColHora + ColConc,
				left + ColVia + ColStKm + ColVmx + ColDep + ColCom + ColHora + ColConc + ColCruz
			};
			int i = 0;
			while (i < xs.Length)
			{
				double sw = (i == 0 || i == xs.Length - 1) ? 1.1 : 0.55;
				sb.Append(CultureInfo.InvariantCulture,
					$"<line x1=\"{F(xs[i])}\" y1=\"{F(y)}\" x2=\"{F(xs[i])}\" y2=\"{F(y + h)}\" stroke=\"#000\" stroke-width=\"{F(sw)}\"/>");
				i++;
			}
		}

		private static void DrawBody(
			StringBuilder sb,
			double tableLeft,
			double bodyTop,
			double rowH,
			IReadOnlyList<CirculationSheetFrontier> rows)
		{
			int n = rows.Count;
			// Y de cada línea de PK (centros de fila alineados con St Km / Dependencia / Com / Hora)
			// Fila i ocupa [bodyTop + i*rowH, bodyTop + (i+1)*rowH]; el PK se dibuja en el centro.

			// —— 1) Columna Vía: runs fusionados por tipo de vía del tramo saliente ——
			DrawMergedViaColumn(sb, tableLeft, bodyTop, rowH, rows);

			// —— 2) Filas de punto (St Km, Dependencia, Com, Hora) + líneas horizontales selectivas ——
			int i = 0;
			while (i < n)
			{
				CirculationSheetFrontier row = rows[i];
				double y0 = bodyTop + i * rowH;
				double y1 = y0 + rowH;
				double cy = y0 + rowH * 0.5;

				// Línea horizontal en columnas de punto (no en Vía/VMx/Conc. fusionadas)
				DrawPointRowHorizontal(sb, tableLeft, y1, rows, i);

				// St Km
				double stX = tableLeft + ColVia;
				sb.Append(Text(stX + ColStKm - 3, cy + 3, row.StationKm, 8, "600", "#000", "end"));

				// Dependencia
				double depX = tableLeft + ColVia + ColStKm + ColVmx;
				DrawDependency(sb, depX, y0, rowH, row);

				// Com
				double comX = depX + ColDep;
				DrawCom(sb, comX, cy, row);

				// Hora
				double horaX = comX + ColCom;
				string hora = FormatRowClock(row);
				sb.Append(Text(horaX + ColHora * 0.5, cy + 3, hora, 8.5, "700", "#000", "middle"));

				// Cruz (sin líneas de división horizontales; solo números de tren)
				double cruzX = horaX + ColHora + ColConc;
				if (!string.IsNullOrEmpty(row.CrossingTrains))
				{
					sb.Append(Text(cruzX + ColCruz * 0.5, cy + 3, Truncate(row.CrossingTrains, 10), 7, "600", "#000", "middle"));
				}

				i++;
			}

			// —— 3) VMx desfasado (entre PKs), fusionado si la limitación no cambia ——
			DrawOffsetVmx(sb, tableLeft + ColVia + ColStKm, bodyTop, rowH, rows);

			// —— 4) Tiempo concedido desfasado ——
			double concX = tableLeft + ColVia + ColStKm + ColVmx + ColDep + ColCom + ColHora;
			DrawOffsetGranted(sb, concX, bodyTop, rowH, rows);

			// Línea superior e inferior del cuerpo en todo el ancho
			sb.Append(CultureInfo.InvariantCulture,
				$"<line x1=\"{F(tableLeft)}\" y1=\"{F(bodyTop)}\" x2=\"{F(tableLeft + TableWidth)}\" y2=\"{F(bodyTop)}\" stroke=\"#000\" stroke-width=\"0.7\"/>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<line x1=\"{F(tableLeft)}\" y1=\"{F(bodyTop + n * rowH)}\" x2=\"{F(tableLeft + TableWidth)}\" y2=\"{F(bodyTop + n * rowH)}\" stroke=\"#000\" stroke-width=\"0.7\"/>");
		}

		private static void DrawPointRowHorizontal(
			StringBuilder sb,
			double tableLeft,
			double y,
			IReadOnlyList<CirculationSheetFrontier> rows,
			int rowIndex)
		{
			// No dibujar la línea inferior de la última en las columnas de tramo si no hay más.
			// Siempre dibujamos StKm–Hora; Vía/VMx/Conc. se gestionan en sus merges.
			double x0 = tableLeft + ColVia;
			double x1 = tableLeft + ColVia + ColStKm; // fin StKm
			double x2 = x1 + ColVmx; // fin VMx — no trazar aquí (desfase)
			double x3 = x2 + ColDep;
			double x4 = x3 + ColCom;
			double x5 = x4 + ColHora;
			double x6 = x5 + ColConc; // no trazar Conc aquí
			double x7 = x6 + ColCruz; // Cruz: sin rayas

			// St Km
			sb.Append(HLine(x0, x1, y));
			// Dependencia + Com + Hora (no Conc. ni Cruz: Conc. desfasada; Cruz sin divisiones)
			sb.Append(HLine(x2, x5, y));
			// x6–x7 = Cruz: sin líneas horizontales a propósito
		}

		private static string HLine(double x0, double x1, double y)
		{
			return string.Format(
				CultureInfo.InvariantCulture,
				"<line x1=\"{0}\" y1=\"{1}\" x2=\"{2}\" y2=\"{1}\" stroke=\"#000\" stroke-width=\"0.45\"/>",
				F(x0), F(y), F(x1));
		}

		private static void DrawMergedViaColumn(
			StringBuilder sb,
			double tableLeft,
			double bodyTop,
			double rowH,
			IReadOnlyList<CirculationSheetFrontier> rows)
		{
			int n = rows.Count;
			if (n < 2)
			{
				return;
			}

			// Runs de tramos salientes con el mismo tipo de vía.
			// El bloque visual cubre desde la fila del PK inicial hasta la del PK final del run.
			int i = 0;
			while (i < n - 1)
			{
				bool isDouble = rows[i].OutgoingIsDoubleTrack;
				int j = i + 1;
				while (j < n - 1 && rows[j].OutgoingIsDoubleTrack == isDouble)
				{
					j++;
				}

				// Tramos i .. j-1 → filas i .. j inclusive
				double y0 = bodyTop + i * rowH;
				double y1 = bodyTop + (j + 1) * rowH;
				string label = isDouble ? "Doble" : "Única";
				double cx = tableLeft + ColVia * 0.5;
				double cy = (y0 + y1) * 0.5;
				sb.Append(CultureInfo.InvariantCulture,
					$"<text x=\"{F(cx)}\" y=\"{F(cy)}\" fill=\"#000\" font-size=\"8\" font-weight=\"700\" font-family=\"Arial,Helvetica,sans-serif\" text-anchor=\"middle\" transform=\"rotate(-90 {F(cx)},{F(cy)})\">{XmlEscape(label)}</text>");

				// Separación solo si cambia el tipo en la frontera j (altura del PK j)
				if (j < n - 1)
				{
					double sepY = bodyTop + (j + 0.5) * rowH;
					sb.Append(CultureInfo.InvariantCulture,
						$"<line x1=\"{F(tableLeft)}\" y1=\"{F(sepY)}\" x2=\"{F(tableLeft + ColVia)}\" y2=\"{F(sepY)}\" stroke=\"#000\" stroke-width=\"0.9\"/>");
				}

				i = j;
			}
		}

		private static void DrawOffsetVmx(
			StringBuilder sb,
			double colX,
			double bodyTop,
			double rowH,
			IReadOnlyList<CirculationSheetFrontier> rows)
		{
			int n = rows.Count;
			if (n < 2)
			{
				return;
			}

			// Texto VMx centrado en cada run de tramos con la misma limitación (desfasado entre PKs).
			int i = 0;
			while (i < n - 1)
			{
				int? vmax = rows[i].OutgoingVmaxKmh;
				int j = i + 1;
				while (j < n - 1 && SameVmax(rows[j].OutgoingVmaxKmh, vmax))
				{
					j++;
				}

				// Entre el PK i y el PK j
				double yStart = bodyTop + (i + 0.5) * rowH;
				double yEnd = bodyTop + (j + 0.5) * rowH;
				double cy = (yStart + yEnd) * 0.5;
				if (vmax.HasValue)
				{
					sb.Append(Text(colX + ColVmx * 0.5, cy + 3,
						vmax.Value.ToString(CultureInfo.InvariantCulture), 9, "700", "#000", "middle"));
				}

				i = j;
			}

			// División a la altura del PK solo donde cambia la limitación saliente.
			int k = 1;
			while (k < n - 1)
			{
				int? prev = rows[k - 1].OutgoingVmaxKmh;
				int? next = rows[k].OutgoingVmaxKmh;
				if (!SameVmax(prev, next))
				{
					double lineY = bodyTop + (k + 0.5) * rowH;
					sb.Append(CultureInfo.InvariantCulture,
						$"<line x1=\"{F(colX)}\" y1=\"{F(lineY)}\" x2=\"{F(colX + ColVmx)}\" y2=\"{F(lineY)}\" stroke=\"#000\" stroke-width=\"0.85\"/>");
				}

				k++;
			}
		}

		private static void DrawOffsetGranted(
			StringBuilder sb,
			double colX,
			double bodyTop,
			double rowH,
			IReadOnlyList<CirculationSheetFrontier> rows)
		{
			int n = rows.Count;
			int i = 0;
			while (i < n - 1)
			{
				// Entre PK i e i+1
				double y0 = bodyTop + (i + 0.5) * rowH;
				double y1 = bodyTop + (i + 1.5) * rowH;
				double cy = (y0 + y1) * 0.5;
				string g = CirculationSheetDocument.FormatGrantedMinutes(rows[i].GrantedToNext);
				if (g.Length > 0)
				{
					sb.Append(Text(colX + ColConc * 0.5, cy + 3, g, 8, "600", "#000", "middle"));
				}

				i++;
			}

			// Divisiones a la altura de cada PK intermedio (siempre, cada tramo es independiente)
			int k = 1;
			while (k < n - 1)
			{
				double lineY = bodyTop + (k + 0.5) * rowH;
				sb.Append(CultureInfo.InvariantCulture,
					$"<line x1=\"{F(colX)}\" y1=\"{F(lineY)}\" x2=\"{F(colX + ColConc)}\" y2=\"{F(lineY)}\" stroke=\"#000\" stroke-width=\"0.45\"/>");
				k++;
			}
		}

		private static bool SameVmax(int? a, int? b)
		{
			if (!a.HasValue && !b.HasValue)
			{
				return true;
			}

			if (!a.HasValue || !b.HasValue)
			{
				return false;
			}

			return a.Value == b.Value;
		}

		private static void DrawDependency(
			StringBuilder sb,
			double x,
			double y0,
			double rowH,
			CirculationSheetFrontier row)
		{
			double textY = y0 + rowH * 0.5 + 3;
			string name = Truncate(row.DependencyName, 36);
			const double fontSize = 8.0;

			if (row.MarkKind == CirculationSheetMarkKind.PrincipalStation)
			{
				// Blanco sobre negro: el rectángulo negro solo envuelve el texto, no toda la casilla.
				double textW = EstimateTextWidth(name, fontSize);
				double padX = 3.0;
				double padY = 1.2;
				double rh = fontSize + 2.0 * padY;
				double rw = textW + 2.0 * padX;
				double rx = x + 5.0;
				double ry = y0 + (rowH - rh) * 0.5;
				sb.Append(CultureInfo.InvariantCulture,
					$"<rect x=\"{F(rx)}\" y=\"{F(ry)}\" width=\"{F(rw)}\" height=\"{F(rh)}\" fill=\"#000\"/>");
				sb.Append(Text(rx + padX, textY, name, fontSize, "700", "#fff", "start"));
			}
			else
			{
				// Negro sobre blanco (apeadero o PK de limitación)
				sb.Append(Text(x + 6, textY, name, 7.5, "400", "#000", "start"));
			}
		}

		/// <summary>Ancho aproximado de texto en mayúsculas (Arial ~0.56 em).</summary>
		private static double EstimateTextWidth(string text, double fontSize)
		{
			if (string.IsNullOrEmpty(text))
			{
				return fontSize;
			}

			return text.Length * fontSize * 0.56;
		}

		private static void DrawCom(StringBuilder sb, double comX, double cy, CirculationSheetFrontier row)
		{
			if (!row.IsCommercialStop || row.Dwell <= TimeSpan.Zero)
			{
				return;
			}

			string text = CirculationSheetDocument.FormatCommercialDwell(row.Dwell, out bool circle);
			if (circle)
			{
				sb.Append(CultureInfo.InvariantCulture,
					$"<circle cx=\"{F(comX + ColCom * 0.5)}\" cy=\"{F(cy)}\" r=\"2.6\" fill=\"#000\"/>");
			}
			else if (text.Length > 0)
			{
				sb.Append(Text(comX + ColCom * 0.5, cy + 3, text, 8, "600", "#000", "middle"));
			}
		}

		private static string FormatRowClock(CirculationSheetFrontier row)
		{
			if (row.IsOrigin)
			{
				return CirculationSheetDocument.FormatSheetTime(row.Departure);
			}

			if (row.IsDestination)
			{
				return CirculationSheetDocument.FormatSheetTime(row.Arrival ?? row.Departure);
			}

			if (row.IsCommercialStop)
			{
				// Hora de salida comercial
				return CirculationSheetDocument.FormatSheetTime(row.Departure ?? row.Arrival);
			}

			// Paso / frontera de V
			return CirculationSheetDocument.FormatSheetTime(row.Departure ?? row.Arrival);
		}

		private static string Text(
			double x, double y, string content,
			double fontSize, string weight, string fill, string anchor)
		{
			return string.Format(
				CultureInfo.InvariantCulture,
				"<text x=\"{0}\" y=\"{1}\" fill=\"{2}\" font-size=\"{3}\" font-weight=\"{4}\" font-family=\"Arial,Helvetica,sans-serif\" text-anchor=\"{5}\">{6}</text>",
				F(x), F(y), fill, F(fontSize), weight, anchor, XmlEscape(content));
		}

		private static string Truncate(string s, int max)
		{
			if (string.IsNullOrEmpty(s) || s.Length <= max)
			{
				return s ?? string.Empty;
			}

			return s.Substring(0, max - 1) + "…";
		}

		private static string XmlEscape(string? s)
		{
			if (string.IsNullOrEmpty(s))
			{
				return string.Empty;
			}

			return s
				.Replace("&", "&amp;", StringComparison.Ordinal)
				.Replace("<", "&lt;", StringComparison.Ordinal)
				.Replace(">", "&gt;", StringComparison.Ordinal)
				.Replace("\"", "&quot;", StringComparison.Ordinal);
		}

		private static string F(double v)
		{
			return v.ToString("0.##", CultureInfo.InvariantCulture);
		}
	}
}

using System.Globalization;
using System.Text;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Libro itinerario SVG: hoja A4 apaisada con dos mitades (páginas de libro)
	/// lado a lado. Filas repartidas de forma equilibrada y estiradas a toda la
	/// altura útil de cada mitad. Cabeceras y pies a la misma cota en ambas.
	/// </summary>
	public static class CirculationSheetSvgRenderer
	{
		/// <summary>Ancho de la hoja física A4 apaisada (pt).</summary>
		public const double SheetWidth = 841.89;

		/// <summary>Alto de la hoja física A4 apaisada (pt).</summary>
		public const double SheetHeight = 595.28;

		/// <summary>Compat: ancho de una mitad (página de libro).</summary>
		public static double PageWidth
		{
			get { return (SheetWidth - SheetOuterMargin * 2 - PanelGutter) * 0.5; }
		}

		/// <summary>Compat: alto de una mitad (= alto de hoja útil).</summary>
		public static double PageHeight
		{
			get { return SheetHeight - SheetOuterMargin * 2; }
		}

		private const double SheetOuterMargin = 10;
		private const double PanelGutter = 12;

		// Márgenes internos de cada mitad (página de libro).
		private const double PanelPadL = 8;
		private const double PanelPadR = 10;
		private const double PanelPadT = 8;
		private const double PanelPadB = 16; // pie

		// Vía | PK | Max | Dependencia | Com | Hora | Conc. | Obs.
		// Ancho total ~390 pt para caber en media A4 apaisada.
		private const double ColVia = 30;
		private const double ColStKm = 32;
		private const double ColVmx = 26;
		private const double ColDep = 168;
		private const double ColCom = 24;
		private const double ColHora = 38;
		private const double ColConc = 30;
		private const double ColCruz = 40;

		private const double HeaderBandH = 18;
		/// <summary>Alto de una línea de texto Loc. / ruta en la franja bajo Tipo.</summary>
		private const double SubHeaderLineH = 11;
		/// <summary>Texto Loc.+ruta (solo 1.ª hoja); la franja real es <see cref="HeaderQrBandH"/>.</summary>
		private const double SubHeaderH = SubHeaderLineH * 2;
		/// <summary>
		/// QR bajo «Tipo»: base 52×4/3 pt (~69,3) menos 20 % ≈ 55,5 pt.
		/// </summary>
		private const double HeaderQrSize = 52.0 * 4.0 / 3.0 * 0.8;
		/// <summary>
		/// Franja bajo la banda oscura: QR a la derecha (todas las hojas) y Loc./ruta a la izquierda (1.ª).
		/// </summary>
		private const double HeaderQrBandH = HeaderQrSize + 3.0;
		private const double ColHeaderH = 15;
		private const double MinRowH = 11;

		private static CirculationSheetPalette DefaultPalette
		{
			get { return CirculationSheetPalette.Print; }
		}

		private static double TableWidth
		{
			get
			{
				return ColVia + ColStKm + ColVmx + ColDep + ColCom + ColHora + ColConc + ColCruz;
			}
		}

		/// <summary>
		/// Alto útil del cuerpo en la 1.ª hoja del tren (con subcabecera de descripción).
		/// Es el caso más restrictivo; la paginación usa este valor.
		/// </summary>
		public static double AvailableBodyHeight
		{
			get { return AvailableBodyHeightForTrainPage(firstPageOfTrain: true); }
		}

		/// <summary>
		/// Alto útil del cuerpo. La 1.ª hoja reserva franja Loc./ruta + QR bajo Tipo;
		/// las de continuación solo banda de número/Tipo (máximo cuerpo de tabla).
		/// </summary>
		public static double AvailableBodyHeightForTrainPage(bool firstPageOfTrain)
		{
			double panelH = PageHeight;
			double reserved = PanelPadT + HeaderBandH + ColHeaderH;
			if (firstPageOfTrain)
			{
				reserved += HeaderQrBandH;
			}

			double avail = panelH - PanelPadB - reserved;
			return avail < MinRowH ? MinRowH : avail;
		}

		/// <summary>
		/// Máximo de filas con altura mínima legible en una mitad (1.ª hoja del tren,
		/// la más restrictiva por la subcabecera Loc./ruta).
		/// </summary>
		public static int MaxRowsPerBookPage
		{
			get { return MaxRowsOnTrainPage(firstPageOfTrain: true); }
		}

		/// <summary>
		/// Máximo de filas legibles (alto ≥ <see cref="MinRowH"/>) en una mitad de libro
		/// para la 1.ª hoja del tren o una de continuación.
		/// </summary>
		public static int MaxRowsOnTrainPage(bool firstPageOfTrain)
		{
			double body = AvailableBodyHeightForTrainPage(firstPageOfTrain);
			int n = (int)Math.Floor(body / MinRowH);
			if (n < 1)
			{
				n = 1;
			}

			return n;
		}

		/// <summary>
		/// Una página de libro sola (compat). Genera una hoja apaisada con la mitad
		/// izquierda ocupada y la derecha en blanco.
		/// </summary>
		public static string RenderPage(CirculationSheetDocument document, CirculationSheetPage page)
		{
			return RenderSheet(document, page, right: null);
		}

		/// <summary>
		/// Renderiza todas las hojas físicas A4 apaisadas de una ficha de un solo tren
		/// (2 mitades por hoja).
		/// </summary>
		public static IReadOnlyList<string> RenderAllPages(CirculationSheetDocument document)
		{
			return RenderAllPages(document, out _, out _);
		}

		/// <summary>
		/// Igual que <see cref="RenderAllPages(CirculationSheetDocument)"/> y devuelve
		/// el sello/payload de documento (el mismo en todas las hojas).
		/// </summary>
		public static IReadOnlyList<string> RenderAllPages(
			CirculationSheetDocument document,
			out string documentSeal,
			out string documentPayload)
		{
			return RenderAllPages(document, DefaultPalette, out documentSeal, out documentPayload);
		}

		/// <summary>
		/// Igual que <see cref="RenderAllPages(CirculationSheetDocument)"/> con paleta (cabina día/noche).
		/// </summary>
		public static IReadOnlyList<string> RenderAllPages(
			CirculationSheetDocument document,
			CirculationSheetPalette palette)
		{
			return RenderAllPages(document, palette, out _, out _);
		}

		public static IReadOnlyList<string> RenderAllPages(
			CirculationSheetDocument document,
			CirculationSheetPalette palette,
			out string documentSeal,
			out string documentPayload)
		{
			if (document is null)
			{
				throw new ArgumentNullException(nameof(document));
			}

			IReadOnlyList<CirculationSheetPage> book = document.Pages;
			int sheetCount = CirculationSheetPager.ComputeSheetCount(Math.Max(1, book.Count));
			documentPayload = CirculationSheetAuthenticity.BuildDocumentPayload(
				"ficha",
				document.TrainNumber,
				document.EditionLabel,
				document.ServiceDaysLabel,
				sheetCount,
				document.Relation);
			documentSeal = CirculationSheetAuthenticity.ComputeSealCode(documentPayload);

			List<string> sheets = new List<string>((book.Count + 1) / 2);
			int i = 0;
			while (i < book.Count)
			{
				CirculationSheetPage left = book[i];
				CirculationSheetPage? right = i + 1 < book.Count ? book[i + 1] : null;
				sheets.Add(RenderSheet(document, left, right, documentSeal, documentPayload, palette));
				i += 2;
			}

			if (sheets.Count == 0)
			{
				sheets.Add(RenderSheet(
					document,
					new CirculationSheetPage(0, 1, Array.Empty<CirculationSheetFrontier>()),
					null,
					documentSeal,
					documentPayload,
					palette));
			}

			return sheets;
		}

		/// <summary>
		/// Renderiza el libro itinerario completo (portada + índice + trenes)
		/// empaquetando semipáginas de 2 en 2 en hojas A4 apaisadas.
		/// </summary>
		public static IReadOnlyList<string> RenderAllBookSheets(ItineraryBookDocument book)
		{
			return RenderAllBookSheets(book, out _, out _);
		}

		/// <summary>
		/// Igual que <see cref="RenderAllBookSheets(ItineraryBookDocument)"/> con sello de documento.
		/// </summary>
		public static IReadOnlyList<string> RenderAllBookSheets(
			ItineraryBookDocument book,
			out string documentSeal,
			out string documentPayload)
		{
			if (book is null)
			{
				throw new ArgumentNullException(nameof(book));
			}

			IReadOnlyList<ItineraryBookHalfPage> halves = book.HalfPages;
			int sheetCount = CirculationSheetPager.ComputeSheetCount(Math.Max(1, halves.Count));
			documentPayload = CirculationSheetAuthenticity.BuildDocumentPayload(
				"libro",
				book.PlanName,
				book.EditionLabel,
				book.DayLabel,
				sheetCount,
				book.Notes);
			documentSeal = CirculationSheetAuthenticity.ComputeSealCode(documentPayload);

			List<string> sheets = new List<string>((halves.Count + 1) / 2);
			int i = 0;
			while (i < halves.Count)
			{
				ItineraryBookHalfPage left = halves[i];
				ItineraryBookHalfPage? right = i + 1 < halves.Count ? halves[i + 1] : null;
				sheets.Add(RenderBookSheet(left, right, documentSeal, documentPayload, DefaultPalette));
				i += 2;
			}

			if (sheets.Count == 0)
			{
				ItineraryBookHalfPage cover = ItineraryBookHalfPage.Cover(
					1, 1, book.PlanName, book.Notes, book.DayLabel, book.EditionLabel, 0, 0);
				sheets.Add(RenderBookSheet(cover, null, documentSeal, documentPayload, DefaultPalette));
			}

			return sheets;
		}

		/// <summary>
		/// Hoja física A4 apaisada: mitad izquierda + mitad derecha (opcional).
		/// </summary>
		public static string RenderSheet(
			CirculationSheetDocument document,
			CirculationSheetPage left,
			CirculationSheetPage? right)
		{
			string payload = CirculationSheetAuthenticity.BuildDocumentPayload(
				"ficha",
				document.TrainNumber,
				document.EditionLabel,
				document.ServiceDaysLabel,
				Math.Max(1, document.Pages.Count),
				document.Relation);
			string seal = CirculationSheetAuthenticity.ComputeSealCode(payload);
			return RenderSheet(document, left, right, seal, payload, DefaultPalette);
		}

		public static string RenderSheet(
			CirculationSheetDocument document,
			CirculationSheetPage left,
			CirculationSheetPage? right,
			string documentSeal,
			string documentPayload)
		{
			return RenderSheet(document, left, right, documentSeal, documentPayload, DefaultPalette);
		}

		public static string RenderSheet(
			CirculationSheetDocument document,
			CirculationSheetPage left,
			CirculationSheetPage? right,
			string documentSeal,
			string documentPayload,
			CirculationSheetPalette palette)
		{
			if (document is null)
			{
				throw new ArgumentNullException(nameof(document));
			}

			if (left is null)
			{
				throw new ArgumentNullException(nameof(left));
			}

			StringBuilder sb = BeginSheetSvg(palette);
			double panelW = PageWidth;
			double panelH = PageHeight;
			double y0 = SheetOuterMargin;
			double leftX = SheetOuterMargin;
			double rightX = SheetOuterMargin + panelW + PanelGutter;

			// Mismo sello de documento en todas las mitades (verificable al copiar SEL).
			DrawTrainTablePanel(
				sb, document, left, leftX, y0, panelW, panelH, null, null, documentSeal, documentPayload, palette);

			if (right is not null)
			{
				DrawTrainTablePanel(
					sb, document, right, rightX, y0, panelW, panelH, null, null, documentSeal, documentPayload, palette);
			}

			DrawHalfSeparator(sb, leftX, y0, panelW, panelH, palette);
			sb.Append("</svg>");
			return sb.ToString();
		}

		/// <summary>Hoja A4 apaisada a partir de dos semipáginas del libro completo.</summary>
		public static string RenderBookSheet(ItineraryBookHalfPage left, ItineraryBookHalfPage? right)
		{
			string plan = left is null ? string.Empty : left.PlanName;
			string edition = left is null ? string.Empty : left.EditionLabel;
			string day = left is null ? string.Empty : left.DayLabel;
			string payload = CirculationSheetAuthenticity.BuildDocumentPayload(
				"libro", plan, edition, day, 1, null);
			string seal = CirculationSheetAuthenticity.ComputeSealCode(payload);
			return RenderBookSheet(left!, right, seal, payload, DefaultPalette);
		}

		public static string RenderBookSheet(
			ItineraryBookHalfPage left,
			ItineraryBookHalfPage? right,
			string documentSeal,
			string documentPayload)
		{
			return RenderBookSheet(left, right, documentSeal, documentPayload, DefaultPalette);
		}

		public static string RenderBookSheet(
			ItineraryBookHalfPage left,
			ItineraryBookHalfPage? right,
			string documentSeal,
			string documentPayload,
			CirculationSheetPalette palette)
		{
			if (left is null)
			{
				throw new ArgumentNullException(nameof(left));
			}

			StringBuilder sb = BeginSheetSvg(palette);
			double panelW = PageWidth;
			double panelH = PageHeight;
			double y0 = SheetOuterMargin;
			double leftX = SheetOuterMargin;
			double rightX = SheetOuterMargin + panelW + PanelGutter;

			DrawHalfPage(sb, left, leftX, y0, panelW, panelH, documentSeal, documentPayload, palette);
			if (right is not null)
			{
				DrawHalfPage(sb, right, rightX, y0, panelW, panelH, documentSeal, documentPayload, palette);
			}

			DrawHalfSeparator(sb, leftX, y0, panelW, panelH, palette);
			sb.Append("</svg>");
			return sb.ToString();
		}

		private static StringBuilder BeginSheetSvg(CirculationSheetPalette palette)
		{
			StringBuilder sb = new StringBuilder(48 * 1024);
			sb.Append(CultureInfo.InvariantCulture,
				$"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(SheetWidth)}\" height=\"{F(SheetHeight)}\" viewBox=\"0 0 {F(SheetWidth)} {F(SheetHeight)}\" preserveAspectRatio=\"xMidYMid meet\" class=\"diamond-circ-sheet-svg\">");
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"0\" y=\"0\" width=\"{F(SheetWidth)}\" height=\"{F(SheetHeight)}\" fill=\"{palette.Background}\"/>");
			return sb;
		}

		/// <summary>
		/// QR a la derecha, justo bajo «Tipo …» (solo 1.ª hoja de la marcha).
		/// Contenido = solo sello (<c>ZAFSEL:v1:{seal}</c>); ~20 % más pequeño que la franja anterior.
		/// </summary>
		private static void DrawHeaderQr(
			StringBuilder sb,
			double tableRight,
			double yBelowHeader,
			string sealCode,
			string documentPayload,
			CirculationSheetPalette palette)
		{
			if (string.IsNullOrEmpty(sealCode))
			{
				return;
			}

			try
			{
				// Solo sello: el payload canónico no va en el QR (más legible).
				string qrText = CirculationSheetQr.BuildQrPayload(sealCode);
				const double qrPad = 2.5;
				double qx = tableRight - HeaderQrSize - qrPad;
				double qy = yBelowHeader + (HeaderQrBandH - HeaderQrSize) * 0.5;
				if (qy < yBelowHeader + 0.5)
				{
					qy = yBelowHeader + 0.5;
				}

				CirculationSheetQr.AppendQrSvg(
					sb, qx, qy, HeaderQrSize, qrText, palette.QrModule, palette.QrPaper, palette.Stroke);
			}
			catch
			{
				// QR opcional: no tumbar el dibujo si falla el generador.
			}
		}

		/// <summary>Ancho máximo de texto Loc./ruta dejando hueco al QR bajo Tipo.</summary>
		private static int SubHeaderTextMaxChars(double tableW)
		{
			double qrReserve = HeaderQrSize + 12.0;
			int n = (int)((tableW - qrReserve) / 3.9);
			if (n < 24)
			{
				n = 24;
			}

			if (n > 48)
			{
				n = 48;
			}

			return n;
		}

		private static void DrawHalfSeparator(
			StringBuilder sb,
			double leftX,
			double y0,
			double panelW,
			double panelH,
			CirculationSheetPalette palette)
		{
			double midX = leftX + panelW + PanelGutter * 0.5;
			sb.Append(CultureInfo.InvariantCulture,
				$"<line x1=\"{F(midX)}\" y1=\"{F(y0 + 4)}\" x2=\"{F(midX)}\" y2=\"{F(y0 + panelH - 4)}\" stroke=\"{palette.MutedStroke}\" stroke-width=\"0.6\" stroke-dasharray=\"3 2\"/>");
		}

		private static void DrawHalfPage(
			StringBuilder sb,
			ItineraryBookHalfPage half,
			double panelX,
			double panelY,
			double panelW,
			double panelH,
			string sealCode,
			string documentPayload,
			CirculationSheetPalette palette)
		{
			if (half.Kind == ItineraryBookHalfKind.Cover)
			{
				DrawCoverPanel(sb, half, panelX, panelY, panelW, panelH, sealCode, palette);
				return;
			}

			if (half.Kind == ItineraryBookHalfKind.Index)
			{
				DrawIndexPanel(sb, half, panelX, panelY, panelW, panelH, sealCode, palette);
				return;
			}

			if (half.TrainDocument is not null && half.TrainPage is not null)
			{
				DrawTrainTablePanel(
					sb,
					half.TrainDocument,
					half.TrainPage,
					panelX,
					panelY,
					panelW,
					panelH,
					half.PageNumber,
					half.PageCount,
					sealCode,
					documentPayload,
					palette);
			}
		}

		/// <summary>Portada del libro (una semipágina).</summary>
		private static void DrawCoverPanel(
			StringBuilder sb,
			ItineraryBookHalfPage half,
			double panelX,
			double panelY,
			double panelW,
			double panelH,
			string sealCode,
			CirculationSheetPalette palette)
		{
			double contentLeft = panelX + PanelPadL;
			double tableW = Math.Min(TableWidth, panelW - PanelPadL - PanelPadR);
			double tableRight = contentLeft + tableW;
			double y = panelY + PanelPadT;

			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{F(contentLeft)}\" y=\"{F(y)}\" width=\"{F(tableW)}\" height=\"{F(HeaderBandH)}\" fill=\"{palette.HeaderFill}\"/>");
			sb.Append(Text(contentLeft + 5, y + HeaderBandH * 0.72, "LIBRO ITINERARIO", 10, "700", palette.HeaderText, "start"));
			sb.Append(Text(tableRight - 5, y + HeaderBandH * 0.72, "Montefaro", 9, "600", palette.HeaderText, "end"));
			y += HeaderBandH + 18;

			sb.Append(Text(contentLeft + 6, y, Truncate(half.PlanName, 48), 16, "700", palette.Text, "start"));
			y += 28;

			if (!string.IsNullOrEmpty(half.DayLabel))
			{
				sb.Append(Text(contentLeft + 6, y, "Día: " + half.DayLabel, 11, "600", palette.Text, "start"));
				y += 18;
			}

			sb.Append(Text(contentLeft + 6, y, "Trenes: " + half.TrainCount.ToString(CultureInfo.InvariantCulture)
				+ "  ·  Grupos / recorridos: " + half.GroupCount.ToString(CultureInfo.InvariantCulture), 10, "500", palette.Text, "start"));
			y += 18;

			sb.Append(Text(contentLeft + 6, y, "Semipáginas: " + half.PageCount.ToString(CultureInfo.InvariantCulture)
				+ "  ·  Hojas A4: " + CirculationSheetPager.ComputeSheetCount(half.PageCount)
					.ToString(CultureInfo.InvariantCulture), 10, "500", palette.Text, "start"));
			y += 22;

			if (!string.IsNullOrEmpty(half.Notes))
			{
				sb.Append(Text(contentLeft + 6, y, "Notas", 9, "700", palette.Text, "start"));
				y += 14;
				// Notas multilínea simple.
				string notes = half.Notes;
				int maxChars = 58;
				int pos = 0;
				int lines = 0;
				while (pos < notes.Length && lines < 8)
				{
					int take = Math.Min(maxChars, notes.Length - pos);
					// Cortar en espacio si se puede.
					if (pos + take < notes.Length)
					{
						int sp = notes.LastIndexOf(' ', pos + take - 1, take);
						if (sp > pos)
						{
							take = sp - pos;
						}
					}

					if (take < 1)
					{
						take = 1;
					}

					string line = notes.Substring(pos, take).Trim();
					sb.Append(Text(contentLeft + 6, y, line, 9, "400", palette.Text, "start"));
					y += 13;
					pos += take;
					while (pos < notes.Length && notes[pos] == ' ')
					{
						pos++;
					}

					lines++;
				}
			}

			DrawPanelFooter(sb, contentLeft, tableRight, panelY, panelH, half.EditionLabel, half.PageNumber, half.PageCount, sealCode, palette);
		}

		/// <summary>Índice de trenes (una semipágina).</summary>
		private static void DrawIndexPanel(
			StringBuilder sb,
			ItineraryBookHalfPage half,
			double panelX,
			double panelY,
			double panelW,
			double panelH,
			string sealCode,
			CirculationSheetPalette palette)
		{
			double contentLeft = panelX + PanelPadL;
			double tableW = Math.Min(TableWidth, panelW - PanelPadL - PanelPadR);
			double tableRight = contentLeft + tableW;
			double y = panelY + PanelPadT;

			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{F(contentLeft)}\" y=\"{F(y)}\" width=\"{F(tableW)}\" height=\"{F(HeaderBandH)}\" fill=\"{palette.HeaderFill}\"/>");
			string title = half.IndexParts > 1
				? "ÍNDICE · " + half.IndexPart.ToString(CultureInfo.InvariantCulture)
					+ "/" + half.IndexParts.ToString(CultureInfo.InvariantCulture)
				: "ÍNDICE";
			sb.Append(Text(contentLeft + 5, y + HeaderBandH * 0.72, title, 10, "700", palette.HeaderText, "start"));
			y += HeaderBandH + 6;

			// Cabecera columnas índice (blanco sobre gris oscuro)
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{F(contentLeft)}\" y=\"{F(y)}\" width=\"{F(tableW)}\" height=\"{F(ColHeaderH)}\" fill=\"{palette.HeaderFill}\"/>");
			sb.Append(Text(contentLeft + 6, y + ColHeaderH * 0.72, "Tren", 7, "700", palette.HeaderText, "start"));
			sb.Append(Text(contentLeft + 58, y + ColHeaderH * 0.72, "Salida", 7, "700", palette.HeaderText, "start"));
			sb.Append(Text(contentLeft + 100, y + ColHeaderH * 0.72, "Relación", 7, "700", palette.HeaderText, "start"));
			sb.Append(Text(tableRight - 6, y + ColHeaderH * 0.72, "Pág", 7, "700", palette.HeaderText, "end"));
			y += ColHeaderH + 4;

			double footerY = panelY + panelH - 4;
			double bodyBottom = panelY + panelH - PanelPadB;
			int n = Math.Max(1, half.IndexLines.Count);
			double rowH = (bodyBottom - y) / n;
			if (rowH < 11)
			{
				rowH = 11;
			}

			if (rowH > 16)
			{
				rowH = 16;
			}

			int i = 0;
			while (i < half.IndexLines.Count)
			{
				ItineraryIndexEntry line = half.IndexLines[i];
				double cy = y + rowH * 0.5 + 2.5;
				if (line.IsGroupHeader)
				{
					sb.Append(CultureInfo.InvariantCulture,
						$"<rect x=\"{F(contentLeft)}\" y=\"{F(y)}\" width=\"{F(tableW)}\" height=\"{F(rowH)}\" fill=\"{palette.IndexGroupFill}\"/>");
					sb.Append(Text(contentLeft + 6, cy, Truncate(line.GroupTitle, 52), 7.5, "700", palette.Text, "start"));
				}
				else
				{
					sb.Append(Text(contentLeft + 6, cy, Truncate(line.TrainNumber, 10), 8, "700", palette.Text, "start"));
					string dep = CirculationSheetDocument.FormatSheetTime(line.Departure);
					sb.Append(Text(contentLeft + 58, cy, dep, 7.5, "500", palette.Text, "start"));
					sb.Append(Text(contentLeft + 100, cy, Truncate(line.Relation, 36), 7.5, "400", palette.Text, "start"));
					sb.Append(Text(tableRight - 6, cy, line.PageStart.ToString(CultureInfo.InvariantCulture), 8, "600", palette.Text, "end"));
				}

				// Línea sutil
				sb.Append(CultureInfo.InvariantCulture,
					$"<line x1=\"{F(contentLeft)}\" y1=\"{F(y + rowH)}\" x2=\"{F(tableRight)}\" y2=\"{F(y + rowH)}\" stroke=\"{palette.MutedStroke}\" stroke-width=\"0.35\"/>");
				y += rowH;
				i++;
			}

			DrawPanelFooter(sb, contentLeft, tableRight, panelY, panelH, "Índice", half.PageNumber, half.PageCount, sealCode, palette);
		}

		private static void DrawPanelFooter(
			StringBuilder sb,
			double contentLeft,
			double tableRight,
			double panelY,
			double panelH,
			string editionLabel,
			int pageNumber,
			int pageCount,
			string? sealCode,
			CirculationSheetPalette palette)
		{
			double footerY = panelY + panelH - 4;
			double triX = contentLeft + 4;
			sb.Append(CultureInfo.InvariantCulture,
				$"<polygon points=\"{F(triX)},{F(footerY)} {F(triX + 8)},{F(footerY)} {F(triX + 4)},{F(footerY - 8)}\" fill=\"{palette.Stroke}\"/>");
			string leftLabel = Truncate(editionLabel ?? string.Empty, 28);
			if (!string.IsNullOrEmpty(sealCode))
			{
				string seal = CirculationSheetAuthenticity.FormatSealLabel(sealCode);
				leftLabel = string.IsNullOrEmpty(leftLabel) ? seal : leftLabel + " · " + seal;
			}

			sb.Append(Text(contentLeft + 18, footerY - 1, Truncate(leftLabel, 48), 6.5, "400", palette.Text, "start"));
			string pageLabel = "Pág " + pageNumber.ToString(CultureInfo.InvariantCulture)
				+ " de " + pageCount.ToString(CultureInfo.InvariantCulture);
			sb.Append(Text(tableRight, footerY - 1, pageLabel, 7.5, "600", palette.Text, "end"));
		}

		/// <summary>
		/// Dibuja una tabla de marcha (mitad) en el rectángulo [panelX, panelY, panelW, panelH].
		/// Cabecera y pie a cotas fijas; cuerpo de filas estirado a todo el alto útil.
		/// Si <paramref name="bookPageNumber"/> se indica, el pie usa la paginación del libro.
		/// </summary>
		private static void DrawTrainTablePanel(
			StringBuilder sb,
			CirculationSheetDocument document,
			CirculationSheetPage page,
			double panelX,
			double panelY,
			double panelW,
			double panelH,
			int? bookPageNumber,
			int? bookPageCount,
			string? sealCode,
			string? documentPayload,
			CirculationSheetPalette palette)
		{
			IReadOnlyList<CirculationSheetFrontier> rows = page.Frontiers;

			double contentLeft = panelX + PanelPadL;
			double tableW = TableWidth;
			// Si la tabla no cabe, se alinea a la izquierda del pad; el resto es margen derecho.
			if (tableW > panelW - PanelPadL - PanelPadR)
			{
				tableW = panelW - PanelPadL - PanelPadR;
			}

			double tableRight = contentLeft + tableW;
			double y = panelY + PanelPadT;

			// 1.ª hoja: Loc. (material) + ruta debajo; Tipo (vmax) en todas las hojas.
			// Continuación: solo número de circulación (+ días) y Tipo; más alto de cuerpo.
			bool firstPageOfTrain = page.PageIndex == 0;

			// —— Cabecera general (gris oscuro, blanco) ——
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{F(contentLeft)}\" y=\"{F(y)}\" width=\"{F(tableW)}\" height=\"{F(HeaderBandH)}\" fill=\"{palette.HeaderFill}\"/>");
			// Número de servicio (plantilla) · días en todas las hojas.
			string titleLeft = string.IsNullOrEmpty(document.TrainNumber)
				? "—"
				: document.TrainNumber + ".-";
			if (!string.IsNullOrEmpty(document.ServiceDaysLabel))
			{
				titleLeft = titleLeft + "  " + document.ServiceDaysLabel;
			}

			// 1.ª hoja: Tipo centrado sobre el QR. Continuación: Tipo al borde derecho.
			const double qrPad = 2.5;
			sb.Append(Text(contentLeft + 5, y + HeaderBandH * 0.72, Truncate(titleLeft, firstPageOfTrain ? 42 : 48), 10, "700", palette.HeaderText, "start"));
			if (!string.IsNullOrEmpty(document.MaterialType))
			{
				if (firstPageOfTrain)
				{
					double tipoCx = tableRight - qrPad - HeaderQrSize * 0.5;
					sb.Append(Text(tipoCx, y + HeaderBandH * 0.72, document.MaterialType, 9, "600", palette.HeaderText, "middle"));
				}
				else
				{
					sb.Append(Text(tableRight - 5, y + HeaderBandH * 0.72, document.MaterialType, 9, "600", palette.HeaderText, "end"));
				}
			}

			y += HeaderBandH;

			// —— Solo 1.ª hoja: Loc./ruta (izq.) + QR bajo Tipo (dcha.). Continuación: sin franja. ——
			if (firstPageOfTrain)
			{
				double yBelowHeader = y;
				sb.Append(CultureInfo.InvariantCulture,
					$"<rect x=\"{F(contentLeft)}\" y=\"{F(y)}\" width=\"{F(tableW)}\" height=\"{F(HeaderQrBandH)}\" fill=\"{palette.SubHeaderFill}\" stroke=\"{palette.Stroke}\" stroke-width=\"0.5\"/>");
				int maxChars = SubHeaderTextMaxChars(tableW);
				sb.Append(Text(
					contentLeft + 5,
					y + SubHeaderLineH * 0.85,
					Truncate(document.LocationLine, maxChars),
					7,
					"600",
					palette.Text,
					"start"));
				sb.Append(Text(
					contentLeft + 5,
					y + SubHeaderLineH * 1.85,
					Truncate(document.RouteLine, maxChars),
					7,
					"400",
					palette.Text,
					"start"));

				DrawHeaderQr(
					sb,
					tableRight,
					yBelowHeader,
					sealCode ?? string.Empty,
					documentPayload ?? string.Empty,
					palette);
				y += HeaderQrBandH;
			}

			// —— Cabecera columnas ——
			double colHeaderY = y;
			DrawColumnHeaders(sb, contentLeft, colHeaderY, ColHeaderH, tableW, palette);
			y += ColHeaderH;
			double bodyTop = y;

			// Pie a cota fija respecto al panel → mismo Y en ambas mitades.
			// Sin subcabecera, el cuerpo se estira y ocupa el espacio liberado.
			double bodyBottom = panelY + panelH - PanelPadB;
			double availBody = bodyBottom - bodyTop;
			if (availBody < MinRowH)
			{
				availBody = MinRowH;
			}

			int n = Math.Max(1, rows.Count);
			// Estirar filas a todo el alto útil (reparto uniforme).
			double rowH = availBody / n;
			if (rowH < MinRowH && n * MinRowH > availBody)
			{
				// Demasiadas filas: comprimir por debajo de MinRowH para no desbordar.
				rowH = availBody / n;
			}

			double bodyH = rowH * Math.Max(rows.Count, 1);

			// Fondo columna Max (limitaciones de velocidad)
			double vmxX = contentLeft + ColVia + ColStKm;
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{F(vmxX)}\" y=\"{F(bodyTop)}\" width=\"{F(ColVmx)}\" height=\"{F(bodyH)}\" fill=\"{palette.VmxFill}\"/>");

			// Marco tabla: mismo color/grosor que las verticales internas de columna.
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{F(contentLeft)}\" y=\"{F(colHeaderY)}\" width=\"{F(tableW)}\" height=\"{F(ColHeaderH + bodyH)}\" fill=\"none\" stroke=\"{palette.Stroke}\" stroke-width=\"0.5\"/>");

			if (rows.Count == 0)
			{
				sb.Append(Text(contentLeft + tableW * 0.5, bodyTop + 16, "(sin fronteras)", 9, "400", palette.Text, "middle"));
			}
			else
			{
				DrawBody(sb, contentLeft, bodyTop, rowH, rows, palette);
			}

			DrawVerticals(sb, contentLeft, colHeaderY, ColHeaderH + bodyH, palette);

			// Pie (paginación del tren o del libro completo + sello de autenticidad)
			int pNum = bookPageNumber ?? page.PageNumber;
			int pCnt = bookPageCount ?? page.PageCount;
			DrawPanelFooter(sb, contentLeft, tableRight, panelY, panelH, document.EditionLabel, pNum, pCnt, sealCode, palette);
		}

		private static void DrawColumnHeaders(
			StringBuilder sb,
			double left,
			double y,
			double h,
			double tableW,
			CirculationSheetPalette palette)
		{
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{F(left)}\" y=\"{F(y)}\" width=\"{F(tableW)}\" height=\"{F(h)}\" fill=\"{palette.HeaderFill}\"/>");
			double x = left;
			HeaderCell(sb, x, y, h, ColVia, "Vía", palette);
			x += ColVia;
			HeaderCell(sb, x, y, h, ColStKm, "PK", palette);
			x += ColStKm;
			HeaderCell(sb, x, y, h, ColVmx, "Max", palette);
			x += ColVmx;
			HeaderCell(sb, x, y, h, ColDep, "Dependencia", palette);
			x += ColDep;
			HeaderCell(sb, x, y, h, ColCom, "Com", palette);
			x += ColCom;
			HeaderCell(sb, x, y, h, ColHora, "Hora", palette);
			x += ColHora;
			HeaderCell(sb, x, y, h, ColConc, "Conc.", palette);
			x += ColConc;
			HeaderCell(sb, x, y, h, ColCruz, "Obs.", palette);
		}

		private static void HeaderCell(
			StringBuilder sb,
			double x,
			double y,
			double h,
			double w,
			string label,
			CirculationSheetPalette palette)
		{
			sb.Append(CultureInfo.InvariantCulture,
				$"<line x1=\"{F(x)}\" y1=\"{F(y)}\" x2=\"{F(x)}\" y2=\"{F(y + h)}\" stroke=\"{palette.Stroke}\" stroke-width=\"0.5\"/>");
			sb.Append(Text(x + w * 0.5, y + h * 0.7, label, 6.5, "700", palette.HeaderText, "middle"));
		}

		private static void DrawVerticals(
			StringBuilder sb,
			double left,
			double y,
			double h,
			CirculationSheetPalette palette)
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
				// Exterior e interior con el mismo trazo (antes el borde era más grueso).
				sb.Append(CultureInfo.InvariantCulture,
					$"<line x1=\"{F(xs[i])}\" y1=\"{F(y)}\" x2=\"{F(xs[i])}\" y2=\"{F(y + h)}\" stroke=\"{palette.Stroke}\" stroke-width=\"0.5\"/>");
				i++;
			}
		}

		private static void DrawBody(
			StringBuilder sb,
			double tableLeft,
			double bodyTop,
			double rowH,
			IReadOnlyList<CirculationSheetFrontier> rows,
			CirculationSheetPalette palette)
		{
			int n = rows.Count;

			DrawMergedViaColumn(sb, tableLeft, bodyTop, rowH, rows, palette);

			// Formato Renfe: sin divisiones horizontales por fila en Dependencia /
			// Com / Hora / Conc. / Obs. Separadores en: Vía (tipo), Max (V), y PK
			// (cambio de eje, p. ej. T3→T2 entre Enllaç y Desvío).
			// Para no perder la fila, cada dependencia lleva línea de puntos hasta Com.
			int i = 0;
			while (i < n)
			{
				CirculationSheetFrontier row = rows[i];
				double y0 = bodyTop + i * rowH;
				double cy = y0 + rowH * 0.5;

				double stX = tableLeft + ColVia;
				sb.Append(Text(stX + ColStKm - 3, cy + 2.5, row.StationKm, 7.5, "600", palette.Text, "end"));

				double depX = tableLeft + ColVia + ColStKm + ColVmx;
				DrawDependency(sb, depX, y0, rowH, row, palette);

				double comX = depX + ColDep;
				DrawCom(sb, comX, cy, row, palette);

				double horaX = comX + ColCom;
				DrawHora(sb, horaX, cy, row, palette);

				double cruzX = horaX + ColHora + ColConc;
				if (!string.IsNullOrEmpty(row.CrossingTrains))
				{
					sb.Append(Text(cruzX + ColCruz * 0.5, cy + 2.5, Truncate(row.CrossingTrains, 10), 6.5, "600", palette.Text, "middle"));
				}

				i++;
			}

			DrawPkAxisTransitions(sb, tableLeft, bodyTop, rowH, rows, palette);

			DrawOffsetVmx(sb, tableLeft + ColVia + ColStKm, bodyTop, rowH, rows, palette);

			double concX = tableLeft + ColVia + ColStKm + ColVmx + ColDep + ColCom + ColHora;
			DrawOffsetGranted(sb, concX, bodyTop, rowH, rows, palette);

			sb.Append(CultureInfo.InvariantCulture,
				$"<line x1=\"{F(tableLeft)}\" y1=\"{F(bodyTop)}\" x2=\"{F(tableLeft + TableWidth)}\" y2=\"{F(bodyTop)}\" stroke=\"{palette.Stroke}\" stroke-width=\"0.5\"/>");
			sb.Append(CultureInfo.InvariantCulture,
				$"<line x1=\"{F(tableLeft)}\" y1=\"{F(bodyTop + n * rowH)}\" x2=\"{F(tableLeft + TableWidth)}\" y2=\"{F(bodyTop + n * rowH)}\" stroke=\"{palette.Stroke}\" stroke-width=\"0.5\"/>");
		}

		/// <summary>
		/// Divisiones horizontales en la columna PK solo en transiciones de eje
		/// (p. ej. T3 → T2 entre la última dependencia de un eje y la primera del siguiente).
		/// </summary>
		private static void DrawPkAxisTransitions(
			StringBuilder sb,
			double tableLeft,
			double bodyTop,
			double rowH,
			IReadOnlyList<CirculationSheetFrontier> rows,
			CirculationSheetPalette palette)
		{
			int n = rows.Count;
			if (n < 2)
			{
				return;
			}

			double x0 = tableLeft + ColVia;
			double x1 = x0 + ColStKm;
			int k = 1;
			while (k < n)
			{
				if (!SameAxisId(rows[k - 1].AxisId, rows[k].AxisId))
				{
					// Límite entre filas: debajo de la última del eje anterior /
					// encima de la primera del nuevo.
					double y = bodyTop + k * rowH;
					sb.Append(CultureInfo.InvariantCulture,
						$"<line x1=\"{F(x0)}\" y1=\"{F(y)}\" x2=\"{F(x1)}\" y2=\"{F(y)}\" stroke=\"{palette.Stroke}\" stroke-width=\"0.45\"/>");
				}

				k++;
			}
		}

		private static bool SameAxisId(string a, string b)
		{
			if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b))
			{
				return true;
			}

			return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
		}

		private static void DrawMergedViaColumn(
			StringBuilder sb,
			double tableLeft,
			double bodyTop,
			double rowH,
			IReadOnlyList<CirculationSheetFrontier> rows,
			CirculationSheetPalette palette)
		{
			int n = rows.Count;
			if (n < 2)
			{
				return;
			}

			int i = 0;
			while (i < n - 1)
			{
				bool isDouble = rows[i].OutgoingIsDoubleTrack;
				int j = i + 1;
				while (j < n - 1 && rows[j].OutgoingIsDoubleTrack == isDouble)
				{
					j++;
				}

				double y0 = bodyTop + i * rowH;
				double y1 = bodyTop + (j + 1) * rowH;
				string label = isDouble ? "Doble" : "Única";
				double cx = tableLeft + ColVia * 0.5;
				double cy = (y0 + y1) * 0.5;
				sb.Append(CultureInfo.InvariantCulture,
					$"<text x=\"{F(cx)}\" y=\"{F(cy)}\" fill=\"{palette.Text}\" font-size=\"7.5\" font-weight=\"700\" font-family=\"Arial,Helvetica,sans-serif\" text-anchor=\"middle\" transform=\"rotate(-90 {F(cx)},{F(cy)})\">{XmlEscape(label)}</text>");

				if (j < n - 1)
				{
					double sepY = bodyTop + (j + 0.5) * rowH;
					sb.Append(CultureInfo.InvariantCulture,
						$"<line x1=\"{F(tableLeft)}\" y1=\"{F(sepY)}\" x2=\"{F(tableLeft + ColVia)}\" y2=\"{F(sepY)}\" stroke=\"{palette.Stroke}\" stroke-width=\"0.45\"/>");
				}

				i = j;
			}
		}

		private static void DrawOffsetVmx(
			StringBuilder sb,
			double colX,
			double bodyTop,
			double rowH,
			IReadOnlyList<CirculationSheetFrontier> rows,
			CirculationSheetPalette palette)
		{
			int n = rows.Count;
			if (n < 2)
			{
				return;
			}

			int i = 0;
			while (i < n - 1)
			{
				int? vmax = rows[i].OutgoingVmaxKmh;
				int j = i + 1;
				while (j < n - 1 && SameVmax(rows[j].OutgoingVmaxKmh, vmax))
				{
					j++;
				}

				double yStart = bodyTop + (i + 0.5) * rowH;
				double yEnd = bodyTop + (j + 0.5) * rowH;
				double cy = (yStart + yEnd) * 0.5;
				if (vmax.HasValue)
				{
					sb.Append(Text(colX + ColVmx * 0.5, cy + 2.5,
						vmax.Value.ToString(CultureInfo.InvariantCulture), 8.5, "700", palette.Text, "middle"));
				}

				i = j;
			}

			int k = 1;
			while (k < n - 1)
			{
				int? prev = rows[k - 1].OutgoingVmaxKmh;
				int? next = rows[k].OutgoingVmaxKmh;
				if (!SameVmax(prev, next))
				{
					double lineY = bodyTop + (k + 0.5) * rowH;
					sb.Append(CultureInfo.InvariantCulture,
						$"<line x1=\"{F(colX)}\" y1=\"{F(lineY)}\" x2=\"{F(colX + ColVmx)}\" y2=\"{F(lineY)}\" stroke=\"{palette.Stroke}\" stroke-width=\"0.45\"/>");
				}

				k++;
			}
		}

		private static void DrawOffsetGranted(
			StringBuilder sb,
			double colX,
			double bodyTop,
			double rowH,
			IReadOnlyList<CirculationSheetFrontier> rows,
			CirculationSheetPalette palette)
		{
			int n = rows.Count;
			// Solo valores desfasados entre filas; sin divisiones horizontales (formato Renfe).
			int i = 0;
			while (i < n - 1)
			{
				double y0 = bodyTop + (i + 0.5) * rowH;
				double y1 = bodyTop + (i + 1.5) * rowH;
				double cy = (y0 + y1) * 0.5;
				string g = CirculationSheetDocument.FormatGrantedMinutes(rows[i].GrantedToNext);
				if (g.Length > 0)
				{
					sb.Append(Text(colX + ColConc * 0.5, cy + 2.5, g, 7.5, "600", palette.Text, "middle"));
				}

				i++;
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
			CirculationSheetFrontier row,
			CirculationSheetPalette palette)
		{
			double midY = y0 + rowH * 0.5;
			double textY = midY + 2.5;
			string name = Truncate(row.DependencyName, 34);
			const double fontSize = 7.5;
			// Borde izquierdo de la columna Com (parada comercial).
			double comLeft = x + ColDep;
			double nameEndX = x + 4.0;

			// Blanco sobre gris de cabecera si el tren para aquí (origen, destino o parada
			// comercial), no por ser estación principal de la red (p. ej. Son Rullán sin
			// parada no se resalta; Lloseta apeadero con parada sí).
			bool trainStopsHere = (row.IsOrigin || row.IsDestination || row.IsCommercialStop)
				&& row.MarkKind != CirculationSheetMarkKind.SpeedLimitChange;

			if (string.IsNullOrEmpty(name))
			{
				return;
			}

			if (trainStopsHere)
			{
				// Blanco sobre el mismo gris oscuro de los encabezados.
				double textW = EstimateTextWidth(name, fontSize, bold: true);
				double padX = 3.5;
				double padY = 1.4;
				double rh = fontSize + 2.0 * padY;
				double rw = textW + 2.0 * padX;
				// No invadir la columna Com.
				double maxW = ColDep - 8.0;
				if (rw > maxW)
				{
					rw = maxW;
				}

				double rx = x + 4.0;
				double ry = y0 + (rowH - rh) * 0.5;
				if (ry < y0 + 0.5)
				{
					ry = y0 + 0.5;
				}

				sb.Append(CultureInfo.InvariantCulture,
					$"<rect x=\"{F(rx)}\" y=\"{F(ry)}\" width=\"{F(rw)}\" height=\"{F(rh)}\" fill=\"{palette.HeaderFill}\"/>");
				sb.Append(Text(rx + padX, textY, name, fontSize, "700", palette.HeaderText, "start"));
				nameEndX = rx + rw;
			}
			else
			{
				const double plainFont = 7;
				sb.Append(Text(x + 5, textY, name, plainFont, "400", palette.Text, "start"));
				nameEndX = x + 5 + EstimateTextWidth(name, plainFont, bold: false);
			}

			// Línea de puntos Renfe: del final del nombre al borde izquierdo de Com,
			// para alinear la fila sin división horizontal continua.
			double leaderStart = nameEndX + 2.0;
			if (leaderStart < comLeft - 1.0)
			{
				sb.Append(CultureInfo.InvariantCulture,
					$"<line x1=\"{F(leaderStart)}\" y1=\"{F(midY)}\" x2=\"{F(comLeft)}\" y2=\"{F(midY)}\" stroke=\"{palette.Stroke}\" stroke-width=\"0.55\" stroke-dasharray=\"1.2 1.6\"/>");
			}
		}

		/// <summary>
		/// Ancho aproximado Arial: negrita/mayúsculas más anchas que el factor 0.56 antiguo
		/// (dejaba el rectángulo negro corto en PALMA, MANACOR, PETRA…).
		/// </summary>
		public static double EstimateTextWidth(string text, double fontSize, bool bold = false)
		{
			if (string.IsNullOrEmpty(text))
			{
				return fontSize;
			}

			double em = bold ? 0.66 : 0.58;
			double sum = 0.0;
			int i = 0;
			while (i < text.Length)
			{
				char c = text[i];
				double f = em;
				if (c == 'I' || c == 'i' || c == 'l' || c == '1' || c == '.' || c == ' ')
				{
					f = bold ? 0.38 : 0.32;
				}
				else if (c == 'M' || c == 'W' || c == 'm' || c == 'w' || c == '@')
				{
					f = bold ? 0.88 : 0.78;
				}
				else if (c == 'Á' || c == 'É' || c == 'Í' || c == 'Ó' || c == 'Ú' || c == 'Ñ'
					|| c == 'á' || c == 'é' || c == 'í' || c == 'ó' || c == 'ú' || c == 'ñ')
				{
					f = bold ? 0.72 : 0.62;
				}

				sum += f;
				i++;
			}

			// Holgura fija para anti-aliasing / métricas del motor SVG.
			return sum * fontSize + (bold ? 2.5 : 1.0);
		}

		private static void DrawCom(
			StringBuilder sb,
			double comX,
			double cy,
			CirculationSheetFrontier row,
			CirculationSheetPalette palette)
		{
			if (!row.IsCommercialStop || row.Dwell <= TimeSpan.Zero)
			{
				return;
			}

			string text = CirculationSheetDocument.FormatCommercialDwell(row.Dwell, out bool circle);
			if (circle)
			{
				sb.Append(CultureInfo.InvariantCulture,
					$"<circle cx=\"{F(comX + ColCom * 0.5)}\" cy=\"{F(cy)}\" r=\"2.4\" fill=\"{palette.Stroke}\"/>");
			}
			else if (text.Length > 0)
			{
				sb.Append(Text(comX + ColCom * 0.5, cy + 2.5, text, 7.5, "600", palette.Text, "middle"));
			}
		}

		/// <summary>
		/// Columna Hora: texto a la izquierda (HH.mm con cero a la izquierda).
		/// Si hay fracción de minuto (½), se dibuja a la derecha en tamaño menor.
		/// </summary>
		private static void DrawHora(
			StringBuilder sb,
			double horaX,
			double cy,
			CirculationSheetFrontier row,
			CirculationSheetPalette palette)
		{
			TimeSpan? clock = RowClock(row);
			string main = CirculationSheetDocument.FormatSheetTime(clock, out string half);
			if (main.Length == 0)
			{
				return;
			}

			const double leftPad = 2.5;
			const double mainFont = 8;
			const double halfFont = 5.5;
			double x = horaX + leftPad;
			// Misma línea base visual; el ½ va un poco más alto (estilo superíndice Renfe).
			sb.Append(Text(x, cy + 2.5, main, mainFont, "700", palette.Text, "start"));
			if (half.Length > 0)
			{
				double mainW = EstimateTextWidth(main, mainFont, bold: true);
				sb.Append(Text(x + mainW + 0.4, cy + 1.1, half, halfFont, "700", palette.Text, "start"));
			}
		}

		private static TimeSpan? RowClock(CirculationSheetFrontier row)
		{
			if (row.IsOrigin)
			{
				return row.Departure;
			}

			if (row.IsDestination)
			{
				return row.Arrival ?? row.Departure;
			}

			if (row.IsCommercialStop)
			{
				return row.Departure ?? row.Arrival;
			}

			return row.Departure ?? row.Arrival;
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

using System.Globalization;
using System.Text;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Consigna serie B: A4 apaisado con dos semipáginas (como las hojas de marcha).
	/// Números y «+» fuera de la tabla; filas al alto del contenido; observaciones
	/// envueltas, sin truncar.
	/// </summary>
	public static class ConsignaSerieBSvgRenderer
	{
		public static IReadOnlyList<string> RenderAllPages(ConsignaSerieBDocument document)
		{
			return RenderAllPages(document, out _, out _);
		}

		public static IReadOnlyList<string> RenderAllPages(
			ConsignaSerieBDocument document,
			out string documentSeal,
			out string documentPayload)
		{
			if (document is null)
			{
				throw new ArgumentNullException(nameof(document));
			}

			int halfCount = Math.Max(1, document.Pages.Count);
			int sheetCount = CirculationSheetPager.ComputeSheetCount(halfCount);
			documentPayload = CirculationSheetAuthenticity.BuildDocumentPayload(
				"consigna-b",
				document.TopoName,
				document.EditionLabel,
				document.DateLabel,
				sheetCount,
				document.ConsignaNumber);
			documentSeal = CirculationSheetAuthenticity.ComputeSealCode(documentPayload);

			List<string> sheets = new List<string>(sheetCount);
			int i = 0;
			while (i < document.Pages.Count)
			{
				ConsignaSerieBPage left = document.Pages[i];
				ConsignaSerieBPage? right = i + 1 < document.Pages.Count ? document.Pages[i + 1] : null;
				sheets.Add(RenderSheet(document, left, right, documentSeal, documentPayload));
				i += 2;
			}

			if (sheets.Count == 0)
			{
				ConsignaSerieBPage blank = ConsignaSerieBPage.Cover(
					1, 1, Array.Empty<ConsignaSerieBIndexEntry>());
				sheets.Add(RenderSheet(document, blank, null, documentSeal, documentPayload));
			}

			return sheets;
		}

		public static string RenderSheet(
			ConsignaSerieBDocument document,
			ConsignaSerieBPage left,
			ConsignaSerieBPage? right,
			string sealCode,
			string documentPayload)
		{
			CirculationSheetPalette palette = CirculationSheetPalette.Print;
			StringBuilder sb = CirculationSheetSvgRenderer.BeginSheetSvg(palette);
			double panelW = CirculationSheetSvgRenderer.PageWidth;
			double panelH = CirculationSheetSvgRenderer.PageHeight;
			double y0 = CirculationSheetSvgRenderer.SheetOuterMargin;
			double leftX = CirculationSheetSvgRenderer.SheetOuterMargin;
			double rightX = CirculationSheetSvgRenderer.SheetOuterMargin
				+ panelW + CirculationSheetSvgRenderer.PanelGutter;

			DrawHalf(sb, document, left, leftX, y0, panelW, panelH, sealCode, documentPayload, palette);
			if (right is not null)
			{
				DrawHalf(sb, document, right, rightX, y0, panelW, panelH, sealCode, documentPayload, palette);
			}

			CirculationSheetSvgRenderer.DrawHalfSeparator(sb, leftX, y0, panelW, panelH, palette);
			sb.Append("</svg>");
			return sb.ToString();
		}

		private static void DrawHalf(
			StringBuilder sb,
			ConsignaSerieBDocument document,
			ConsignaSerieBPage page,
			double panelX,
			double panelY,
			double panelW,
			double panelH,
			string sealCode,
			string documentPayload,
			CirculationSheetPalette palette)
		{
			if (page.Kind == ConsignaSerieBPageKind.Cover)
			{
				DrawCover(sb, document, page, panelX, panelY, panelW, panelH, sealCode, palette);
				return;
			}

			if (page.Kind == ConsignaSerieBPageKind.Index)
			{
				DrawIndex(sb, document, page, panelX, panelY, panelW, panelH, sealCode, palette);
				return;
			}

			DrawAxisTable(
				sb, document, page, panelX, panelY, panelW, panelH,
				sealCode, documentPayload, palette);
		}

		private static void DrawCover(
			StringBuilder sb,
			ConsignaSerieBDocument document,
			ConsignaSerieBPage page,
			double panelX,
			double panelY,
			double panelW,
			double panelH,
			string sealCode,
			CirculationSheetPalette palette)
		{
			double contentL = panelX + ConsignaSerieBLayout.PanelPadL;
			double contentR = panelX + panelW - ConsignaSerieBLayout.PanelPadR;
			double y = CirculationSheetSvgRenderer.DrawOfficialCoverHeader(
				sb, contentL, contentR, panelY, "CONSIGNA SERIE B", palette);

			sb.Append(CirculationSheetSvgRenderer.Text(
				contentL + 6, y, document.CoverTitle, 16, "700", palette.Text, "start"));
			y += ConsignaSerieBLayout.CoverTitleH;

			if (!string.IsNullOrEmpty(document.RepealLine))
			{
				sb.Append(CirculationSheetSvgRenderer.Text(
					contentL + 6, y, document.RepealLine, 8, "400", palette.Text, "start"));
				y += 14;
			}

			y = DrawCoverLegend(sb, document.CoverLegend, contentL, y, palette);

			y += ConsignaSerieBLayout.CoverIndexGap;
			DrawIndexTable(sb, page, contentL, contentR, y, palette);

			CirculationSheetSvgRenderer.DrawPanelFooter(
				sb, contentL, contentR, panelY, panelH, document.EditionLabel,
				page.PageNumber, page.PageCount, sealCode, palette);
		}

		private static double DrawCoverLegend(
			StringBuilder sb,
			ConsignaSerieBCoverLegend legend,
			double contentL,
			double y,
			CirculationSheetPalette palette)
		{
			if (legend.ItemCount <= 0)
			{
				return y;
			}

			y += 6;
			sb.Append("<g class=\"diamond-consigna-legend\">");
			sb.Append(CirculationSheetSvgRenderer.Text(
				contentL + 6, y + 9, "Leyenda", 8, "700", palette.Text, "start"));
			y += ConsignaSerieBLayout.LegendTitleH;

			if (legend.ShowHighSpeed)
			{
				DrawLegendSpeedRow(
					sb, contentL, y,
					ConsignaSerieBLayout.LegendHighExampleKmh,
					"Limitación superior a 50 km/h",
					palette);
				y += ConsignaSerieBLayout.LegendRowH;
			}

			if (legend.ShowLowSpeed)
			{
				DrawLegendSpeedRow(
					sb, contentL, y,
					ConsignaSerieBLayout.LegendLowExampleKmh,
					"Limitación igual o inferior a 50 km/h",
					palette);
				y += ConsignaSerieBLayout.LegendRowH;
			}

			if (legend.ShowUnsignaled)
			{
				double iconY = y + (ConsignaSerieBLayout.LegendRowH
					- CirculationSheetSvgRenderer.UnsignaledWarningSize) * 0.5;
				CirculationSheetSvgRenderer.DrawUnsignaledWarning(
					sb,
					contentL + 6,
					iconY,
					CirculationSheetSvgRenderer.UnsignaledWarningSize,
					palette.Text);
				sb.Append(CirculationSheetSvgRenderer.Text(
					contentL + 6 + CirculationSheetSvgRenderer.UnsignaledWarningSize + 5,
					y + ConsignaSerieBLayout.LegendRowH * 0.72,
					"Limitación no señalizada en vía",
					7, "400", palette.Text, "start"));
				y += ConsignaSerieBLayout.LegendRowH;
			}

			sb.Append("</g>");
			return y + 4;
		}

		private static void DrawLegendSpeedRow(
			StringBuilder sb,
			double contentL,
			double y,
			int speed,
			string caption,
			CirculationSheetPalette palette)
		{
			double boxW = ConsignaSerieBLayout.LegendSampleW;
			double boxH = ConsignaSerieBLayout.LegendSampleH;
			double boxY = y + (ConsignaSerieBLayout.LegendRowH - boxH) * 0.5;
			DrawVShade(sb, contentL + 6, boxY, boxH, speed);
			string vFill = speed <= ConsignaSerieBLayout.VShadeThresholdKmh
				? ConsignaSerieBLayout.VTextLow
				: ConsignaSerieBLayout.VTextHigh;
			sb.Append(CirculationSheetSvgRenderer.Text(
				contentL + 6 + boxW * 0.5,
				boxY + boxH * 0.78,
				speed.ToString(CultureInfo.InvariantCulture),
				7, "700", vFill, "middle"));
			sb.Append(CirculationSheetSvgRenderer.Text(
				contentL + 6 + boxW + 5,
				y + ConsignaSerieBLayout.LegendRowH * 0.72,
				caption, 7, "400", palette.Text, "start"));
		}

		private static void DrawIndex(
			StringBuilder sb,
			ConsignaSerieBDocument document,
			ConsignaSerieBPage page,
			double panelX,
			double panelY,
			double panelW,
			double panelH,
			string sealCode,
			CirculationSheetPalette palette)
		{
			double contentL = panelX + ConsignaSerieBLayout.PanelPadL;
			double contentR = panelX + panelW - ConsignaSerieBLayout.PanelPadR;
			double headerW = contentR - contentL;
			double y = panelY + ConsignaSerieBLayout.PanelPadT;

			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{CirculationSheetSvgRenderer.F(contentL)}\" y=\"{CirculationSheetSvgRenderer.F(y)}\" width=\"{CirculationSheetSvgRenderer.F(headerW)}\" height=\"{CirculationSheetSvgRenderer.F(ConsignaSerieBLayout.HeaderBandH)}\" fill=\"{palette.HeaderFill}\"/>");
			string title = page.IndexParts > 1
				? "ÍNDICE · " + page.IndexPart.ToString(CultureInfo.InvariantCulture)
					+ "/" + page.IndexParts.ToString(CultureInfo.InvariantCulture)
				: "ÍNDICE";
			sb.Append(CirculationSheetSvgRenderer.Text(
				contentL + 5, y + ConsignaSerieBLayout.HeaderBandH * 0.72,
				title, 10, "700", palette.HeaderText, "start"));
			y += ConsignaSerieBLayout.HeaderBandH + 6;

			DrawIndexTable(sb, page, contentL, contentR, y, palette);

			CirculationSheetSvgRenderer.DrawPanelFooter(
				sb, contentL, contentR, panelY, panelH, document.EditionLabel,
				page.PageNumber, page.PageCount, sealCode, palette);
		}

		private static void DrawIndexTable(
			StringBuilder sb,
			ConsignaSerieBPage page,
			double contentL,
			double contentR,
			double y,
			CirculationSheetPalette palette)
		{
			double headerW = contentR - contentL;
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{CirculationSheetSvgRenderer.F(contentL)}\" y=\"{CirculationSheetSvgRenderer.F(y)}\" width=\"{CirculationSheetSvgRenderer.F(headerW)}\" height=\"{CirculationSheetSvgRenderer.F(ConsignaSerieBLayout.IndexColHeaderH)}\" fill=\"{palette.HeaderFill}\"/>");
			sb.Append(CirculationSheetSvgRenderer.Text(
				contentL + 6, y + ConsignaSerieBLayout.IndexColHeaderH * 0.72,
				"Eje", 7, "700", palette.HeaderText, "start"));
			sb.Append(CirculationSheetSvgRenderer.Text(
				contentR - 6, y + ConsignaSerieBLayout.IndexColHeaderH * 0.72,
				"Pág", 7, "700", palette.HeaderText, "end"));
			y += ConsignaSerieBLayout.IndexColHeaderH;

			double rowH = ConsignaSerieBLayout.IndexRowH;
			int i = 0;
			while (i < page.IndexLines.Count)
			{
				ConsignaSerieBIndexEntry line = page.IndexLines[i];
				double cy = y + rowH * 0.72;
				sb.Append(CirculationSheetSvgRenderer.Text(
					contentL + 6, cy,
					CirculationSheetSvgRenderer.Truncate(line.Label, 48),
					8, "600", palette.Text, "start"));
				sb.Append(CirculationSheetSvgRenderer.Text(
					contentR - 6, cy,
					line.PageNumber.ToString(CultureInfo.InvariantCulture),
					8, "600", palette.Text, "end"));
				sb.Append(CultureInfo.InvariantCulture,
					$"<line x1=\"{CirculationSheetSvgRenderer.F(contentL)}\" y1=\"{CirculationSheetSvgRenderer.F(y + rowH)}\" x2=\"{CirculationSheetSvgRenderer.F(contentR)}\" y2=\"{CirculationSheetSvgRenderer.F(y + rowH)}\" stroke=\"{palette.MutedStroke}\" stroke-width=\"0.35\"/>");
				y += rowH;
				i++;
			}
		}

		private static void DrawAxisTable(
			StringBuilder sb,
			ConsignaSerieBDocument document,
			ConsignaSerieBPage page,
			double panelX,
			double panelY,
			double panelW,
			double panelH,
			string sealCode,
			string documentPayload,
			CirculationSheetPalette palette)
		{
			double contentL = panelX + ConsignaSerieBLayout.PanelPadL;
			double tableL = contentL + ConsignaSerieBLayout.NumGutter;
			double tableW = ConsignaSerieBLayout.TableWidth(panelW);
			double tableR = tableL + tableW;
			double contentR = panelX + panelW - ConsignaSerieBLayout.PanelPadR;
			double headerW = contentR - contentL;
			double y = panelY + ConsignaSerieBLayout.PanelPadT;
			double qrBandH = CirculationSheetSvgRenderer.HeaderQrBandHeight;
			double qrSize = CirculationSheetSvgRenderer.HeaderQrModuleSize;
			const double qrPad = 2.5;

			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{CirculationSheetSvgRenderer.F(contentL)}\" y=\"{CirculationSheetSvgRenderer.F(y)}\" width=\"{CirculationSheetSvgRenderer.F(headerW)}\" height=\"{CirculationSheetSvgRenderer.F(ConsignaSerieBLayout.HeaderBandH)}\" fill=\"{palette.HeaderFill}\"/>");
			sb.Append(CirculationSheetSvgRenderer.Text(
				contentL + 5, y + ConsignaSerieBLayout.HeaderBandH * 0.72,
				"CONSIGNA SERIE B", 10, "700", palette.HeaderText, "start"));
			double tipoCx = contentR - qrPad - qrSize * 0.5;
			sb.Append(CirculationSheetSvgRenderer.Text(
				tipoCx, y + ConsignaSerieBLayout.HeaderBandH * 0.72,
				"nº " + document.ConsignaNumber, 9, "600", palette.HeaderText, "middle"));
			y += ConsignaSerieBLayout.HeaderBandH;

			double yBelowHeader = y;
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{CirculationSheetSvgRenderer.F(contentL)}\" y=\"{CirculationSheetSvgRenderer.F(y)}\" width=\"{CirculationSheetSvgRenderer.F(headerW)}\" height=\"{CirculationSheetSvgRenderer.F(qrBandH)}\" fill=\"{palette.SubHeaderFill}\" stroke=\"{palette.Stroke}\" stroke-width=\"0.5\"/>");
			sb.Append(CirculationSheetSvgRenderer.Text(
				contentL + 5, y + 16,
				CirculationSheetSvgRenderer.Truncate(page.AxisHeaderText, 42),
				9, "700", palette.Text, "start"));
			CirculationSheetSvgRenderer.DrawHeaderQr(
				sb, contentR, yBelowHeader, sealCode ?? string.Empty,
				documentPayload ?? string.Empty, palette);
			y += qrBandH;

			double colCenter = ConsignaSerieBLayout.CenterWidth(panelW);
			double xKmL = tableL;
			double xVL = xKmL + ConsignaSerieBLayout.ColKm;
			double xSt = xVL + ConsignaSerieBLayout.ColV;
			double xVR = xSt + colCenter;
			double xKmR = xVR + ConsignaSerieBLayout.ColV;
			double numX = contentL + ConsignaSerieBLayout.NumGutter * 0.5;

			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{CirculationSheetSvgRenderer.F(tableL)}\" y=\"{CirculationSheetSvgRenderer.F(y)}\" width=\"{CirculationSheetSvgRenderer.F(tableW)}\" height=\"{CirculationSheetSvgRenderer.F(ConsignaSerieBLayout.ColHeaderH)}\" fill=\"{palette.HeaderFill}\"/>");
			sb.Append(CirculationSheetSvgRenderer.Text(
				numX, y + 15, "Nº", 6, "700", palette.Text, "middle"));
			sb.Append(CirculationSheetSvgRenderer.Text(
				xKmL + (ConsignaSerieBLayout.ColKm + ConsignaSerieBLayout.ColV) * 0.5,
				y + 10, "↑ Vía II", 6.5, "700", palette.HeaderText, "middle"));
			sb.Append(CirculationSheetSvgRenderer.Text(
				xSt + colCenter * 0.5, y + 10, "Estaciones", 6.5, "700", palette.HeaderText, "middle"));
			sb.Append(CirculationSheetSvgRenderer.Text(
				xVR + (ConsignaSerieBLayout.ColV + ConsignaSerieBLayout.ColKm) * 0.5,
				y + 10, "Vía I ↓", 6.5, "700", palette.HeaderText, "middle"));
			sb.Append(CirculationSheetSvgRenderer.Text(
				xKmL + ConsignaSerieBLayout.ColKm * 0.5, y + 20, "Km", 6, "600", palette.HeaderText, "middle"));
			sb.Append(CirculationSheetSvgRenderer.Text(
				xVL + ConsignaSerieBLayout.ColV * 0.5, y + 20, "V", 6, "600", palette.HeaderText, "middle"));
			sb.Append(CirculationSheetSvgRenderer.Text(
				xVR + ConsignaSerieBLayout.ColV * 0.5, y + 20, "V", 6, "600", palette.HeaderText, "middle"));
			sb.Append(CirculationSheetSvgRenderer.Text(
				xKmR + ConsignaSerieBLayout.ColKm * 0.5, y + 20, "Km", 6, "600", palette.HeaderText, "middle"));
			y += ConsignaSerieBLayout.ColHeaderH;

			double bodyTop = y;
			double[] rowHeights = new double[page.Rows.Count];
			double[] rowGaps = new double[page.Rows.Count];
			double bodyH = 0;
			int ri = 0;
			while (ri < page.Rows.Count)
			{
				rowGaps[ri] = ConsignaSerieBLayout.JoinGapBefore(page.Rows, ri);
				rowHeights[ri] = ConsignaSerieBLayout.MeasureRow(page.Rows[ri], panelW);
				bodyH += rowGaps[ri] + rowHeights[ri];
				ri++;
			}

			if (bodyH < ConsignaSerieBLayout.EmptyBodyH)
			{
				bodyH = ConsignaSerieBLayout.EmptyBodyH;
			}

			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{CirculationSheetSvgRenderer.F(tableL)}\" y=\"{CirculationSheetSvgRenderer.F(bodyTop)}\" width=\"{CirculationSheetSvgRenderer.F(tableW)}\" height=\"{CirculationSheetSvgRenderer.F(bodyH)}\" fill=\"none\" stroke=\"{palette.Stroke}\" stroke-width=\"0.5\"/>");

			double rowY = bodyTop;
			ri = 0;
			while (ri < page.Rows.Count)
			{
				rowY += rowGaps[ri];
				DrawRow(
					sb, page.Rows[ri],
					rowY, rowHeights[ri],
					numX, xKmL, xVL, xSt, xVR, xKmR, tableR, colCenter,
					palette);
				rowY += rowHeights[ri];
				ri++;
			}

			DrawVerticals(
				sb, tableL, bodyTop, bodyH,
				xVL, xSt, xVR, xKmR, tableR, palette);

			CirculationSheetSvgRenderer.DrawPanelFooter(
				sb, contentL, tableR, panelY, panelH, document.EditionLabel,
				page.PageNumber, page.PageCount, sealCode, palette);
		}

		private static void DrawRow(
			StringBuilder sb,
			ConsignaSerieBRow row,
			double y0,
			double rowH,
			double numX,
			double xKmL,
			double xVL,
			double xSt,
			double xVR,
			double xKmR,
			double tableR,
			double colCenter,
			CirculationSheetPalette palette)
		{
			if (row.IsStation)
			{
				DrawInnerHorizontal(sb, xSt, xVR, y0, palette);
				DrawInnerHorizontal(sb, xSt, xVR, y0 + rowH, palette);
				DrawStation(sb, row.StationName, xSt, y0, colCenter, rowH, palette);
				return;
			}

			ConsignaSerieBEntry? entry = row.Entry;
			if (entry is null)
			{
				return;
			}

			double mid = y0 + rowH * 0.5;
			if (entry.IsNew)
			{
				sb.Append(CirculationSheetSvgRenderer.Text(
					numX, mid - 3, "+", 8, "700", palette.Text, "middle"));
				sb.Append(CirculationSheetSvgRenderer.Text(
					numX, mid + 7,
					entry.Number.ToString(CultureInfo.InvariantCulture),
					8, "700", palette.Text, "middle"));
			}
			else
			{
				sb.Append(CirculationSheetSvgRenderer.Text(
					numX, mid + 3,
					entry.Number.ToString(CultureInfo.InvariantCulture),
					8, "700", palette.Text, "middle"));
			}

			string km0 = CirculationSheetDocument.FormatStationKm(Math.Min(entry.Limit.PK, entry.Limit.PKEnd));
			string kmf = CirculationSheetDocument.FormatStationKm(Math.Max(entry.Limit.PK, entry.Limit.PKEnd));
			string v = entry.Limit.Speed.ToString(CultureInfo.InvariantCulture);
			if (entry.AppliesLeft)
			{
				DrawVShade(sb, xVL, y0, rowH, entry.Limit.Speed);
				DrawInnerHorizontal(sb, xVL, xSt, y0, palette);
				DrawInnerHorizontal(sb, xVL, xSt, y0 + rowH, palette);
				DrawPkSpeed(sb, xKmL, xVL, y0, rowH, km0, kmf, v, entry.Limit.Speed, palette);
			}

			if (entry.AppliesRight)
			{
				DrawVShade(sb, xVR, y0, rowH, entry.Limit.Speed);
				DrawInnerHorizontal(sb, xVR, xKmR, y0, palette);
				DrawInnerHorizontal(sb, xVR, xKmR, y0 + rowH, palette);
				DrawPkSpeed(sb, xKmR, xVR, y0, rowH, km0, kmf, v, entry.Limit.Speed, palette);
			}

			double textX = xSt + 4;
			if (!entry.Limit.SignaledOnTrack)
			{
				CirculationSheetSvgRenderer.DrawUnsignaledWarning(
					sb,
					textX,
					y0 + ConsignaSerieBLayout.LimitPadY,
					CirculationSheetSvgRenderer.UnsignaledWarningSize,
					palette.Text);
				textX += CirculationSheetSvgRenderer.UnsignaledWarningSize + 1.8;
			}

			double maxW = colCenter - (textX - xSt) - 4;
			double ty = y0 + ConsignaSerieBLayout.LimitPadY + ConsignaSerieBLayout.LineH * 0.82;
			List<string> reasonLines = ConsignaSerieBLayout.WrapLines(
				entry.ReasonLabel, ConsignaSerieBLayout.ReasonFont, true, maxW);
			int li = 0;
			while (li < reasonLines.Count)
			{
				sb.Append(CirculationSheetSvgRenderer.Text(
					textX, ty, reasonLines[li],
					ConsignaSerieBLayout.ReasonFont, "600", palette.Text, "start"));
				ty += ConsignaSerieBLayout.LineH;
				li++;
			}

			if (!string.IsNullOrWhiteSpace(entry.Limit.Observations))
			{
				List<string> obsLines = ConsignaSerieBLayout.WrapLines(
					entry.Limit.Observations.Trim(), ConsignaSerieBLayout.ObsFont, false, maxW);
				int oi = 0;
				while (oi < obsLines.Count)
				{
					sb.Append(CirculationSheetSvgRenderer.Text(
						textX, ty, obsLines[oi],
						ConsignaSerieBLayout.ObsFont, "400", palette.Text, "start"));
					ty += ConsignaSerieBLayout.LineH;
					oi++;
				}
			}

			sb.Append(CirculationSheetSvgRenderer.Text(
				textX, ty,
				ConsignaSerieBLayout.FormatCreatedLabel(entry.Limit.CreatedAt),
				ConsignaSerieBLayout.DateFont, "400", palette.Text, "start"));
			ty += ConsignaSerieBLayout.LineH;

			int si = 0;
			while (si < entry.InteriorStations.Count)
			{
				string interior = entry.InteriorStations[si];
				double stH = ConsignaSerieBLayout.MeasureStationAt(interior, colCenter);
				DrawInnerHorizontal(sb, xSt, xVR, ty - 1, palette);
				DrawStation(sb, interior, xSt, ty - 1, colCenter, stH, palette);
				DrawInnerHorizontal(sb, xSt, xVR, ty - 1 + stH, palette);
				ty += stH;
				si++;
			}
		}

		private static void DrawStation(
			StringBuilder sb,
			string name,
			double xSt,
			double y0,
			double colCenter,
			double rowH,
			CirculationSheetPalette palette)
		{
			double textW = colCenter - 6;
			List<string> lines = ConsignaSerieBLayout.WrapLines(
				name, ConsignaSerieBLayout.StationFont, true, textW);
			if (lines.Count == 0)
			{
				return;
			}

			double blockH = lines.Count * ConsignaSerieBLayout.LineH;
			double ty = y0 + (rowH - blockH) * 0.5 + ConsignaSerieBLayout.LineH * 0.78;
			int i = 0;
			while (i < lines.Count)
			{
				sb.Append(CirculationSheetSvgRenderer.Text(
					xSt + colCenter * 0.5, ty, lines[i],
					ConsignaSerieBLayout.StationFont, "700", palette.Text, "middle"));
				ty += ConsignaSerieBLayout.LineH;
				i++;
			}
		}

		private static void DrawVShade(
			StringBuilder sb,
			double xV,
			double y0,
			double rowH,
			int speed)
		{
			string fill = speed <= ConsignaSerieBLayout.VShadeThresholdKmh
				? ConsignaSerieBLayout.VFillLow
				: ConsignaSerieBLayout.VFillHigh;
			sb.Append(CultureInfo.InvariantCulture,
				$"<rect x=\"{CirculationSheetSvgRenderer.F(xV)}\" y=\"{CirculationSheetSvgRenderer.F(y0)}\" width=\"{CirculationSheetSvgRenderer.F(ConsignaSerieBLayout.ColV)}\" height=\"{CirculationSheetSvgRenderer.F(rowH)}\" fill=\"{fill}\"/>");
		}

		private static void DrawPkSpeed(
			StringBuilder sb,
			double xKm,
			double xV,
			double y0,
			double rowH,
			string km0,
			string kmf,
			string speed,
			int speedValue,
			CirculationSheetPalette palette)
		{
			double mid = y0 + rowH * 0.5;
			double kmX = xKm + ConsignaSerieBLayout.ColKm * 0.5;
			double kmFont = ConsignaSerieBLayout.KmFont;
			sb.Append(CirculationSheetSvgRenderer.Text(
				kmX, KmBaseline(y0), km0, kmFont, "700", palette.Text, "middle"));
			sb.Append(CirculationSheetSvgRenderer.Text(
				kmX, KmBaseline(y0 + rowH), kmf, kmFont, "700", palette.Text, "middle"));

			string vFill = speedValue <= ConsignaSerieBLayout.VShadeThresholdKmh
				? ConsignaSerieBLayout.VTextLow
				: ConsignaSerieBLayout.VTextHigh;
			sb.Append(CirculationSheetSvgRenderer.Text(
				xV + ConsignaSerieBLayout.ColV * 0.5, mid + 3, speed, 8, "700", vFill, "middle"));
		}

		private static double KmBaseline(double centerY)
		{
			return centerY + ConsignaSerieBLayout.KmFont * 0.36;
		}

		private static void DrawInnerHorizontal(
			StringBuilder sb,
			double x0,
			double x1,
			double y,
			CirculationSheetPalette palette)
		{
			sb.Append(CultureInfo.InvariantCulture,
				$"<line x1=\"{CirculationSheetSvgRenderer.F(x0)}\" y1=\"{CirculationSheetSvgRenderer.F(y)}\" x2=\"{CirculationSheetSvgRenderer.F(x1)}\" y2=\"{CirculationSheetSvgRenderer.F(y)}\" stroke=\"{palette.Stroke}\" stroke-width=\"0.35\"/>");
		}

		private static void DrawVerticals(
			StringBuilder sb,
			double tableL,
			double y0,
			double h,
			double xVL,
			double xSt,
			double xVR,
			double xKmR,
			double tableR,
			CirculationSheetPalette palette)
		{
			double[] xs = { tableL, xVL, xSt, xVR, xKmR, tableR };
			int i = 0;
			while (i < xs.Length)
			{
				sb.Append(CultureInfo.InvariantCulture,
					$"<line x1=\"{CirculationSheetSvgRenderer.F(xs[i])}\" y1=\"{CirculationSheetSvgRenderer.F(y0)}\" x2=\"{CirculationSheetSvgRenderer.F(xs[i])}\" y2=\"{CirculationSheetSvgRenderer.F(y0 + h)}\" stroke=\"{palette.Stroke}\" stroke-width=\"0.4\"/>");
				i++;
			}
		}
	}
}

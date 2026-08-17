using System.Globalization;
using System.Text;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Métricas y paginación de la consigna: filas al alto del contenido,
	/// bloques estación–limitaciones–estación indivisibles.
	/// </summary>
	internal static class ConsignaSerieBLayout
	{
		public const double NumGutter = 22;
		public const double ColKm = 36;
		public const double ColV = 22;
		public const double HeaderBandH = 18;
		public const double SubHeaderH = 16;
		public const double ColHeaderH = 24;
		public const double FooterReserve = 16;
		public const double PanelPadT = 8;
		public const double PanelPadL = 6;
		public const double PanelPadR = 8;
		public const double StationRowMinH = 14;
		public const double StationFont = 7.0;
		public const double ReasonFont = 6.5;
		public const double ObsFont = 6.0;
		public const double LineH = 8.0;
		public const double LimitPadY = 3.0;
		public const double PkMinH = 16.0;
		public const double KmFont = 9.0;
		/// <summary>Umbral: V ≤ 50 texto blanco sobre gris muy oscuro; V ≥ 51 negro sobre gris claro.</summary>
		public const int VShadeThresholdKmh = 50;
		public const string VFillHigh = "#d9d9d9";
		public const string VTextHigh = "#000000";
		public const string VFillLow = "#2b2b2b";
		public const string VTextLow = "#ffffff";
		public const double DateFont = 5.5;
		/// <summary>
		/// Hueco entre dos limitaciones consecutivas: cabe el km final de la de
		/// arriba y el km inicial de la de abajo, centrados en sus respectivas rayas.
		/// </summary>
		public const double KmJoinGap = KmFont + 3.0;
		/// <summary>Hueco entre dos estaciones consecutivas sin limitaciones entre ellas.</summary>
		public const double StationJoinGap = 6.0;
		public const double EmptyBodyH = 10.0;
		public const double CoverTitleGap = 18.0;
		public const double CoverTitleH = 28.0;
		public const double CoverDateH = 18.0;
		public const double CoverIndexGap = 14.0;
		public const double IndexColHeaderH = 15.0;
		public const double IndexRowH = 14.0;

		public static double TableWidth(double panelW)
		{
			double w = panelW - PanelPadL - PanelPadR - NumGutter;
			return w < 80 ? 80 : w;
		}

		public static double CenterWidth(double panelW)
		{
			double c = TableWidth(panelW) - (2.0 * (ColKm + ColV));
			return c < 60 ? 60 : c;
		}

		public static double AvailableBody()
		{
			double panelH = CirculationSheetSvgRenderer.PageHeight;
			double used = PanelPadT + HeaderBandH
				+ CirculationSheetSvgRenderer.HeaderQrBandHeight
				+ ColHeaderH + FooterReserve;
			double avail = panelH - used;
			return avail < StationRowMinH ? StationRowMinH : avail;
		}

		public static int CoverIndexCapacity()
		{
			double panelH = CirculationSheetSvgRenderer.PageHeight;
			double used = PanelPadT + HeaderBandH
				+ CirculationDocumentBranding.CoverLogoGapAfterHeader
				+ CirculationDocumentBranding.CoverLogoH
				+ CirculationDocumentBranding.CoverLogoGapAfter
				+ CoverTitleH
				+ CoverDateH + CoverIndexGap + IndexColHeaderH + FooterReserve;
			int n = (int)((panelH - used) / IndexRowH);
			return n < 4 ? 4 : n;
		}

		public static int IndexPageCapacity()
		{
			double panelH = CirculationSheetSvgRenderer.PageHeight;
			double used = PanelPadT + HeaderBandH + 6.0 + IndexColHeaderH + 4.0 + FooterReserve;
			int n = (int)((panelH - used) / IndexRowH);
			return n < 8 ? 8 : n;
		}

		public static bool IsLimitRow(ConsignaSerieBRow row)
		{
			return !row.IsStation && row.Entry is not null;
		}

		public static double JoinGapBefore(IReadOnlyList<ConsignaSerieBRow> rows, int index)
		{
			if (index <= 0 || index >= rows.Count)
			{
				return 0;
			}

			if (IsLimitRow(rows[index - 1]) && IsLimitRow(rows[index]))
			{
				return KmJoinGap;
			}

			if (rows[index - 1].IsStation && rows[index].IsStation)
			{
				return StationJoinGap;
			}

			return 0;
		}

		public static double MeasureRowsHeight(IReadOnlyList<ConsignaSerieBRow> rows, double panelW)
		{
			double h = 0;
			int i = 0;
			while (i < rows.Count)
			{
				h += JoinGapBefore(rows, i);
				h += MeasureRow(rows[i], panelW);
				i++;
			}

			return h;
		}

		public static double MeasureRow(ConsignaSerieBRow row, double panelW)
		{
			if (row.IsStation)
			{
				return MeasureStation(row.StationName, panelW);
			}

			if (row.Entry is not null)
			{
				return MeasureLimit(row.Entry, panelW);
			}

			return StationRowMinH;
		}

		public static string FormatCreatedLabel(DateTime createdAt)
		{
			DateTime day = createdAt.Kind == DateTimeKind.Utc
				? createdAt.ToLocalTime()
				: createdAt;
			return "(" + day.ToString("dd-MM-yy", CultureInfo.InvariantCulture) + ")";
		}

		public static double MeasureStation(string name, double panelW)
		{
			return MeasureStationAt(name, CenterWidth(panelW));
		}

		public static double MeasureStationAt(string name, double colCenter)
		{
			List<string> lines = WrapLines(name ?? string.Empty, StationFont, true, colCenter - 8.0);
			int n = lines.Count < 1 ? 1 : lines.Count;
			double h = (n * LineH) + 4.0;
			return h < StationRowMinH ? StationRowMinH : h;
		}

		public static double MeasureBlock(ConsignaSerieBBlock block, double panelW)
		{
			return MeasureBlock(block, panelW, string.Empty);
		}

		public static double MeasureBlock(ConsignaSerieBBlock block, double panelW, string lastStation)
		{
			double h = 0;
			if (!string.IsNullOrEmpty(block.StationBefore)
				&& !string.Equals(block.StationBefore, lastStation, StringComparison.Ordinal))
			{
				if (!string.IsNullOrEmpty(lastStation))
				{
					h += StationJoinGap;
				}

				h += MeasureStation(block.StationBefore, panelW);
			}

			int i = 0;
			while (i < block.Limits.Count)
			{
				if (i > 0)
				{
					h += KmJoinGap;
				}

				h += MeasureLimit(block.Limits[i], panelW);
				i++;
			}

			if (!string.IsNullOrEmpty(block.StationAfter)
				&& !string.Equals(block.StationAfter, block.StationBefore, StringComparison.Ordinal)
				&& !string.Equals(block.StationAfter, lastStation, StringComparison.Ordinal))
			{
				h += MeasureStation(block.StationAfter, panelW);
			}

			return h;
		}

		public static double MeasureLimit(ConsignaSerieBEntry entry, double panelW)
		{
			double center = CenterWidth(panelW) - 8.0;
			double textH = 0;
			List<string> reason = WrapLines(entry.ReasonLabel, ReasonFont, true, center);
			textH += reason.Count * LineH;
			if (!string.IsNullOrWhiteSpace(entry.Limit.Observations))
			{
				List<string> lines = WrapLines(entry.Limit.Observations.Trim(), ObsFont, false, center);
				textH += lines.Count * LineH;
			}

			textH += LineH;

			int si = 0;
			while (si < entry.InteriorStations.Count)
			{
				textH += MeasureStation(entry.InteriorStations[si], panelW);
				si++;
			}

			if (textH < PkMinH)
			{
				textH = PkMinH;
			}

			return textH + (2.0 * LimitPadY);
		}

		public static List<string> WrapLines(string text, double fontSize, bool bold, double maxWidth)
		{
			List<string> lines = new List<string>();
			if (string.IsNullOrEmpty(text))
			{
				return lines;
			}

			string[] paragraphs = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
			int p = 0;
			while (p < paragraphs.Length)
			{
				WrapParagraph(paragraphs[p], fontSize, bold, maxWidth, lines);
				p++;
			}

			if (lines.Count == 0)
			{
				lines.Add(string.Empty);
			}

			return lines;
		}

		private static void WrapParagraph(
			string paragraph,
			double fontSize,
			bool bold,
			double maxWidth,
			List<string> lines)
		{
			if (string.IsNullOrEmpty(paragraph))
			{
				lines.Add(string.Empty);
				return;
			}

			string[] words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (words.Length == 0)
			{
				lines.Add(string.Empty);
				return;
			}

			StringBuilder current = new StringBuilder();
			int i = 0;
			while (i < words.Length)
			{
				EmitWord(words[i], fontSize, bold, maxWidth, current, lines);
				i++;
			}

			if (current.Length > 0)
			{
				lines.Add(current.ToString());
			}
		}

		private static void EmitWord(
			string word,
			double fontSize,
			bool bold,
			double maxWidth,
			StringBuilder current,
			List<string> lines)
		{
			if (current.Length > 0)
			{
				string trial = current.ToString() + " " + word;
				if (CirculationSheetSvgRenderer.EstimateTextWidth(trial, fontSize, bold) <= maxWidth)
				{
					current.Append(' ');
					current.Append(word);
					return;
				}

				lines.Add(current.ToString());
				current.Clear();
			}

			string rest = word;
			while (rest.Length > 1
				&& CirculationSheetSvgRenderer.EstimateTextWidth(rest, fontSize, bold) > maxWidth)
			{
				string piece = BreakLongWord(rest, fontSize, bold, maxWidth, out rest);
				if (piece.Length == 0)
				{
					piece = rest.Substring(0, 1);
					rest = rest.Substring(1);
				}

				lines.Add(piece);
			}

			current.Append(rest);
		}

		private static string BreakLongWord(
			string word,
			double fontSize,
			bool bold,
			double maxWidth,
			out string rest)
		{
			int n = word.Length;
			while (n > 1
				&& CirculationSheetSvgRenderer.EstimateTextWidth(word.Substring(0, n), fontSize, bold) > maxWidth)
			{
				n--;
			}

			rest = n < word.Length ? word.Substring(n) : string.Empty;
			return word.Substring(0, n);
		}

		public static List<ConsignaSerieBPage> PaginateByHeight(
			IReadOnlyList<ConsignaSerieBAxisSection> sections)
		{
			double panelW = CirculationSheetSvgRenderer.PageWidth;
			double avail = AvailableBody();
			List<ConsignaSerieBPage> draft = new List<ConsignaSerieBPage>();

			int si = 0;
			while (si < sections.Count)
			{
				ConsignaSerieBAxisSection section = sections[si];
				if (section.Blocks.Count > 0)
				{
					List<ConsignaSerieBBlock> current = new List<ConsignaSerieBBlock>();
					double used = 0;
					string lastStation = string.Empty;
					bool first = true;
					int bi = 0;
					while (bi < section.Blocks.Count)
					{
						ConsignaSerieBBlock block = section.Blocks[bi];
						double h = MeasureBlock(block, panelW, lastStation);
						if (current.Count > 0 && used + h > avail)
						{
							draft.Add(ConsignaSerieBPage.Axis(
								section.AxisId, section.AxisName, Flatten(current), first));
							first = false;
							current = new List<ConsignaSerieBBlock>();
							used = 0;
							lastStation = string.Empty;
							h = MeasureBlock(block, panelW, lastStation);
						}

						current.Add(block);
						used += h;
						if (!string.IsNullOrEmpty(block.StationAfter))
						{
							lastStation = block.StationAfter;
						}
						else if (!string.IsNullOrEmpty(block.StationBefore))
						{
							lastStation = block.StationBefore;
						}

						bi++;
					}

					if (current.Count > 0)
					{
						draft.Add(ConsignaSerieBPage.Axis(
							section.AxisId, section.AxisName, Flatten(current), first));
					}
				}

				si++;
			}

			return AssignAxisSheets(draft);
		}

		public static List<ConsignaSerieBPage> AssembleBook(IReadOnlyList<ConsignaSerieBPage> content)
		{
			IReadOnlyList<ConsignaSerieBPage> axisPages = AssignAxisSheets(content);
			int coverCap = CoverIndexCapacity();
			int indexCap = IndexPageCapacity();
			int extraIndex = 0;
			if (axisPages.Count > coverCap)
			{
				int remain = axisPages.Count - coverCap;
				extraIndex = (remain + indexCap - 1) / indexCap;
			}

			int front = 1 + extraIndex;
			int total = front + axisPages.Count;
			if (total < 1)
			{
				total = 1;
			}

			List<ConsignaSerieBIndexEntry> index = new List<ConsignaSerieBIndexEntry>(axisPages.Count);
			int i = 0;
			while (i < axisPages.Count)
			{
				ConsignaSerieBPage p = axisPages[i];
				index.Add(new ConsignaSerieBIndexEntry(
					p.AxisName, p.AxisSheetIndex, p.AxisSheetCount, front + i + 1));
				i++;
			}

			List<ConsignaSerieBPage> book = new List<ConsignaSerieBPage>(total);
			int takeCover = index.Count < coverCap ? index.Count : coverCap;
			book.Add(ConsignaSerieBPage.Cover(1, total, Slice(index, 0, takeCover)));

			int offset = takeCover;
			int part = 2;
			int parts = 1 + extraIndex;
			int pageNo = 2;
			while (offset < index.Count)
			{
				int take = index.Count - offset;
				if (take > indexCap)
				{
					take = indexCap;
				}

				book.Add(ConsignaSerieBPage.Index(
					pageNo, total, Slice(index, offset, take), part, parts));
				offset += take;
				part++;
				pageNo++;
			}

			i = 0;
			while (i < axisPages.Count)
			{
				book.Add(axisPages[i].WithPaging(front + i + 1, total));
				i++;
			}

			return book;
		}

		private static List<ConsignaSerieBPage> AssignAxisSheets(IReadOnlyList<ConsignaSerieBPage> pages)
		{
			List<ConsignaSerieBPage> result = new List<ConsignaSerieBPage>(pages.Count);
			int i = 0;
			while (i < pages.Count)
			{
				string id = pages[i].AxisId;
				int j = i + 1;
				while (j < pages.Count
					&& string.Equals(pages[j].AxisId, id, StringComparison.Ordinal))
				{
					j++;
				}

				int count = j - i;
				int k = i;
				int n = 1;
				while (k < j)
				{
					result.Add(pages[k].WithAxisSheets(n, count));
					n++;
					k++;
				}

				i = j;
			}

			return result;
		}

		private static List<ConsignaSerieBIndexEntry> Slice(
			IReadOnlyList<ConsignaSerieBIndexEntry> source,
			int offset,
			int count)
		{
			List<ConsignaSerieBIndexEntry> slice = new List<ConsignaSerieBIndexEntry>(count);
			int i = 0;
			while (i < count && offset + i < source.Count)
			{
				slice.Add(source[offset + i]);
				i++;
			}

			return slice;
		}

		public static List<ConsignaSerieBRow> Flatten(IReadOnlyList<ConsignaSerieBBlock> blocks)
		{
			List<ConsignaSerieBRow> rows = new List<ConsignaSerieBRow>();
			string last = string.Empty;
			int i = 0;
			while (i < blocks.Count)
			{
				ConsignaSerieBBlock block = blocks[i];
				if (!string.IsNullOrEmpty(block.StationBefore)
					&& !string.Equals(block.StationBefore, last, StringComparison.Ordinal))
				{
					rows.Add(ConsignaSerieBRow.Station(block.StationBefore));
					last = block.StationBefore;
				}

				int k = 0;
				while (k < block.Limits.Count)
				{
					rows.Add(ConsignaSerieBRow.Limit(block.Limits[k]));
					k++;
				}

				if (!string.IsNullOrEmpty(block.StationAfter)
					&& !string.Equals(block.StationAfter, last, StringComparison.Ordinal))
				{
					rows.Add(ConsignaSerieBRow.Station(block.StationAfter));
					last = block.StationAfter;
				}

				i++;
			}

			return rows;
		}
	}
}

using System.Globalization;
using Diamond.Topo;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Consigna serie B: limitaciones temporales por eje, numeradas al redactar.
	/// Vía II a la izquierda (↓); vía I a la derecha (↑); ambas si aplica a las dos.
	/// </summary>
	public sealed class ConsignaSerieBDocument
	{
		public const string PendingNumber = "XX";

		private readonly string mvarTopoName;
		private readonly string mvarEditionLabel;
		private readonly string mvarConsignaNumber;
		private readonly string mvarDateLabel;
		private readonly string mvarPreviousNumber;
		private readonly IReadOnlyList<ConsignaSerieBAxisSection> mcolAxes;
		private readonly IReadOnlyList<ConsignaSerieBPage> mcolPages;
		private readonly IReadOnlyList<ConsignaSerieBIndexEntry> mcolIndex;

		private ConsignaSerieBDocument(
			string topoName,
			string editionLabel,
			string consignaNumber,
			string dateLabel,
			string previousNumber,
			IReadOnlyList<ConsignaSerieBAxisSection> axes,
			IReadOnlyList<ConsignaSerieBPage> pages,
			IReadOnlyList<ConsignaSerieBIndexEntry> index)
		{
			mvarTopoName = topoName ?? string.Empty;
			mvarEditionLabel = editionLabel ?? string.Empty;
			mvarConsignaNumber = string.IsNullOrWhiteSpace(consignaNumber)
				? PendingNumber
				: consignaNumber.Trim();
			mvarDateLabel = dateLabel ?? string.Empty;
			mvarPreviousNumber = previousNumber ?? string.Empty;
			mcolAxes = axes;
			mcolPages = pages;
			mcolIndex = index;
		}

		public string TopoName
		{
			get { return mvarTopoName; }
		}

		public string EditionLabel
		{
			get { return mvarEditionLabel; }
		}

		public string ConsignaNumber
		{
			get { return mvarConsignaNumber; }
		}

		public string DateLabel
		{
			get { return mvarDateLabel; }
		}

		public string PreviousNumber
		{
			get { return mvarPreviousNumber; }
		}

		public string CoverTitle
		{
			get
			{
				string title = "Consigna Serie B nº " + mvarConsignaNumber;
				if (string.IsNullOrEmpty(mvarDateLabel))
				{
					return title;
				}

				return title + "  (" + mvarDateLabel + ")";
			}
		}

		public string RepealLine
		{
			get
			{
				if (string.IsNullOrWhiteSpace(mvarPreviousNumber))
				{
					return string.Empty;
				}

				return "Deroga Consigna Serie B nº " + mvarPreviousNumber.Trim() + " y anteriores";
			}
		}

		public IReadOnlyList<ConsignaSerieBAxisSection> Axes
		{
			get { return mcolAxes; }
		}

		public IReadOnlyList<ConsignaSerieBPage> Pages
		{
			get { return mcolPages; }
		}

		public IReadOnlyList<ConsignaSerieBIndexEntry> Index
		{
			get { return mcolIndex; }
		}

		public int EntryCount
		{
			get
			{
				int n = 0;
				int i = 0;
				while (i < mcolAxes.Count)
				{
					n += mcolAxes[i].Entries.Count;
					i++;
				}

				return n;
			}
		}

		public static ConsignaSerieBDocument Build(
			TopoLayout layout,
			IReadOnlyList<TemporarySpeedLimit> limits,
			string? topoName = null,
			string? editionLabel = null,
			string? consignaNumber = null,
			DateTime? date = null,
			string? previousNumber = null)
		{
			if (layout is null)
			{
				throw new ArgumentNullException(nameof(layout));
			}

			string name = string.IsNullOrWhiteSpace(topoName)
				? (string.IsNullOrWhiteSpace(layout.Info?.Id) ? "Topología" : layout.Info.Id)
				: topoName.Trim();
			DateTime when = date ?? DateTime.Now;
			string dateLabel = when.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
			string edition = editionLabel
				?? ("Zafiro · " + dateLabel);
			string number = string.IsNullOrWhiteSpace(consignaNumber)
				? PendingNumber
				: consignaNumber.Trim();

			List<TemporarySpeedLimit> raw = new List<TemporarySpeedLimit>();
			if (limits is not null)
			{
				int li = 0;
				while (li < limits.Count)
				{
					if (limits[li] is not null)
					{
						raw.Add(limits[li]);
					}

					li++;
				}
			}

			List<ConsignaSerieBAxisSection> sections = new List<ConsignaSerieBAxisSection>();
			int nextNumber = 1;
			int ai = 0;
			while (ai < layout.Axes.Count)
			{
				Axis axis = layout.Axes[ai];
				List<TemporarySpeedLimit> ofAxis = LimitsOfAxis(raw, axis.Id);
				ofAxis.Sort(CompareByPk);
				List<ConsignaSerieBEntry> entries = new List<ConsignaSerieBEntry>(ofAxis.Count);
				int ei = 0;
				while (ei < ofAxis.Count)
				{
					TemporarySpeedLimit limit = ofAxis[ei];
					FindFlankingStations(
						axis,
						limit.PK,
						limit.PKEnd,
						out string beforeName,
						out string afterName);
					entries.Add(new ConsignaSerieBEntry(
						nextNumber,
						limit.IsNewCreation,
						limit,
						beforeName,
						afterName,
						FindInteriorStations(axis, limit.PK, limit.PKEnd, beforeName, afterName)));
					nextNumber++;
					ei++;
				}

				if (entries.Count > 0)
				{
					string axisTitle = string.IsNullOrWhiteSpace(axis.Name)
						? (string.IsNullOrWhiteSpace(axis.Id) ? "Eje" : axis.Id)
						: axis.Name;
					List<ConsignaSerieBBlock> blocks = BuildBlocks(entries);
					List<ConsignaSerieBRow> rows = ConsignaSerieBLayout.Flatten(blocks);
					sections.Add(new ConsignaSerieBAxisSection(axis.Id, axisTitle, entries, rows, blocks));
				}

				ai++;
			}

			List<ConsignaSerieBPage> content = ConsignaSerieBLayout.PaginateByHeight(sections);
			List<ConsignaSerieBPage> pages = ConsignaSerieBLayout.AssembleBook(content);
			List<ConsignaSerieBIndexEntry> index = new List<ConsignaSerieBIndexEntry>();
			int pi = 0;
			while (pi < pages.Count)
			{
				if (pages[pi].Kind == ConsignaSerieBPageKind.Axis)
				{
					index.Add(new ConsignaSerieBIndexEntry(
						pages[pi].AxisName,
						pages[pi].AxisSheetIndex,
						pages[pi].AxisSheetCount,
						pages[pi].PageNumber));
				}

				pi++;
			}

			return new ConsignaSerieBDocument(
				name, edition, number, dateLabel,
				previousNumber ?? string.Empty,
				sections, pages, index);
		}

		private static List<ConsignaSerieBBlock> BuildBlocks(IReadOnlyList<ConsignaSerieBEntry> entries)
		{
			HashSet<string> absorbed = new HashSet<string>(StringComparer.Ordinal);
			List<ConsignaSerieBBlock> blocks = new List<ConsignaSerieBBlock>();
			List<ConsignaSerieBEntry> current = new List<ConsignaSerieBEntry>();
			string currentBefore = string.Empty;
			bool open = false;
			int i = 0;
			while (i < entries.Count)
			{
				ConsignaSerieBEntry raw = entries[i];
				List<string> show = new List<string>();
				int ii = 0;
				while (ii < raw.InteriorStations.Count)
				{
					string name = raw.InteriorStations[ii];
					if (absorbed.Add(name))
					{
						show.Add(name);
					}

					ii++;
				}

				ConsignaSerieBEntry entry = raw.WithInteriors(show);
				string before = raw.StationBefore;
				if (absorbed.Contains(before))
				{
					before = string.Empty;
				}

				if (!open)
				{
					currentBefore = before;
					current.Add(entry);
					open = true;
				}
				else if (string.IsNullOrEmpty(before)
					|| string.Equals(before, currentBefore, StringComparison.Ordinal))
				{
					current.Add(entry);
				}
				else
				{
					blocks.Add(new ConsignaSerieBBlock(currentBefore, current, before));
					currentBefore = before;
					current = new List<ConsignaSerieBEntry>();
					current.Add(entry);
				}

				i++;
			}

			if (open)
			{
				string after = current[current.Count - 1].StationAfter;
				if (absorbed.Contains(after))
				{
					after = string.Empty;
				}

				blocks.Add(new ConsignaSerieBBlock(currentBefore, current, after));
			}

			return blocks;
		}

		private static IReadOnlyList<string> FindInteriorStations(
			Axis axis,
			long pk0,
			long pkf,
			string beforeName,
			string afterName)
		{
			long lo = Math.Min(pk0, pkf);
			long hi = Math.Max(pk0, pkf);
			List<(long Pk, string Name)> found = new List<(long, string)>();
			int i = 0;
			while (i < axis.Stations.Count)
			{
				StationOnAxis st = axis.Stations[i];
				if (st.PK > lo && st.PK < hi)
				{
					string name = StationLabel(st.Station);
					if (name.Length > 0
						&& !string.Equals(name, beforeName, StringComparison.Ordinal)
						&& !string.Equals(name, afterName, StringComparison.Ordinal))
					{
						found.Add((st.PK, name));
					}
				}

				i++;
			}

			found.Sort((a, b) => a.Pk.CompareTo(b.Pk));
			List<string> names = new List<string>();
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			i = 0;
			while (i < found.Count)
			{
				if (seen.Add(found[i].Name))
				{
					names.Add(found[i].Name);
				}

				i++;
			}

			return names;
		}

		private static List<TemporarySpeedLimit> LimitsOfAxis(
			IReadOnlyList<TemporarySpeedLimit> all,
			string axisId)
		{
			List<TemporarySpeedLimit> salida = new List<TemporarySpeedLimit>();
			int i = 0;
			while (i < all.Count)
			{
				if (string.Equals(all[i].AxisId, axisId, StringComparison.Ordinal))
				{
					salida.Add(all[i]);
				}

				i++;
			}

			return salida;
		}

		private static int CompareByPk(TemporarySpeedLimit a, TemporarySpeedLimit b)
		{
			long a0 = Math.Min(a.PK, a.PKEnd);
			long b0 = Math.Min(b.PK, b.PKEnd);
			int c = a0.CompareTo(b0);
			if (c != 0)
			{
				return c;
			}

			return Math.Max(a.PK, a.PKEnd).CompareTo(Math.Max(b.PK, b.PKEnd));
		}

		private static void FindFlankingStations(
			Axis axis,
			long pk0,
			long pkf,
			out string beforeName,
			out string afterName)
		{
			beforeName = string.Empty;
			afterName = string.Empty;
			long lo = Math.Min(pk0, pkf);
			long hi = Math.Max(pk0, pkf);
			StationOnAxis? before = null;
			StationOnAxis? after = null;
			int i = 0;
			while (i < axis.Stations.Count)
			{
				StationOnAxis st = axis.Stations[i];
				if (st.PK <= lo)
				{
					if (before is null || st.PK >= before.PK)
					{
						before = st;
					}
				}

				if (st.PK >= hi)
				{
					if (after is null || st.PK < after.PK)
					{
						after = st;
					}
				}

				i++;
			}

			if (before is not null)
			{
				beforeName = StationLabel(before.Station);
			}

			if (after is not null)
			{
				afterName = StationLabel(after.Station);
				if (before is not null && ReferenceEquals(before.Station, after.Station))
				{
					afterName = string.Empty;
				}
				else if (before is not null
					&& string.Equals(beforeName, afterName, StringComparison.OrdinalIgnoreCase))
				{
					afterName = string.Empty;
				}
			}
		}

		private static string StationLabel(Station station)
		{
			if (!string.IsNullOrWhiteSpace(station.Name))
			{
				return station.Name.Trim().ToUpperInvariant();
			}

			if (!string.IsNullOrWhiteSpace(station.Avr))
			{
				return station.Avr.Trim().ToUpperInvariant();
			}

			return (station.Id ?? string.Empty).ToUpperInvariant();
		}

	}

	public sealed class ConsignaSerieBBlock
	{
		private readonly string mvarStationBefore;
		private readonly IReadOnlyList<ConsignaSerieBEntry> mcolLimits;
		private readonly string mvarStationAfter;

		public ConsignaSerieBBlock(
			string stationBefore,
			IReadOnlyList<ConsignaSerieBEntry> limits,
			string stationAfter)
		{
			mvarStationBefore = stationBefore ?? string.Empty;
			mcolLimits = limits ?? Array.Empty<ConsignaSerieBEntry>();
			mvarStationAfter = stationAfter ?? string.Empty;
		}

		public string StationBefore
		{
			get { return mvarStationBefore; }
		}

		public IReadOnlyList<ConsignaSerieBEntry> Limits
		{
			get { return mcolLimits; }
		}

		public string StationAfter
		{
			get { return mvarStationAfter; }
		}
	}

	public sealed class ConsignaSerieBRow
	{
		private readonly bool mvarIsStation;
		private readonly string mvarStationName;
		private readonly ConsignaSerieBEntry? mvarEntry;

		private ConsignaSerieBRow(bool isStation, string stationName, ConsignaSerieBEntry? entry)
		{
			mvarIsStation = isStation;
			mvarStationName = stationName ?? string.Empty;
			mvarEntry = entry;
		}

		public static ConsignaSerieBRow Station(string name)
		{
			return new ConsignaSerieBRow(true, name, null);
		}

		public static ConsignaSerieBRow Limit(ConsignaSerieBEntry entry)
		{
			return new ConsignaSerieBRow(false, string.Empty, entry);
		}

		public bool IsStation
		{
			get { return mvarIsStation; }
		}

		public string StationName
		{
			get { return mvarStationName; }
		}

		public ConsignaSerieBEntry? Entry
		{
			get { return mvarEntry; }
		}
	}

	public sealed class ConsignaSerieBAxisSection
	{
		private readonly string mvarAxisId;
		private readonly string mvarAxisName;
		private readonly IReadOnlyList<ConsignaSerieBEntry> mcolEntries;
		private readonly IReadOnlyList<ConsignaSerieBRow> mcolRows;
		private readonly IReadOnlyList<ConsignaSerieBBlock> mcolBlocks;

		public ConsignaSerieBAxisSection(
			string axisId,
			string axisName,
			IReadOnlyList<ConsignaSerieBEntry> entries,
			IReadOnlyList<ConsignaSerieBRow> rows,
			IReadOnlyList<ConsignaSerieBBlock> blocks)
		{
			mvarAxisId = axisId ?? string.Empty;
			mvarAxisName = axisName ?? string.Empty;
			mcolEntries = entries ?? Array.Empty<ConsignaSerieBEntry>();
			mcolRows = rows ?? Array.Empty<ConsignaSerieBRow>();
			mcolBlocks = blocks ?? Array.Empty<ConsignaSerieBBlock>();
		}

		public string AxisId
		{
			get { return mvarAxisId; }
		}

		public string AxisName
		{
			get { return mvarAxisName; }
		}

		public IReadOnlyList<ConsignaSerieBEntry> Entries
		{
			get { return mcolEntries; }
		}

		public IReadOnlyList<ConsignaSerieBRow> Rows
		{
			get { return mcolRows; }
		}

		public IReadOnlyList<ConsignaSerieBBlock> Blocks
		{
			get { return mcolBlocks; }
		}
	}

	public sealed class ConsignaSerieBEntry
	{
		private readonly int mvarNumber;
		private readonly bool mvarIsNew;
		private readonly TemporarySpeedLimit mvarLimit;
		private readonly string mvarStationBefore;
		private readonly string mvarStationAfter;
		private readonly IReadOnlyList<string> mcolInteriors;

		public ConsignaSerieBEntry(
			int number,
			bool isNew,
			TemporarySpeedLimit limit,
			string stationBefore,
			string stationAfter)
			: this(number, isNew, limit, stationBefore, stationAfter, Array.Empty<string>())
		{
		}

		public ConsignaSerieBEntry(
			int number,
			bool isNew,
			TemporarySpeedLimit limit,
			string stationBefore,
			string stationAfter,
			IReadOnlyList<string>? interiors)
		{
			mvarNumber = number;
			mvarIsNew = isNew;
			mvarLimit = limit ?? throw new ArgumentNullException(nameof(limit));
			mvarStationBefore = stationBefore ?? string.Empty;
			mvarStationAfter = stationAfter ?? string.Empty;
			mcolInteriors = interiors ?? Array.Empty<string>();
		}

		public ConsignaSerieBEntry WithInteriors(IReadOnlyList<string> interiors)
		{
			return new ConsignaSerieBEntry(
				mvarNumber, mvarIsNew, mvarLimit,
				mvarStationBefore, mvarStationAfter, interiors);
		}

		public int Number
		{
			get { return mvarNumber; }
		}

		public bool IsNew
		{
			get { return mvarIsNew; }
		}

		public TemporarySpeedLimit Limit
		{
			get { return mvarLimit; }
		}

		public string StationBefore
		{
			get { return mvarStationBefore; }
		}

		public string StationAfter
		{
			get { return mvarStationAfter; }
		}

		/// <summary>Estaciones con PK estrictamente interior a la limitación (no se repiten fuera).</summary>
		public IReadOnlyList<string> InteriorStations
		{
			get { return mcolInteriors; }
		}

		public bool AppliesLeft
		{
			get
			{
				return mvarLimit.Track == TemporaryLimitTrack.Track2
					|| mvarLimit.Track == TemporaryLimitTrack.Both;
			}
		}

		public bool AppliesRight
		{
			get
			{
				return mvarLimit.Track == TemporaryLimitTrack.Track1
					|| mvarLimit.Track == TemporaryLimitTrack.Both;
			}
		}

		public string ReasonLabel
		{
			get { return TemporaryLimitReasonText.Label(mvarLimit.Reason); }
		}
	}

	public sealed class ConsignaSerieBIndexEntry
	{
		private readonly string mvarAxisName;
		private readonly int mvarAxisSheetIndex;
		private readonly int mvarAxisSheetCount;
		private readonly int mvarPageNumber;

		public ConsignaSerieBIndexEntry(
			string axisName,
			int axisSheetIndex,
			int axisSheetCount,
			int pageNumber)
		{
			mvarAxisName = axisName ?? string.Empty;
			mvarAxisSheetIndex = axisSheetIndex;
			mvarAxisSheetCount = axisSheetCount;
			mvarPageNumber = pageNumber;
		}

		public string AxisName
		{
			get { return mvarAxisName; }
		}

		public int AxisSheetIndex
		{
			get { return mvarAxisSheetIndex; }
		}

		public int AxisSheetCount
		{
			get { return mvarAxisSheetCount; }
		}

		public int PageNumber
		{
			get { return mvarPageNumber; }
		}

		public string Label
		{
			get
			{
				if (mvarAxisSheetCount <= 1)
				{
					return mvarAxisName;
				}

				return mvarAxisName
					+ "  "
					+ mvarAxisSheetIndex.ToString(CultureInfo.InvariantCulture)
					+ " de "
					+ mvarAxisSheetCount.ToString(CultureInfo.InvariantCulture);
			}
		}
	}

	public enum ConsignaSerieBPageKind
	{
		Cover = 0,
		Index = 1,
		Axis = 2
	}

	public sealed class ConsignaSerieBPage
	{
		private readonly ConsignaSerieBPageKind mvarKind;
		private readonly int mvarPageNumber;
		private readonly int mvarPageCount;
		private readonly string mvarAxisId;
		private readonly string mvarAxisName;
		private readonly IReadOnlyList<ConsignaSerieBRow> mcolRows;
		private readonly bool mvarFirstOfAxis;
		private readonly int mvarAxisSheetIndex;
		private readonly int mvarAxisSheetCount;
		private readonly IReadOnlyList<ConsignaSerieBIndexEntry> mcolIndexLines;
		private readonly int mvarIndexPart;
		private readonly int mvarIndexParts;

		private ConsignaSerieBPage(
			ConsignaSerieBPageKind kind,
			int pageNumber,
			int pageCount,
			string axisId,
			string axisName,
			IReadOnlyList<ConsignaSerieBRow> rows,
			bool firstOfAxis,
			int axisSheetIndex,
			int axisSheetCount,
			IReadOnlyList<ConsignaSerieBIndexEntry> indexLines,
			int indexPart,
			int indexParts)
		{
			mvarKind = kind;
			mvarPageNumber = pageNumber;
			mvarPageCount = pageCount;
			mvarAxisId = axisId ?? string.Empty;
			mvarAxisName = axisName ?? string.Empty;
			mcolRows = rows ?? Array.Empty<ConsignaSerieBRow>();
			mvarFirstOfAxis = firstOfAxis;
			mvarAxisSheetIndex = axisSheetIndex;
			mvarAxisSheetCount = axisSheetCount;
			mcolIndexLines = indexLines ?? Array.Empty<ConsignaSerieBIndexEntry>();
			mvarIndexPart = indexPart;
			mvarIndexParts = indexParts;
		}

		public static ConsignaSerieBPage Cover(
			int pageNumber,
			int pageCount,
			IReadOnlyList<ConsignaSerieBIndexEntry> indexLines)
		{
			return new ConsignaSerieBPage(
				ConsignaSerieBPageKind.Cover,
				pageNumber, pageCount,
				string.Empty, string.Empty,
				Array.Empty<ConsignaSerieBRow>(),
				false, 0, 0,
				indexLines, 1, 1);
		}

		public static ConsignaSerieBPage Index(
			int pageNumber,
			int pageCount,
			IReadOnlyList<ConsignaSerieBIndexEntry> indexLines,
			int part,
			int parts)
		{
			return new ConsignaSerieBPage(
				ConsignaSerieBPageKind.Index,
				pageNumber, pageCount,
				string.Empty, string.Empty,
				Array.Empty<ConsignaSerieBRow>(),
				false, 0, 0,
				indexLines, part, parts);
		}

		public static ConsignaSerieBPage Axis(
			string axisId,
			string axisName,
			IReadOnlyList<ConsignaSerieBRow> rows,
			bool firstOfAxis)
		{
			return new ConsignaSerieBPage(
				ConsignaSerieBPageKind.Axis,
				0, 0,
				axisId, axisName,
				rows,
				firstOfAxis, 0, 0,
				Array.Empty<ConsignaSerieBIndexEntry>(), 0, 0);
		}

		public ConsignaSerieBPage WithPaging(int pageNumber, int pageCount)
		{
			return new ConsignaSerieBPage(
				mvarKind, pageNumber, pageCount,
				mvarAxisId, mvarAxisName, mcolRows, mvarFirstOfAxis,
				mvarAxisSheetIndex, mvarAxisSheetCount,
				mcolIndexLines, mvarIndexPart, mvarIndexParts);
		}

		public ConsignaSerieBPage WithAxisSheets(int sheetIndex, int sheetCount)
		{
			return new ConsignaSerieBPage(
				mvarKind, mvarPageNumber, mvarPageCount,
				mvarAxisId, mvarAxisName, mcolRows, sheetIndex == 1,
				sheetIndex, sheetCount,
				mcolIndexLines, mvarIndexPart, mvarIndexParts);
		}

		public ConsignaSerieBPageKind Kind
		{
			get { return mvarKind; }
		}

		public int PageNumber
		{
			get { return mvarPageNumber; }
		}

		public int PageCount
		{
			get { return mvarPageCount; }
		}

		public string AxisId
		{
			get { return mvarAxisId; }
		}

		public string AxisName
		{
			get { return mvarAxisName; }
		}

		public IReadOnlyList<ConsignaSerieBRow> Rows
		{
			get { return mcolRows; }
		}

		public bool FirstOfAxis
		{
			get { return mvarFirstOfAxis; }
		}

		public int AxisSheetIndex
		{
			get { return mvarAxisSheetIndex; }
		}

		public int AxisSheetCount
		{
			get { return mvarAxisSheetCount; }
		}

		public IReadOnlyList<ConsignaSerieBIndexEntry> IndexLines
		{
			get { return mcolIndexLines; }
		}

		public int IndexPart
		{
			get { return mvarIndexPart; }
		}

		public int IndexParts
		{
			get { return mvarIndexParts; }
		}

		public string AxisHeaderText
		{
			get
			{
				if (string.IsNullOrEmpty(mvarAxisName))
				{
					return string.Empty;
				}

				if (mvarAxisSheetCount <= 1)
				{
					return mvarAxisName;
				}

				return mvarAxisName
					+ "  "
					+ mvarAxisSheetIndex.ToString(CultureInfo.InvariantCulture)
					+ " de "
					+ mvarAxisSheetCount.ToString(CultureInfo.InvariantCulture);
			}
		}
	}
}

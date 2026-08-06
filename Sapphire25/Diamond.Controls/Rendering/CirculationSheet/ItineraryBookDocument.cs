using System.Globalization;
using Diamond.Motion;
using Diamond.Timed;
using Diamond.Topo;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Libro itinerario de toda la malla: portada + índice + fichas de todos los trenes
	/// agrupados por asimilación / recorrido. La paginación se cuenta en semipáginas
	/// (mitades de A4 apaisado; 1 hoja física = 2 semipáginas).
	/// </summary>
	public sealed class ItineraryBookDocument
	{
		private readonly string mvarPlanName;
		private readonly string mvarNotes;
		private readonly string mvarDayLabel;
		private readonly string mvarEditionLabel;
		private readonly int mvarTrainCount;
		private readonly int mvarGroupCount;
		private readonly int mvarTotalHalfPages;
		private readonly IReadOnlyList<ItineraryBookHalfPage> mcolHalfPages;
		private readonly IReadOnlyList<ItineraryIndexEntry> mcolIndex;

		private ItineraryBookDocument(
			string planName,
			string notes,
			string dayLabel,
			string editionLabel,
			int trainCount,
			int groupCount,
			int totalHalfPages,
			IReadOnlyList<ItineraryBookHalfPage> halfPages,
			IReadOnlyList<ItineraryIndexEntry> index)
		{
			mvarPlanName = planName;
			mvarNotes = notes;
			mvarDayLabel = dayLabel;
			mvarEditionLabel = editionLabel;
			mvarTrainCount = trainCount;
			mvarGroupCount = groupCount;
			mvarTotalHalfPages = totalHalfPages;
			mcolHalfPages = halfPages;
			mcolIndex = index;
		}

		public string PlanName
		{
			get { return mvarPlanName; }
		}

		public string Notes
		{
			get { return mvarNotes; }
		}

		public string DayLabel
		{
			get { return mvarDayLabel; }
		}

		public string EditionLabel
		{
			get { return mvarEditionLabel; }
		}

		public int TrainCount
		{
			get { return mvarTrainCount; }
		}

		public int GroupCount
		{
			get { return mvarGroupCount; }
		}

		/// <summary>Total de semipáginas (mitades de libro).</summary>
		public int TotalHalfPages
		{
			get { return mvarTotalHalfPages; }
		}

		/// <summary>Hojas físicas A4 apaisadas (2 semipáginas por hoja).</summary>
		public int PhysicalSheetCount
		{
			get { return CirculationSheetPager.ComputeSheetCount(mvarTotalHalfPages); }
		}

		public IReadOnlyList<ItineraryBookHalfPage> HalfPages
		{
			get { return mcolHalfPages; }
		}

		public IReadOnlyList<ItineraryIndexEntry> Index
		{
			get { return mcolIndex; }
		}

		/// <summary>
		/// Construye el libro con todos los trenes de la malla.
		/// </summary>
		/// <param name="demandPlan">
		/// Plan de demanda (opcional): resuelve <see cref="ServiceDays"/> por DemandId.
		/// </param>
		/// <param name="exploitation">
		/// Plan de explotación multi-día (opcional): une los días en que aparece cada tren.
		/// </param>
		public static ItineraryBookDocument Build(
			Mesh mesh,
			string? planName = null,
			string? notes = null,
			DayOfWeek? planningDay = null,
			int maxFrontiersPerHalf = CirculationSheetPager.DefaultMaxFrontiersPerPage,
			string? editionLabel = null,
			Plan? demandPlan = null,
			ExploitationPlan? exploitation = null)
		{
			if (mesh is null)
			{
				throw new ArgumentNullException(nameof(mesh));
			}

			string plan = string.IsNullOrWhiteSpace(planName) ? "Plan Diamond" : planName.Trim();
			string notesText = notes?.Trim() ?? string.Empty;
			string day = planningDay.HasValue
				? FormatDay(planningDay.Value)
				: string.Empty;
			string edition = editionLabel
				?? ("DIAMOND · " + DateTime.Now.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));

			// 1) Ordenar trenes: impares → pares → número → salida.
			// Solo circulaciones con número de plantilla (4923…); nunca Id técnico.
			List<Circulation> ordered = SortCirculations(mesh.Circulations);
			List<Circulation> numbered = new List<Circulation>(ordered.Count);
			int oi = 0;
			while (oi < ordered.Count)
			{
				if (ordered[oi].HasServiceNumber)
				{
					numbered.Add(ordered[oi]);
				}

				oi++;
			}

			// 2) Ficha de cada tren (páginas de libro propias).
			List<TrainSlot> slots = new List<TrainSlot>(numbered.Count);
			int gi = 0;
			string? prevGroup = null;
			int groupCount = 0;
			while (gi < numbered.Count)
			{
				Circulation c = numbered[gi];
				string groupKey = GroupKey(c);
				if (!string.Equals(groupKey, prevGroup, StringComparison.Ordinal))
				{
					groupCount++;
					prevGroup = groupKey;
				}

				ServiceDays? serviceDays = ResolveServiceDays(c, demandPlan, exploitation, mesh.PlanningDay);
				CirculationSheetDocument doc = CirculationSheetDocument.Build(
					c, mesh, maxFrontiersPerHalf, edition, serviceDays);
				slots.Add(new TrainSlot(c, doc, groupKey, GroupTitle(c)));
				gi++;
			}

			// 3) Índice (una línea por tren + cabeceras de grupo).
			List<ItineraryIndexEntry> indexDraft = new List<ItineraryIndexEntry>();
			string? lastGroup = null;
			int si = 0;
			while (si < slots.Count)
			{
				TrainSlot slot = slots[si];
				if (!string.Equals(slot.GroupKey, lastGroup, StringComparison.Ordinal))
				{
					indexDraft.Add(ItineraryIndexEntry.GroupHeader(slot.GroupTitle));
					lastGroup = slot.GroupKey;
				}

				// PageStart se rellena tras conocer el layout.
				indexDraft.Add(ItineraryIndexEntry.Train(
					slot.Document.TrainNumber,
					slot.Document.Relation,
					slot.Circulation.Departure,
					pageStart: 0));
				si++;
			}

			// 4) Contar semipáginas del índice (líneas que caben por mitad).
			const int indexLinesPerHalf = 28;
			int indexHalfCount = indexDraft.Count == 0
				? 1
				: (indexDraft.Count + indexLinesPerHalf - 1) / indexLinesPerHalf;
			if (indexHalfCount < 1)
			{
				indexHalfCount = 1;
			}

			// 5) Portada (1) + índice + tablas de trenes.
			int trainHalfCount = 0;
			si = 0;
			while (si < slots.Count)
			{
				trainHalfCount += slots[si].Document.Pages.Count;
				si++;
			}

			int totalHalf = 1 + indexHalfCount + trainHalfCount;
			if (totalHalf < 1)
			{
				totalHalf = 1;
			}

			// 6) Asignar números de semipágina a cada tren e índice.
			int pageCursor = 1 + indexHalfCount; // primera semipágina de trenes
			List<ItineraryIndexEntry> indexFinal = new List<ItineraryIndexEntry>(indexDraft.Count);
			int indexTrain = 0;
			int ii = 0;
			while (ii < indexDraft.Count)
			{
				ItineraryIndexEntry e = indexDraft[ii];
				if (e.IsGroupHeader)
				{
					indexFinal.Add(e);
				}
				else
				{
					TrainSlot slot = slots[indexTrain];
					int start = pageCursor;
					indexFinal.Add(ItineraryIndexEntry.Train(
						e.TrainNumber,
						e.Relation,
						e.Departure,
						start));
					pageCursor += slot.Document.Pages.Count;
					indexTrain++;
				}

				ii++;
			}

			// 7) Secuencia de semipáginas.
			List<ItineraryBookHalfPage> halves = new List<ItineraryBookHalfPage>(totalHalf);
			int pageNo = 1;

			// Portada
			halves.Add(ItineraryBookHalfPage.Cover(
				pageNo, totalHalf, plan, notesText, day, edition,
				slots.Count, groupCount));
			pageNo++;

			// Índice (repartido en mitades)
			int idxOffset = 0;
			int ih = 0;
			while (ih < indexHalfCount)
			{
				int take = Math.Min(indexLinesPerHalf, indexFinal.Count - idxOffset);
				if (take < 0)
				{
					take = 0;
				}

				List<ItineraryIndexEntry> slice = new List<ItineraryIndexEntry>(take);
				int k = 0;
				while (k < take)
				{
					slice.Add(indexFinal[idxOffset + k]);
					k++;
				}

				halves.Add(ItineraryBookHalfPage.Index(pageNo, totalHalf, slice, ih + 1, indexHalfCount));
				idxOffset += take;
				pageNo++;
				ih++;
			}

			// Tablas de trenes
			si = 0;
			while (si < slots.Count)
			{
				TrainSlot slot = slots[si];
				int pi = 0;
				while (pi < slot.Document.Pages.Count)
				{
					halves.Add(ItineraryBookHalfPage.TrainTable(
						pageNo, totalHalf, slot.Document, slot.Document.Pages[pi]));
					pageNo++;
					pi++;
				}

				si++;
			}

			return new ItineraryBookDocument(
				plan,
				notesText,
				day,
				edition,
				slots.Count,
				groupCount,
				halves.Count,
				halves,
				indexFinal);
		}

		private static List<Circulation> SortCirculations(IReadOnlyList<Circulation> source)
		{
			List<Circulation> list = new List<Circulation>(source.Count);
			int i = 0;
			while (i < source.Count)
			{
				list.Add(source[i]);
				i++;
			}

			// Orden global (la numeración 49xx es común a ida y vuelta, aunque la
			// asimilación/recorrido sea distinta):
			//   1) impares (ida típica) → pares (vuelta) → no numéricos
			//   2) número de tren (numérico)
			//   3) hora de salida
			//   4) grupo (estabilidad / cabeceras de índice)
			list.Sort(static (a, b) =>
			{
				int parityA = ServiceNumberParityRank(a);
				int parityB = ServiceNumberParityRank(b);
				if (parityA != parityB)
				{
					return parityA.CompareTo(parityB);
				}

				string na = a.HasServiceNumber ? a.ServiceNumber : a.Id;
				string nb = b.HasServiceNumber ? b.ServiceNumber : b.Id;
				int n = CompareServiceNumbers(na, nb);
				if (n != 0)
				{
					return n;
				}

				int t = a.Departure.CompareTo(b.Departure);
				if (t != 0)
				{
					return t;
				}

				return string.Compare(GroupKey(a), GroupKey(b), StringComparison.OrdinalIgnoreCase);
			});

			return list;
		}

		/// <summary>0 = impar (ida típica), 1 = par (vuelta), 2 = no numérico.</summary>
		private static int ServiceNumberParityRank(Circulation c)
		{
			string s = c.HasServiceNumber ? c.ServiceNumber : c.Id;
			long n;
			if (!TryParseTrailingNumber(s, out n))
			{
				return 2;
			}

			return (n % 2L) == 1L ? 0 : 1;
		}

		private static int CompareServiceNumbers(string a, string b)
		{
			long na;
			long nb;
			bool aOk = TryParseTrailingNumber(a, out na);
			bool bOk = TryParseTrailingNumber(b, out nb);
			if (aOk && bOk)
			{
				return na.CompareTo(nb);
			}

			return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>Extrae el número de tren (p. ej. "4903" o "R-4903" → 4903).</summary>
		public static bool TryParseTrailingNumber(string text, out long number)
		{
			number = 0;
			if (string.IsNullOrWhiteSpace(text))
			{
				return false;
			}

			// Tomar la racha final de dígitos.
			int end = text.Length - 1;
			while (end >= 0 && !char.IsDigit(text[end]))
			{
				end--;
			}

			if (end < 0)
			{
				return false;
			}

			int start = end;
			while (start >= 0 && char.IsDigit(text[start]))
			{
				start--;
			}

			start++;
			string digits = text.Substring(start, end - start + 1);
			return long.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
		}

		/// <summary>
		/// Días de circulación: 1) demanda del plan, 2) unión en plan de explotación,
		/// 3) día único de la malla filtrada.
		/// </summary>
		public static ServiceDays? ResolveServiceDays(
			Circulation c,
			Plan? demandPlan,
			ExploitationPlan? exploitation,
			DayOfWeek? meshPlanningDay)
		{
			if (demandPlan is not null && !string.IsNullOrWhiteSpace(c.DemandId))
			{
				int i = 0;
				while (i < demandPlan.Demand.Count)
				{
					DemandRequirement req = demandPlan.Demand[i];
					if (string.Equals(req.Id, c.DemandId, StringComparison.OrdinalIgnoreCase))
					{
						return req.ServiceDays;
					}

					i++;
				}
			}

			if (exploitation is not null)
			{
				ServiceDay mask = ServiceDay.None;
				foreach (KeyValuePair<DayOfWeek, Mesh> kv in exploitation.MeshesByDay)
				{
					int j = 0;
					while (j < kv.Value.Circulations.Count)
					{
						Circulation other = kv.Value.Circulations[j];
						if (string.Equals(other.TechnicalId, c.TechnicalId, StringComparison.Ordinal)
							|| (c.HasServiceNumber && other.HasServiceNumber
								&& string.Equals(other.ServiceNumber, c.ServiceNumber, StringComparison.Ordinal)))
						{
							mask |= ServiceDays.FromDayOfWeek(kv.Key);
							break;
						}

						j++;
					}
				}

				if (mask != ServiceDay.None)
				{
					return new ServiceDays(mask);
				}
			}

			if (meshPlanningDay.HasValue)
			{
				return ServiceDays.FromDayOfWeekMask(meshPlanningDay.Value);
			}

			return null;
		}

		/// <summary>Clave de agrupación: demanda + firma de recorrido (ida/vuelta distintas).</summary>
		public static string GroupKey(Circulation c)
		{
			string demand = string.IsNullOrWhiteSpace(c.DemandId) ? "_" : c.DemandId.Trim();
			string path = c.Asimilation.View.PathSignature();
			string origin = c.Asimilation.Origin.Station.Avr
				?? c.Asimilation.Origin.Station.Id;
			string dest = c.Asimilation.Destination.Station.Avr
				?? c.Asimilation.Destination.Station.Id;
			return demand + "|" + path + "|" + origin + ">" + dest;
		}

		public static string GroupTitle(Circulation c)
		{
			// Índice del libro: recorrido y material, sin Id de demanda/asimilación.
			string o = FormatStation(c.Asimilation.Origin.Station);
			string d = FormatStation(c.Asimilation.Destination.Station);
			string mat = c.Asimilation.Specs is not null
				? (string.IsNullOrEmpty(c.Asimilation.Specs.Id)
					? c.Asimilation.Specs.Name
					: c.Asimilation.Specs.Id)
				: string.Empty;
			if (string.IsNullOrEmpty(mat))
			{
				return o + " → " + d;
			}

			return mat + " · " + o + " → " + d;
		}

		private static string FormatStation(Station st)
		{
			if (!string.IsNullOrWhiteSpace(st.Name))
			{
				return st.Name.Trim();
			}

			if (!string.IsNullOrWhiteSpace(st.Avr))
			{
				return st.Avr.Trim();
			}

			return st.Id;
		}

		private static string FormatDay(DayOfWeek d)
		{
			switch (d)
			{
				case DayOfWeek.Monday: return "Lunes";
				case DayOfWeek.Tuesday: return "Martes";
				case DayOfWeek.Wednesday: return "Miércoles";
				case DayOfWeek.Thursday: return "Jueves";
				case DayOfWeek.Friday: return "Viernes";
				case DayOfWeek.Saturday: return "Sábado";
				case DayOfWeek.Sunday: return "Domingo";
				default: return d.ToString();
			}
		}

		private readonly struct TrainSlot
		{
			public TrainSlot(Circulation circulation, CirculationSheetDocument document, string groupKey, string groupTitle)
			{
				Circulation = circulation;
				Document = document;
				GroupKey = groupKey;
				GroupTitle = groupTitle;
			}

			public Circulation Circulation { get; }
			public CirculationSheetDocument Document { get; }
			public string GroupKey { get; }
			public string GroupTitle { get; }
		}
	}

	/// <summary>Entrada del índice del libro.</summary>
	public sealed class ItineraryIndexEntry
	{
		private readonly bool mvarIsGroupHeader;
		private readonly string mvarTrainNumber;
		private readonly string mvarRelation;
		private readonly TimeSpan mvarDeparture;
		private readonly int mvarPageStart;
		private readonly string mvarGroupTitle;

		private ItineraryIndexEntry(
			bool isGroupHeader,
			string trainNumber,
			string relation,
			TimeSpan departure,
			int pageStart,
			string groupTitle)
		{
			mvarIsGroupHeader = isGroupHeader;
			mvarTrainNumber = trainNumber;
			mvarRelation = relation;
			mvarDeparture = departure;
			mvarPageStart = pageStart;
			mvarGroupTitle = groupTitle;
		}

		public static ItineraryIndexEntry GroupHeader(string title)
		{
			return new ItineraryIndexEntry(true, string.Empty, string.Empty, TimeSpan.Zero, 0, title);
		}

		public static ItineraryIndexEntry Train(string number, string relation, TimeSpan departure, int pageStart)
		{
			return new ItineraryIndexEntry(false, number, relation, departure, pageStart, string.Empty);
		}

		public bool IsGroupHeader
		{
			get { return mvarIsGroupHeader; }
		}

		public string TrainNumber
		{
			get { return mvarTrainNumber; }
		}

		public string Relation
		{
			get { return mvarRelation; }
		}

		public TimeSpan Departure
		{
			get { return mvarDeparture; }
		}

		public int PageStart
		{
			get { return mvarPageStart; }
		}

		public string GroupTitle
		{
			get { return mvarGroupTitle; }
		}
	}

	/// <summary>Una semipágina del libro (mitad de A4 apaisado).</summary>
	public sealed class ItineraryBookHalfPage
	{
		private readonly ItineraryBookHalfKind mvarKind;
		private readonly int mvarPageNumber;
		private readonly int mvarPageCount;
		private readonly string mvarPlanName;
		private readonly string mvarNotes;
		private readonly string mvarDayLabel;
		private readonly string mvarEditionLabel;
		private readonly int mvarTrainCount;
		private readonly int mvarGroupCount;
		private readonly IReadOnlyList<ItineraryIndexEntry> mcolIndexLines;
		private readonly int mvarIndexPart;
		private readonly int mvarIndexParts;
		private readonly CirculationSheetDocument? mvarTrainDoc;
		private readonly CirculationSheetPage? mvarTrainPage;

		private ItineraryBookHalfPage(
			ItineraryBookHalfKind kind,
			int pageNumber,
			int pageCount,
			string planName,
			string notes,
			string dayLabel,
			string editionLabel,
			int trainCount,
			int groupCount,
			IReadOnlyList<ItineraryIndexEntry> indexLines,
			int indexPart,
			int indexParts,
			CirculationSheetDocument? trainDoc,
			CirculationSheetPage? trainPage)
		{
			mvarKind = kind;
			mvarPageNumber = pageNumber;
			mvarPageCount = pageCount;
			mvarPlanName = planName;
			mvarNotes = notes;
			mvarDayLabel = dayLabel;
			mvarEditionLabel = editionLabel;
			mvarTrainCount = trainCount;
			mvarGroupCount = groupCount;
			mcolIndexLines = indexLines;
			mvarIndexPart = indexPart;
			mvarIndexParts = indexParts;
			mvarTrainDoc = trainDoc;
			mvarTrainPage = trainPage;
		}

		public static ItineraryBookHalfPage Cover(
			int pageNumber,
			int pageCount,
			string planName,
			string notes,
			string dayLabel,
			string editionLabel,
			int trainCount,
			int groupCount)
		{
			return new ItineraryBookHalfPage(
				ItineraryBookHalfKind.Cover,
				pageNumber,
				pageCount,
				planName,
				notes,
				dayLabel,
				editionLabel,
				trainCount,
				groupCount,
				Array.Empty<ItineraryIndexEntry>(),
				0,
				0,
				null,
				null);
		}

		public static ItineraryBookHalfPage Index(
			int pageNumber,
			int pageCount,
			IReadOnlyList<ItineraryIndexEntry> lines,
			int part,
			int parts)
		{
			return new ItineraryBookHalfPage(
				ItineraryBookHalfKind.Index,
				pageNumber,
				pageCount,
				string.Empty,
				string.Empty,
				string.Empty,
				string.Empty,
				0,
				0,
				lines,
				part,
				parts,
				null,
				null);
		}

		public static ItineraryBookHalfPage TrainTable(
			int pageNumber,
			int pageCount,
			CirculationSheetDocument document,
			CirculationSheetPage page)
		{
			return new ItineraryBookHalfPage(
				ItineraryBookHalfKind.TrainTable,
				pageNumber,
				pageCount,
				string.Empty,
				string.Empty,
				string.Empty,
				string.Empty,
				0,
				0,
				Array.Empty<ItineraryIndexEntry>(),
				0,
				0,
				document,
				page);
		}

		public ItineraryBookHalfKind Kind
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

		public string PlanName
		{
			get { return mvarPlanName; }
		}

		public string Notes
		{
			get { return mvarNotes; }
		}

		public string DayLabel
		{
			get { return mvarDayLabel; }
		}

		public string EditionLabel
		{
			get { return mvarEditionLabel; }
		}

		public int TrainCount
		{
			get { return mvarTrainCount; }
		}

		public int GroupCount
		{
			get { return mvarGroupCount; }
		}

		public IReadOnlyList<ItineraryIndexEntry> IndexLines
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

		public CirculationSheetDocument? TrainDocument
		{
			get { return mvarTrainDoc; }
		}

		public CirculationSheetPage? TrainPage
		{
			get { return mvarTrainPage; }
		}
	}

	public enum ItineraryBookHalfKind
	{
		Cover = 0,
		Index = 1,
		TrainTable = 2
	}
}

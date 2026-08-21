using System.Globalization;
using Diamond.Project;
using Tourmaline26.Services.Catalog;
using Tourmaline26.Services.SfmInfo;

namespace Tourmaline26.Services.Correspondence
{
	/// <summary>
	/// Compone la lista de enlaces de una estación: trenes SFM + buses TIB/EMT,
	/// ordenados por hora de salida, sin expediciones ya pasadas.
	/// No se anuncian servicios (tren o bus) con menos de 5 minutos de margen.
	/// Tampoco se anuncian servicios que terminen en la estación anunciada
	/// ni en el destino de este tren (no son un enlace útil).
	/// </summary>
	public sealed class CorrespondenceBoardService : IDisposable
	{
		private static readonly TimeSpan MinimumLeadTime = TimeSpan.FromMinutes(5);

		private readonly SfmDeparturesService mvarSfm;
		private readonly TibDeparturesService mvarTib;
		private readonly EmtDeparturesService mvarEmt;
		private readonly PlacesCatalog mvarPlaces;
		private readonly ILogger<CorrespondenceBoardService> mvarLogger;
		private readonly object mvarLock = new();

		private string? mvarStationName;
		private StationInfo? mvarStationHint;
		private string? mvarExcludeDestination;
		private int mvarMaxDepartures = 10;
		private Place? mvarPlace;
		private SfmStation? mvarSfmStation;
		private IReadOnlyList<ConnectionDeparture> mcolUpcoming = Array.Empty<ConnectionDeparture>();

		public event EventHandler? Updated;

		public CorrespondenceBoardService(
			SfmDeparturesService sfm,
			TibDeparturesService tib,
			EmtDeparturesService emt,
			PlacesCatalog places,
			ILogger<CorrespondenceBoardService> logger)
		{
			mvarSfm = sfm;
			mvarTib = tib;
			mvarEmt = emt;
			mvarPlaces = places;
			mvarLogger = logger;
			mvarSfm.Updated += OnSourceUpdated;
			mvarTib.Updated += OnSourceUpdated;
			mvarEmt.Updated += OnSourceUpdated;
		}

		public Place? CurrentPlace
		{
			get { lock (mvarLock) return mvarPlace; }
		}

		public string DisplayStationName
		{
			get
			{
				lock (mvarLock)
				{
					if (mvarPlace is not null && !string.IsNullOrWhiteSpace(mvarPlace.Names.Tft))
						return mvarPlace.Names.Tft;
					if (mvarSfmStation is not null && !string.IsNullOrWhiteSpace(mvarSfmStation.Name))
						return mvarSfmStation.Name;
					return mvarStationName?.Trim() ?? string.Empty;
				}
			}
		}

		public IReadOnlyList<ConnectionDeparture> Upcoming
		{
			get { lock (mvarLock) return mcolUpcoming; }
		}

		/// <summary>Buses (TIB o EMT) que caben en la tabla anunciada.</summary>
		public int AnnouncedBusCount
		{
			get
			{
				lock (mvarLock)
				{
					int n = 0;
					foreach (ConnectionDeparture row in mcolUpcoming)
					{
						if (row.IsBus)
							n++;
					}
					return n;
				}
			}
		}

		public bool IsSfmConnected => mvarSfm.IsConnected;

		public string CombinedError
		{
			get
			{
				var parts = new List<string>(3);
				if (!string.IsNullOrEmpty(mvarSfm.LastError))
					parts.Add(mvarSfm.LastError);
				if (!string.IsNullOrEmpty(mvarTib.LastError))
					parts.Add(mvarTib.LastError);
				if (!string.IsNullOrEmpty(mvarEmt.LastError))
					parts.Add(mvarEmt.LastError);
				return string.Join(" / ", parts);
			}
		}

		/// <summary>
		/// Fija la estación anunciada y, opcionalmente, un destino a excluir
		/// (normalmente el destino de este tren). Se omiten trenes y buses
		/// que terminen en esa estación o en ese destino.
		/// </summary>
		public void SetContext(string? stationName, string? excludeDestination)
		{
			SetContext(stationName, excludeDestination, maxDepartures: null, stationHint: null);
		}

		public void SetContext(string? stationName, string? excludeDestination, int maxDepartures)
		{
			SetContext(stationName, excludeDestination, maxDepartures, stationHint: null);
		}

		public void SetContext(StationInfo? station, string? excludeDestination)
		{
			SetContext(station?.Name, excludeDestination, maxDepartures: null, station);
		}

		public void SetContext(StationInfo? station, string? excludeDestination, int maxDepartures)
		{
			SetContext(station?.Name, excludeDestination, maxDepartures, station);
		}

		private void SetContext(
			string? stationName,
			string? excludeDestination,
			int? maxDepartures,
			StationInfo? stationHint)
		{
			bool resubscribe;
			lock (mvarLock)
			{
				resubscribe = !string.Equals(mvarStationName, stationName, StringComparison.Ordinal)
					|| !string.Equals(mvarStationHint?.Id, stationHint?.Id, StringComparison.Ordinal)
					|| !string.Equals(mvarStationHint?.Avr, stationHint?.Avr, StringComparison.OrdinalIgnoreCase);
				mvarStationName = stationName;
				mvarStationHint = stationHint;
				mvarExcludeDestination = excludeDestination;
				if (maxDepartures.HasValue && maxDepartures.Value > 0)
					mvarMaxDepartures = maxDepartures.Value;
			}

			if (resubscribe)
				ApplySubscriptions();
			Rebuild();
		}

		public void Dispose()
		{
			mvarSfm.Updated -= OnSourceUpdated;
			mvarTib.Updated -= OnSourceUpdated;
			mvarEmt.Updated -= OnSourceUpdated;
		}

		private void OnSourceUpdated(object? sender, EventArgs e)
		{
			Rebuild();
			RaiseUpdated();
		}

		private void ApplySubscriptions()
		{
			string? stationName;
			StationInfo? hint;
			lock (mvarLock)
			{
				stationName = mvarStationName;
				hint = mvarStationHint;
			}

			Place? place = ResolvePlace(stationName, hint);
			SfmStation? sfmStation = ResolveSfmStation(stationName, place);

			if (place is null && sfmStation is not null)
				place = mvarPlaces.FindBySfmCode(sfmStation.Code);

			lock (mvarLock)
			{
				mvarPlace = place;
				mvarSfmStation = sfmStation;
			}

			int? sfmCode = place?.SfmCode ?? sfmStation?.Code;
			if (sfmCode is int code && code > 0)
				mvarSfm.SetStation(code);

			IReadOnlyList<TibStopRef> tibStops = place?.CorrespondenceStops
				?? (IReadOnlyList<TibStopRef>)Array.Empty<TibStopRef>();
			IReadOnlyList<TibStopRef> emtStops = place?.EmtStops
				?? (IReadOnlyList<TibStopRef>)Array.Empty<TibStopRef>();
			mvarTib.SetStops(tibStops.Select(s => s.Code));
			mvarEmt.SetStops(emtStops.Select(s => s.Code));

			if (place is null)
			{
				mvarLogger.LogInformation(
					"CorrespondenceBoard: sin lugar de catálogo para '{Station}' (solo trenes SFM).",
					stationName);
			}
			else
			{
				mvarLogger.LogInformation(
					"CorrespondenceBoard: {Place} → TIB {Tib}  EMT {Emt}",
					place.Id,
					tibStops.Count == 0 ? "—" : string.Join(",", tibStops.Select(s => s.Code)),
					emtStops.Count == 0 ? "—" : string.Join(",", emtStops.Select(s => s.Code)));
			}
		}

		private void Rebuild()
		{
			Place? place;
			SfmStation? sfmStation;
			string? exclude;
			string? stationName;
			int take;
			lock (mvarLock)
			{
				place = mvarPlace;
				sfmStation = mvarSfmStation;
				exclude = mvarExcludeDestination;
				stationName = mvarStationName;
				take = mvarMaxDepartures;
			}

			DateTime now = DateTime.Now;
			var rows = new List<ConnectionDeparture>();

			if (sfmStation is null || mvarSfm.Snapshot.StationCode == sfmStation.Code)
			{
				foreach (SfmDeparture dep in mvarSfm.Departures)
				{
					ConnectionDeparture row = MapTrain(dep);
					if (!IsUsable(row, now, exclude, place, stationName))
						continue;
					rows.Add(row);
				}
			}

			int rawBuses = 0;
			if (place is not null)
			{
				foreach (TibStopRef stop in place.CorrespondenceStops)
				{
					IReadOnlyList<TibDeparture> buses = mvarTib.GetDepartures(stop.Code);
					rawBuses += buses.Count;
					foreach (TibDeparture bus in buses)
					{
						if (!LineAllowed(stop, bus.LineCode))
							continue;

						ConnectionDeparture row = MapTibBus(bus, stop);
						if (!IsUsable(row, now, exclude, place, stationName))
							continue;
						rows.Add(row);
					}
				}

				foreach (TibStopRef stop in place.EmtStops)
				{
					IReadOnlyList<EmtDeparture> buses = mvarEmt.GetDepartures(stop.Code);
					rawBuses += buses.Count;
					foreach (EmtDeparture bus in buses)
					{
						if (!LineAllowed(stop, bus.LineCode))
							continue;

						ConnectionDeparture row = MapEmtBus(bus);
						if (!IsUsable(row, now, exclude, place, stationName))
							continue;
						rows.Add(row);
					}
				}
			}

			IReadOnlyList<ConnectionDeparture> upcoming = SelectUpcoming(rows, take);

			if (rawBuses > 0 && upcoming.All(d => !d.IsBus))
			{
				mvarLogger.LogInformation(
					"CorrespondenceBoard: buses devolvieron {Raw} salidas pero ninguna pasó el filtro (lugar={Place}).",
					rawBuses,
					place?.Id ?? "—");
			}

			lock (mvarLock)
				mcolUpcoming = upcoming;
		}

		private ConnectionDeparture MapTrain(SfmDeparture dep)
		{
			string dest = ResolveDisplayName(dep.DestinationCode, dep.DestinationName);
			DestLook look = LookupDestination(dest, keepOriginalName: false);
			string? notice = PrimaryNotice(dep);
			return new ConnectionDeparture
			{
				Mode = ConnectionMode.Train,
				DepartureTimeLocal = dep.DepartureTimeLocal,
				EstimatedTimeLocal = dep.EstimatedTimeLocal,
				LineSymbol = string.IsNullOrWhiteSpace(dep.LineSymbol) ? "—" : dep.LineSymbol,
				LineColorHex = string.IsNullOrWhiteSpace(dep.LineColorHex) ? "#004F8D" : dep.LineColorHex,
				DestinationName = dest,
				DestinationIcon = look.Icon,
				DestinationIconReplacesText = look.ReplaceText,
				ServiceName = dep.ServiceName,
				TripId = dep.ServicePlanCode,
				Platform = dep.Platform,
				OriginalPlatform = dep.OriginalPlatform,
				PlatformChanged = dep.PlatformChanged,
				Notice = notice
			};
		}

		private ConnectionDeparture MapTibBus(TibDeparture dep, TibStopRef stop)
		{
			DestLook look = LookupDestination(dep.DestinationName, keepOriginalName: true);
			return new ConnectionDeparture
			{
				Mode = ConnectionMode.Bus,
				DepartureTimeLocal = dep.DepartureTimeLocal,
				EstimatedTimeLocal = DateTime.MinValue,
				LineSymbol = string.IsNullOrWhiteSpace(dep.LineCode) ? "—" : dep.LineCode,
				LineColorHex = dep.LineColorHex,
				DestinationName = look.Name,
				DestinationIcon = look.Icon,
				DestinationIconReplacesText = look.ReplaceText,
				ServiceName = dep.TripId.ToString(CultureInfo.InvariantCulture),
				TripId = dep.TripId,
				Platform = stop.BayFor(dep.LineCode),
				OriginalPlatform = null,
				PlatformChanged = false,
				Notice = null
			};
		}

		private ConnectionDeparture MapEmtBus(EmtDeparture dep)
		{
			DestLook look = LookupDestination(dep.DestinationName, keepOriginalName: true);
			return new ConnectionDeparture
			{
				Mode = ConnectionMode.Emt,
				DepartureTimeLocal = dep.EstimatedTimeLocal,
				EstimatedTimeLocal = DateTime.MinValue,
				LineSymbol = string.IsNullOrWhiteSpace(dep.LineCode) ? "—" : dep.LineCode,
				LineColorHex = dep.LineColorHex,
				DestinationName = look.Name,
				DestinationIcon = look.Icon,
				DestinationIconReplacesText = look.ReplaceText,
				ServiceName = string.Concat(dep.StopCode, ":", dep.LineCode),
				TripId = 0,
				Platform = null,
				OriginalPlatform = null,
				PlatformChanged = false,
				Notice = null
			};
		}

		private DestLook LookupDestination(string? dest, bool keepOriginalName)
		{
			string raw = PlaceNameText.CleanTransitHeadsign((dest ?? string.Empty).Trim());
			Place? destPlace = mvarPlaces.FindByTibName(raw);
			if (destPlace is null)
			{
				Place? fuzzy = mvarPlaces.FindByDisplayName(raw);
				// Un bus a Consell/Alaró no es la estación SFM Consell-Alaró.
				if (fuzzy is not null && !(keepOriginalName && fuzzy.Kind == "rail"))
					destPlace = fuzzy;
			}

			string name;
			if (keepOriginalName)
			{
				if (destPlace is not null
					&& destPlace.Kind != "rail"
					&& !string.IsNullOrWhiteSpace(destPlace.Names.Tft))
				{
					name = destPlace.Names.Tft;
				}
				else
				{
					name = raw;
				}
			}
			else if (destPlace is not null && !string.IsNullOrWhiteSpace(destPlace.Names.Tft))
			{
				name = destPlace.Names.Tft;
			}
			else
			{
				name = raw;
			}

			string? icon = destPlace is not null && destPlace.Names.Icon.Length > 0
				? destPlace.Names.Icon
				: null;
			bool replace = destPlace is not null && destPlace.Names.IconMode == PlaceIconMode.Replace;

			if (PlaceNameText.IsAirportWord(raw))
			{
				icon ??= "Airport";
				replace = false;
				if (keepOriginalName)
					name = raw;
			}
			else if (PlaceNameText.IsPortWord(raw))
			{
				icon ??= "Ferry";
				replace = false;
				if (keepOriginalName)
					name = raw;
			}

			return new DestLook(name, icon, replace);
		}

		private readonly record struct DestLook(string Name, string? Icon, bool ReplaceText);

		private static bool LineAllowed(TibStopRef stop, string lineCode)
		{
			if (stop.Lines.Count == 0)
				return true;
			if (stop.Lines.Contains(lineCode, StringComparer.OrdinalIgnoreCase))
				return true;

			string compact = CompactLine(lineCode);
			foreach (string allowed in stop.Lines)
			{
				if (string.Equals(CompactLine(allowed), compact, StringComparison.OrdinalIgnoreCase))
					return true;
			}
			return false;
		}

		private static string CompactLine(string? line)
		{
			if (string.IsNullOrWhiteSpace(line))
				return string.Empty;
			string s = line.Trim();
			if (s.StartsWith("L", StringComparison.OrdinalIgnoreCase) && s.Length > 1)
				s = s[1..];
			return s;
		}

		private string ResolveDisplayName(int sfmCode, string fallback)
		{
			Place? bySfm = sfmCode > 0 ? mvarPlaces.FindBySfmCode(sfmCode) : null;
			if (bySfm is not null && !string.IsNullOrWhiteSpace(bySfm.Names.Tft))
				return bySfm.Names.Tft;
			Place? byName = mvarPlaces.FindByDisplayName(fallback);
			if (byName is not null && !string.IsNullOrWhiteSpace(byName.Names.Tft))
				return byName.Names.Tft;
			return fallback ?? string.Empty;
		}

		private static IReadOnlyList<ConnectionDeparture> SelectUpcoming(
			List<ConnectionDeparture> rows,
			int take)
		{
			List<ConnectionDeparture> sorted = rows
				.GroupBy(DedupKey, StringComparer.Ordinal)
				.Select(g => g.First())
				.OrderBy(d => d)
				.ToList();

			if (take <= 0 || sorted.Count <= take)
				return sorted;

			List<ConnectionDeparture> taken = sorted.Take(take).ToList();
			bool hasBus = taken.Exists(d => d.IsBus);
			bool anyBus = sorted.Exists(d => d.IsBus);
			if (hasBus || !anyBus)
				return taken;

			int busSlots = Math.Min(
				sorted.Count(d => d.IsBus),
				Math.Max(1, take / 3));
			List<ConnectionDeparture> buses = sorted
				.Where(d => d.IsBus)
				.Take(busSlots)
				.ToList();
			int trainSlots = take - buses.Count;
			return sorted
				.Where(d => d.Mode == ConnectionMode.Train)
				.Take(trainSlots)
				.Concat(buses)
				.OrderBy(d => d)
				.ToList();
		}

		private bool IsUsable(
			ConnectionDeparture row,
			DateTime now,
			string? excludeDestination,
			Place? place,
			string? stationName)
		{
			if (row.SortTime == default || row.SortTime == DateTime.MinValue)
				return false;

			DateTime local = ToLocal(row.SortTime);
			// Solo salidas a 5 minutos o más: no da tiempo a coger las más inmediatas.
			if (local < now + MinimumLeadTime)
				return false;

			if (TerminatesHere(row.DestinationName, excludeDestination, place, stationName))
				return false;
			return true;
		}

		/// <summary>
		/// True si el servicio termina en el destino de este tren o en la
		/// estación a la que nos dirigimos (p. ej. un TIB que acaba en Manacor).
		/// </summary>
		private bool TerminatesHere(
			string destination,
			string? excludeDestination,
			Place? place,
			string? stationName)
		{
			if (SameDestination(destination, excludeDestination))
				return true;
			if (SameDestination(destination, stationName))
				return true;
			if (place is null)
				return false;

			string cleaned = PlaceNameText.CleanTransitHeadsign(destination);
			Place? destPlace = mvarPlaces.FindByTibName(cleaned)
				?? mvarPlaces.FindByDisplayName(cleaned);
			if (destPlace is not null)
			{
				if (destPlace.Kind == "bus" && place.Kind == "rail")
					return false;
				return string.Equals(destPlace.Id, place.Id, StringComparison.OrdinalIgnoreCase);
			}

			string destNorm = PlaceNameText.Normalize(cleaned);
			if (destNorm.Length == 0)
				return false;
			return destNorm == PlaceNameText.Normalize(place.Names.Canonical)
				|| destNorm == PlaceNameText.Normalize(place.Names.Tft)
				|| destNorm == PlaceNameText.Normalize(place.Names.Teleindicator)
				|| destNorm == PlaceNameText.Normalize(place.TibName);
		}

		private static DateTime ToLocal(DateTime value)
		{
			return value.Kind switch
			{
				DateTimeKind.Utc => value.ToLocalTime(),
				DateTimeKind.Local => value,
				_ => DateTime.SpecifyKind(value, DateTimeKind.Local)
			};
		}

		private static string DedupKey(ConnectionDeparture row)
		{
			if (row.Mode == ConnectionMode.Bus && row.TripId != 0)
				return "B:" + row.TripId.ToString(CultureInfo.InvariantCulture);
			if (row.Mode == ConnectionMode.Emt)
			{
				return string.Concat(
					"E:",
					row.ServiceName,
					":",
					row.DestinationName,
					":",
					row.SortTime.ToString("HH:mm", CultureInfo.InvariantCulture));
			}
			return string.Concat(
				"T:",
				row.ServiceName,
				":",
				row.SortTime.ToString("o", CultureInfo.InvariantCulture),
				":",
				row.DestinationName);
		}

		private Place? ResolvePlace(string? stationName, StationInfo? hint)
		{
			if (hint is not null)
			{
				Place? keyed = mvarPlaces.Find(hint);
				if (keyed is not null)
					return keyed;
			}

			if (string.IsNullOrWhiteSpace(stationName))
				return null;
			return mvarPlaces.FindByDiamondId(stationName)
				?? mvarPlaces.FindByAvr(stationName)
				?? mvarPlaces.FindById(stationName)
				?? mvarPlaces.FindByDisplayName(stationName);
		}

		private SfmStation? ResolveSfmStation(string? stationName, Place? place)
		{
			if (place?.SfmCode is int code && code > 0)
			{
				SfmStation? byCode = mvarSfm.FindStation(code);
				if (byCode is not null)
					return byCode;
			}

			if (string.IsNullOrWhiteSpace(stationName))
				return null;

			SfmStation? direct = mvarSfm.FindStationByName(stationName);
			if (direct is not null)
				return direct;

			string needle = PlaceNameText.Normalize(stationName);
			if (needle.Length < 2)
				return null;

			SfmStation? best = null;
			int bestScore = 0;
			foreach (SfmStation s in mvarSfm.Stations)
			{
				string cand = PlaceNameText.Normalize(s.Name);
				string abbr = PlaceNameText.Normalize(s.Abbreviation);
				int score = 0;
				if (cand == needle || abbr == needle)
					score = 100;
				else if (PlaceNameText.SameDistinctiveTokens(cand, needle))
					score = 95;

				if (score > bestScore)
				{
					bestScore = score;
					best = s;
				}
			}

			return bestScore >= 95 ? best : null;
		}

		private static string? PrimaryNotice(SfmDeparture dep)
		{
			SfmLocalizedText? pick =
				dep.InfoMessages.FirstOrDefault(m => m.LanguageCode == 600) ??
				dep.InfoMessages.FirstOrDefault(m => m.LanguageCode == 601) ??
				dep.InfoMessages.FirstOrDefault();
			return string.IsNullOrWhiteSpace(pick?.Text) ? null : pick!.Text.Trim();
		}

		private bool SameDestination(string? a, string? b)
		{
			if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
				return false;

			Place? pa = mvarPlaces.FindByDisplayName(a);
			Place? pb = mvarPlaces.FindByDisplayName(b);
			if (pa is not null && pb is not null)
				return string.Equals(pa.Id, pb.Id, StringComparison.OrdinalIgnoreCase);

			return PlaceNameText.Normalize(a) == PlaceNameText.Normalize(b);
		}

		private void RaiseUpdated()
		{
			try
			{
				Updated?.Invoke(this, EventArgs.Empty);
			}
			catch (Exception ex)
			{
				mvarLogger.LogDebug(ex, "CorrespondenceBoard: error en suscriptor Updated.");
			}
		}
	}
}

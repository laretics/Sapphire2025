using System;
using System.Collections.Generic;
using Diamond.Timed;
using Diamond.Topo;
using ProjectCirculation = Diamond.Project.Circulation;
using ProjectModel = Diamond.Project.Project;
using ProjectAsimilation = Diamond.Project.Asimilation;
using ProjectTimedCall = Diamond.Project.TimedCall;
using PublishedProjectPackage = Diamond.Project.PublishedProjectPackage;
using PublishedProjectHydrator = Diamond.Project.PublishedProjectHydrator;

namespace Diamond.Cabin
{
	/// <summary>
	/// Estado mínimo de misión y topología para un cliente de cabina (p. ej. Tourmaline).
	/// Sustituye el antiguo TimeNetEnvironment: topo + plan publicado del día + circulación.
	/// </summary>
	public sealed class CabinEnvironment
	{
		private long mvarPreviousPkLocation = -1;
		private long mvarPk;
		private long mvarStationArea = CabinItinerary.DefaultStationAreaMeters;
		private readonly HashSet<string> mcolLeftStationIds =
			new HashSet<string>(StringComparer.Ordinal);
		private ProjectCirculation? mvarCirculation;

		public CabinEnvironment()
		{
			LinearLocation = new LinearLocation();
			ClockNow = DateTime.Now;
		}

		/// <summary>Topología cargada (caché local).</summary>
		public TopoLayout? Topo { get; set; }

		/// <summary>Guid del documento de topología en el almacén Sapphire.</summary>
		public Guid TopoDocumentId { get; set; }

		/// <summary>ContentHash del XML de topo en caché (comparación con servidor).</summary>
		public string TopoContentHash { get; set; } = string.Empty;

		/// <summary>Paquete publicado activo (todos los días).</summary>
		public PublishedProjectPackage? PublishedPackage { get; set; }

		/// <summary>Metadatos del plan publicado seleccionado (id, vigencia…).</summary>
		public Guid PublishedPlanId { get; set; }

		public string PublishedPlanName { get; set; } = string.Empty;

		public DateTime? PublishedValidFrom { get; set; }

		public DateTime? PublishedValidTo { get; set; }

		/// <summary>Proyecto materializado del día de malla efectivo.</summary>
		public ProjectModel? DayProject { get; private set; }

		/// <summary>
		/// Día de malla forzado desde la UI. <c>null</c> = seguir el calendario de <see cref="ClockNow"/>.
		/// </summary>
		private DayOfWeek? mvarPlanningDayOverride;

		/// <summary>Día de malla que se materializa (override o día civil del reloj).</summary>
		public DayOfWeek EffectivePlanningDay
		{
			get { return mvarPlanningDayOverride ?? ClockNow.DayOfWeek; }
		}

		/// <summary><c>true</c> si el día de malla no es el del calendario.</summary>
		public bool HasPlanningDayOverride
		{
			get
			{
				return mvarPlanningDayOverride.HasValue
					&& mvarPlanningDayOverride.Value != ClockNow.DayOfWeek;
			}
		}

		public LinearLocation LinearLocation { get; }

		public Axis? Axis
		{
			get { return LinearLocation.Axis; }
		}

		public Diamond.Project.StationInfo? CurrentStation { get; private set; }

		public long StationAreaMeters
		{
			get { return mvarStationArea; }
			set
			{
				mvarStationArea = value < 0 ? 0 : value;
				RefreshCurrentStation();
			}
		}

		public long PK
		{
			get { return mvarPk; }
			set
			{
				mvarPk = value;
				OnPkChanged();
			}
		}

		public bool PKIncreasing { get; private set; } = true;

		public TimeSpan CurrentDelay { get; set; } = TimeSpan.Zero;

		/// <summary>Reloj de referencia (sincronizable; por defecto local).</summary>
		public DateTime ClockNow { get; set; }

		public ProjectCirculation? Circulation
		{
			get { return mvarCirculation; }
			set
			{
				mvarCirculation = value;
				mcolLeftStationIds.Clear();
				UpdateMissionAxes();
				RefreshCurrentStation();
			}
		}

		public ProjectAsimilation? Asimilation
		{
			get
			{
				return mvarCirculation is null ? null : mvarCirculation.Asimilation;
			}
		}

		/// <summary>
		/// Paradas comerciales residuales desde el PK actual (sin incluir la actual si ya se abandonó).
		/// </summary>
		public IReadOnlyList<ProjectTimedCall> RemainingCalls
		{
			get
			{
				if (mvarCirculation is null)
				{
					return Array.Empty<ProjectTimedCall>();
				}

				return CabinItinerary.RemainingCommercialCalls(
					mvarCirculation,
					mvarPk,
					includeCurrentStation: false);
			}
		}

		/// <summary>
		/// Carga el proyecto del día según <see cref="EffectivePlanningDay"/> y el paquete publicado.
		/// </summary>
		public bool RefreshDayProject()
		{
			if (PublishedPackage is null)
			{
				DayProject = null;
				return false;
			}

			DayProject = PublishedProjectHydrator.DayToProject(PublishedPackage, EffectivePlanningDay);
			if (mvarCirculation is not null)
			{
				ProjectCirculation? match = DayProject is null
					? null
					: FindCirculationById(DayProject, mvarCirculation.Id);
				if (match is not null)
				{
					mvarCirculation = match;
					UpdateMissionAxes();
				}
				else
				{
					Circulation = null;
				}
			}

			return DayProject is not null;
		}

		/// <summary>
		/// Elige el día de malla. Si coincide con el calendario, se deja de forzar
		/// (medianoche volverá a cambiar de día sola).
		/// </summary>
		public bool SelectPlanningDay(DayOfWeek day)
		{
			if (day == ClockNow.DayOfWeek)
			{
				mvarPlanningDayOverride = null;
			}
			else
			{
				mvarPlanningDayOverride = day;
			}

			return RefreshDayProject();
		}

		/// <summary>
		/// Asigna topo + paquete y materializa el día.
		/// </summary>
		public void Load(
			TopoLayout? topo,
			Guid topoDocumentId,
			string topoContentHash,
			PublishedProjectPackage? package,
			Guid publishedPlanId,
			string publishedPlanName,
			DateTime? validFrom,
			DateTime? validTo)
		{
			Topo = topo;
			TopoDocumentId = topoDocumentId;
			TopoContentHash = topoContentHash ?? string.Empty;
			PublishedPackage = package;
			PublishedPlanId = publishedPlanId;
			PublishedPlanName = publishedPlanName ?? string.Empty;
			PublishedValidFrom = validFrom;
			PublishedValidTo = validTo;
			if (topo is not null)
			{
				// Misma infraestructura que el planificador / publicación:
				// cantones en estaciones principales y doble vía Palma–Enllaç en T3.
				// El XML de topo no serializa spans de vía; sin esto la hoja de
				// circulación del maquinista marca todo el recorrido como vía única.
				SfmDemoInfrastructure.Apply(topo);
			}

			RefreshDayProject();
		}

		public void LeaveCurrentStation()
		{
			if (CurrentStation is not null && CurrentStation.Id.Length > 0)
			{
				mcolLeftStationIds.Add(CurrentStation.Id);
			}

			RefreshCurrentStation();
		}

		/// <summary>
		/// Olvida las estaciones ya abandonadas (p. ej. al reiniciar un simulador de ruta).
		/// </summary>
		public void ResetStationProgress()
		{
			mcolLeftStationIds.Clear();
			RefreshCurrentStation();
		}

		/// <summary>
		/// Actualiza PK desde la localización lineal (p. ej. tras GPS).
		/// </summary>
		public void ApplyLinearLocation()
		{
			if (LinearLocation.Axis is not null && LinearLocation.PKRef >= 0)
			{
				PK = LinearLocation.PKRef;
			}
		}

		/// <summary>
		/// Actualiza el PK de ruta por odómetro MVB (sin GPS).
		/// </summary>
		public void ApplyOdometerPk(long pk)
		{
			LinearLocation.SetOdometer(pk);
			PK = pk;
		}

		public IReadOnlyList<ProjectCirculation> SearchCirculations(string? query)
		{
			if (DayProject is null || string.IsNullOrWhiteSpace(query))
			{
				return Array.Empty<ProjectCirculation>();
			}

			string normalized = query.Replace(' ', ',')
				.Replace('-', ',')
				.Replace('.', ',')
				.Trim()
				.ToUpperInvariant();
			string[] tokens = normalized.Split(
				',',
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

			HashSet<ProjectCirculation> byName = new HashSet<ProjectCirculation>();
			HashSet<ProjectCirculation> byTime = new HashSet<ProjectCirculation>();
			HashSet<ProjectCirculation> byDestination = new HashSet<ProjectCirculation>();

			int t = 0;
			while (t < tokens.Length)
			{
				string token = tokens[t];
				TimeSpan hour;
				bool porHora = TimeSpan.TryParse(token, out hour);

				int c = 0;
				while (c < DayProject.Circulations.Count)
				{
					ProjectCirculation cir = DayProject.Circulations[c];
					string service = cir.HasServiceNumber ? cir.ServiceNumber : cir.Id;
					if (service.ToUpperInvariant().Contains(token))
					{
						byName.Add(cir);
					}

					if (porHora
						&& Math.Abs((cir.Departure - hour).TotalMinutes) <= 20.0)
					{
						byTime.Add(cir);
					}

					string dest = cir.Destination.Name.ToUpperInvariant();
					string destAvr = cir.Destination.Avr.ToUpperInvariant();
					if (dest.Contains(token) || destAvr.Contains(token))
					{
						byDestination.Add(cir);
					}

					c++;
				}

				t++;
			}

			// Coincidencias en ≥2 categorías tienen prioridad visual (como el menú TimeNet).
			List<ProjectCirculation> multi = new List<ProjectCirculation>();
			foreach (ProjectCirculation cir in byName)
			{
				int hits = 1;
				if (byTime.Contains(cir))
				{
					hits++;
				}

				if (byDestination.Contains(cir))
				{
					hits++;
				}

				if (hits >= 2)
				{
					multi.Add(cir);
				}
			}

			if (multi.Count > 0)
			{
				return multi;
			}

			List<ProjectCirculation> all = new List<ProjectCirculation>();
			foreach (ProjectCirculation cir in byName)
			{
				all.Add(cir);
			}

			foreach (ProjectCirculation cir in byTime)
			{
				if (!byName.Contains(cir))
				{
					all.Add(cir);
				}
			}

			foreach (ProjectCirculation cir in byDestination)
			{
				if (!byName.Contains(cir) && !byTime.Contains(cir))
				{
					all.Add(cir);
				}
			}

			return all;
		}

		public void SearchBuckets(
			string? query,
			out IReadOnlyList<ProjectCirculation> byName,
			out IReadOnlyList<ProjectCirculation> byTime,
			out IReadOnlyList<ProjectCirculation> byDestination,
			out IReadOnlyList<ProjectCirculation> byLocation,
			out IReadOnlyList<ProjectCirculation> multiMatch)
		{
			List<ProjectCirculation> names = new List<ProjectCirculation>();
			List<ProjectCirculation> times = new List<ProjectCirculation>();
			List<ProjectCirculation> dests = new List<ProjectCirculation>();
			List<ProjectCirculation> locations = new List<ProjectCirculation>();
			List<ProjectCirculation> multi = new List<ProjectCirculation>();

			byName = names;
			byTime = times;
			byDestination = dests;
			byLocation = locations;
			multiMatch = multi;

			if (DayProject is null || string.IsNullOrWhiteSpace(query))
			{
				return;
			}

			string[] tokens = SplitSearchTokens(query);
			HashSet<ProjectCirculation> setName = new HashSet<ProjectCirculation>();
			HashSet<ProjectCirculation> setTime = new HashSet<ProjectCirculation>();
			HashSet<ProjectCirculation> setDest = new HashSet<ProjectCirculation>();
			HashSet<ProjectCirculation> setLoc = new HashSet<ProjectCirculation>();

			int t = 0;
			while (t < tokens.Length)
			{
				string token = tokens[t];
				TimeSpan hour;
				bool porHora = TryParseClock(token, out hour);
				int c = 0;
				while (c < DayProject.Circulations.Count)
				{
					ProjectCirculation cir = DayProject.Circulations[c];
					if (CirculationMatchesNumber(cir, token))
					{
						setName.Add(cir);
					}

					if (porHora
						&& Math.Abs((cir.Departure - hour).TotalMinutes) <= 20.0)
					{
						setTime.Add(cir);
					}

					if (StationMatches(cir.Destination, token))
					{
						setDest.Add(cir);
					}

					if (CirculationTouchesLocation(cir, token))
					{
						setLoc.Add(cir);
					}

					c++;
				}

				t++;
			}

			AddSortedByDeparture(names, setName);
			AddSortedByDeparture(times, setTime);
			AddSortedByDeparture(dests, setDest);
			AddSortedByDeparture(locations, setLoc);

			foreach (ProjectCirculation cir in setName)
			{
				int hits = 1;
				if (setTime.Contains(cir))
				{
					hits++;
				}

				if (setDest.Contains(cir) || setLoc.Contains(cir))
				{
					hits++;
				}

				if (hits >= 2)
				{
					multi.Add(cir);
				}
			}

			if (multi.Count > 1)
			{
				multi.Sort(CompareByDeparture);
			}
		}

		/// <summary>Circulaciones del día que coinciden con los números de tren de un turno.</summary>
		public IReadOnlyList<ProjectCirculation> CirculationsForShiftTokens(
			IReadOnlyList<string> tokens,
			string? query)
		{
			List<ProjectCirculation> salida = new List<ProjectCirculation>();
			if (DayProject is null || tokens is null || tokens.Count == 0)
			{
				return salida;
			}

			string[] queryTokens = SplitSearchTokens(query);
			int c = 0;
			while (c < DayProject.Circulations.Count)
			{
				ProjectCirculation cir = DayProject.Circulations[c];
				if (CirculationMatchesAnyShiftToken(cir, tokens)
					&& CirculationMatchesAllQueryTokens(cir, queryTokens))
				{
					salida.Add(cir);
				}

				c++;
			}

			salida.Sort(CompareByDeparture);
			return salida;
		}

		/// <summary>
		/// Hora civil: <c>8</c>, <c>8:30</c>, <c>08.30</c>, <c>830</c>, <c>0830</c>.
		/// No interpreta enteros sueltos como días de <see cref="TimeSpan"/>.
		/// </summary>
		public static bool TryParseClock(string? token, out TimeSpan time)
		{
			time = default;
			if (string.IsNullOrWhiteSpace(token))
			{
				return false;
			}

			string raw = token.Trim();
			string withColon = raw.Replace('.', ':');
			if (TimeSpan.TryParseExact(
				withColon,
				new[] { @"h\:mm", @"hh\:mm" },
				System.Globalization.CultureInfo.InvariantCulture,
				out TimeSpan parsed)
				&& parsed.TotalHours < 24.0
				&& parsed.Days == 0)
			{
				time = parsed;
				return true;
			}

			bool allDigits = raw.Length > 0;
			int di = 0;
			while (di < raw.Length)
			{
				if (!char.IsDigit(raw[di]))
				{
					allDigits = false;
					break;
				}

				di++;
			}

			if (!allDigits)
			{
				return false;
			}

			int hours;
			int minutes;
			if (raw.Length <= 2)
			{
				if (!int.TryParse(raw, out hours) || hours > 23)
				{
					return false;
				}

				minutes = 0;
			}
			else if (raw.Length == 3)
			{
				hours = raw[0] - '0';
				if (!int.TryParse(raw.Substring(1), out minutes))
				{
					return false;
				}
			}
			else if (raw.Length == 4)
			{
				if (!int.TryParse(raw.Substring(0, 2), out hours)
					|| !int.TryParse(raw.Substring(2, 2), out minutes))
				{
					return false;
				}
			}
			else
			{
				return false;
			}

			if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59)
			{
				return false;
			}

			time = new TimeSpan(hours, minutes, 0);
			return true;
		}

		private static string[] SplitSearchTokens(string? query)
		{
			if (string.IsNullOrWhiteSpace(query))
			{
				return Array.Empty<string>();
			}

			return query.Trim()
				.ToUpperInvariant()
				.Replace(',', ' ')
				.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		}

		private static bool CirculationMatchesNumber(ProjectCirculation cir, string token)
		{
			string service = cir.HasServiceNumber ? cir.ServiceNumber : cir.Id;
			if (service.ToUpperInvariant().Contains(token))
			{
				return true;
			}

			return cir.TechnicalId.Length > 0
				&& cir.TechnicalId.ToUpperInvariant().Contains(token);
		}

		private static bool StationMatches(Diamond.Project.StationInfo station, string token)
		{
			if (station.Name.ToUpperInvariant().Contains(token))
			{
				return true;
			}

			if (station.Avr.ToUpperInvariant().Contains(token))
			{
				return true;
			}

			return station.Id.ToUpperInvariant().Contains(token);
		}

		private static bool CirculationTouchesLocation(ProjectCirculation cir, string token)
		{
			if (StationMatches(cir.Origin, token) || StationMatches(cir.Destination, token))
			{
				return true;
			}

			int i = 0;
			while (i < cir.Calls.Count)
			{
				if (StationMatches(cir.Calls[i].Station, token))
				{
					return true;
				}

				i++;
			}

			return false;
		}

		private static bool CirculationMatchesAnyShiftToken(
			ProjectCirculation cir,
			IReadOnlyList<string> tokens)
		{
			int i = 0;
			while (i < tokens.Count)
			{
				string token = tokens[i].Trim().ToUpperInvariant();
				if (token.Length > 0 && CirculationMatchesNumber(cir, token))
				{
					return true;
				}

				i++;
			}

			return false;
		}

		private static bool CirculationMatchesAllQueryTokens(
			ProjectCirculation cir,
			string[] queryTokens)
		{
			if (queryTokens.Length == 0)
			{
				return true;
			}

			int t = 0;
			while (t < queryTokens.Length)
			{
				string token = queryTokens[t];
				TimeSpan hour;
				bool porHora = TryParseClock(token, out hour);
				bool ok = CirculationMatchesNumber(cir, token)
					|| CirculationTouchesLocation(cir, token)
					|| (porHora && Math.Abs((cir.Departure - hour).TotalMinutes) <= 20.0);
				if (!ok)
				{
					return false;
				}

				t++;
			}

			return true;
		}

		private static void AddSortedByDeparture(
			List<ProjectCirculation> target,
			HashSet<ProjectCirculation> source)
		{
			foreach (ProjectCirculation cir in source)
			{
				target.Add(cir);
			}

			if (target.Count > 1)
			{
				target.Sort(CompareByDeparture);
			}
		}

		private static int CompareByDeparture(ProjectCirculation a, ProjectCirculation b)
		{
			int cmp = a.Departure.CompareTo(b.Departure);
			if (cmp != 0)
			{
				return cmp;
			}

			string an = a.HasServiceNumber ? a.ServiceNumber : a.Id;
			string bn = b.HasServiceNumber ? b.ServiceNumber : b.Id;
			return string.Compare(an, bn, StringComparison.OrdinalIgnoreCase);
		}

		private void OnPkChanged()
		{
			if (mvarPreviousPkLocation < 0)
			{
				mvarPreviousPkLocation = mvarPk;
			}
			else if (Math.Abs(mvarPk - mvarPreviousPkLocation) > 500)
			{
				PKIncreasing = mvarPreviousPkLocation < mvarPk;
				mvarPreviousPkLocation = mvarPk;
			}

			RefreshCurrentStation();
		}

		private void RefreshCurrentStation()
		{
			CurrentStation = CabinItinerary.FindCurrentStation(
				mvarCirculation,
				mvarPk,
				mcolLeftStationIds,
				mvarStationArea);
		}

		private void UpdateMissionAxes()
		{
			if (Topo is null || mvarCirculation is null)
			{
				LinearLocation.MissionAxes = null;
				return;
			}

			IReadOnlyList<CabinItinerary.AxisRef> refs =
				CabinItinerary.ResolveMissionAxisIds(mvarCirculation.Asimilation.ViewId);
			if (refs.Count == 0)
			{
				LinearLocation.MissionAxes = null;
				return;
			}

			List<Axis> axes = new List<Axis>();
			int i = 0;
			while (i < refs.Count)
			{
				Axis? axis = Topo.FindAxisById(refs[i].Id);
				if (axis is not null)
				{
					axes.Add(axis);
				}

				i++;
			}

			LinearLocation.MissionAxes = axes.Count > 0 ? axes : null;
		}

		private static ProjectCirculation? FindCirculationById(ProjectModel project, string id)
		{
			int i = 0;
			while (i < project.Circulations.Count)
			{
				if (string.Equals(project.Circulations[i].Id, id, StringComparison.Ordinal))
				{
					return project.Circulations[i];
				}

				i++;
			}

			return null;
		}
	}
}

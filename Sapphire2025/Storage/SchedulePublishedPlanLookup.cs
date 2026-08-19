using System.IO.Compression;
using Diamond.Controls.Rendering;
using Diamond.Project;
using Diamond.Timed;
using Diamond.Topo;
using Sapphire2025Models;
using Sapphire2025Models.Diamond;
using ProjectCirculation = Diamond.Project.Circulation;

namespace Sapphire2025.Storage
{
	/// <summary>
	/// Carga el plan de explotación publicado que corresponde a un día civil
	/// (pasado, hoy o futuro) y localiza circulaciones por número de tren.
	/// </summary>
	public sealed class SchedulePublishedPlanLookup
	{
		private readonly DiamondClient mvarClient;
		private IReadOnlyList<DiamondPublishedPlanHeaderModel>? mcolHeaders;
		private readonly Dictionary<Guid, PublishedProjectPackage> mcolPackages = new();
		private readonly Dictionary<Guid, TopoLayout> mcolTopos = new();
		private readonly Dictionary<string, Mesh> mcolMeshes = new(StringComparer.Ordinal);

		public SchedulePublishedPlanLookup(DiamondClient client)
		{
			mvarClient = client ?? throw new ArgumentNullException(nameof(client));
		}

		/// <summary>
		/// Plan publicado aplicable a esa fecha + proyecto del día de la semana + topología.
		/// Null si no hay ningún plan publicado que se pueda usar.
		/// </summary>
		public async Task<SchedulePublishedDaySnapshot?> LoadDayAsync(
			DateTime date,
			CancellationToken cancellationToken = default)
		{
			DateTime day = date.Date;
			IReadOnlyList<DiamondPublishedPlanHeaderModel> headers = await EnsureHeadersAsync();
			DiamondPublishedPlanHeaderModel? header = PickPlanForDate(headers, day);
			if (header is null || Guid.Empty.Equals(header.Id))
			{
				return null;
			}

			PublishedProjectPackage? package = await EnsurePackageAsync(header);
			if (package is null)
			{
				return null;
			}

			TopoLayout? topo = await EnsureTopoAsync(header.TopoId, cancellationToken);
			Project? dayProject = PublishedProjectHydrator.DayToProject(package, day.DayOfWeek);
			return new SchedulePublishedDaySnapshot(header, package, dayProject, topo, day);
		}

		/// <summary>
		/// Busca la circulación del día seleccionado. Si no está en ese día de la semana,
		/// recorre el resto de días del mismo plan (festivos / mallas distintas).
		/// </summary>
		public static ScheduleSheetHit? FindCirculation(
			SchedulePublishedDaySnapshot? snapshot,
			string? trainId,
			TimeSpan startTime)
		{
			if (snapshot is null)
			{
				return null;
			}

			ProjectCirculation? sameDay = FindCirculationInProject(snapshot.DayProject, trainId, startTime);
			if (sameDay is not null)
			{
				return new ScheduleSheetHit(sameDay, snapshot.Date.DayOfWeek);
			}

			PublishedProjectPackage package = snapshot.Package;
			int i = 0;
			while (i < package.Days.Count)
			{
				if (package.Days[i].Day != snapshot.Date.DayOfWeek)
				{
					Project other = PublishedProjectHydrator.ToProject(package.Days[i]);
					ProjectCirculation? hit = FindCirculationInProject(other, trainId, startTime);
					if (hit is not null)
					{
						return new ScheduleSheetHit(hit, package.Days[i].Day);
					}
				}

				i++;
			}

			return null;
		}

		/// <summary>
		/// Malla Timed del día de la circulación (para cruces en Obs.).
		/// </summary>
		public Mesh? GetOrBuildMesh(SchedulePublishedDaySnapshot snapshot, DayOfWeek day)
		{
			if (snapshot is null || snapshot.Topo is null)
			{
				return null;
			}

			string key = snapshot.Header.Id.ToString("N") + "|" + ((int)day).ToString();
			if (mcolMeshes.TryGetValue(key, out Mesh? cached))
			{
				return cached;
			}

			Project? project = snapshot.DayProject;
			if (project is null || snapshot.Date.DayOfWeek != day)
			{
				project = PublishedProjectHydrator.DayToProject(snapshot.Package, day);
			}

			if (project is null)
			{
				return null;
			}

			Mesh mesh = CabinCirculationHydrator.ToMesh(project, snapshot.Topo);
			mcolMeshes[key] = mesh;
			return mesh;
		}

		/// <summary>
		/// Busca la circulación del día que corresponde al número de tren del turno.
		/// Si hay varias con el mismo número, se queda con la de salida más cercana.
		/// </summary>
		public static ProjectCirculation? FindCirculationInProject(
			Project? project,
			string? trainId,
			TimeSpan startTime)
		{
			if (project is null || string.IsNullOrWhiteSpace(trainId))
			{
				return null;
			}

			ProjectCirculation? best = null;
			double bestAbs = double.MaxValue;
			int i = 0;
			while (i < project.Circulations.Count)
			{
				ProjectCirculation circ = project.Circulations[i];
				if (MatchesTrainId(trainId, circ))
				{
					double abs = Math.Abs((circ.Departure - startTime).TotalMinutes);
					if (best is null || abs < bestAbs)
					{
						best = circ;
						bestAbs = abs;
					}
				}

				i++;
			}

			return best;
		}

		public static bool MatchesTrainId(string trainId, ProjectCirculation circulation)
		{
			if (string.IsNullOrWhiteSpace(trainId) || circulation is null)
			{
				return false;
			}

			string wanted = trainId.Trim();
			if (EqualsIgnoreCase(wanted, circulation.ServiceNumber)
				|| EqualsIgnoreCase(wanted, circulation.Id)
				|| EqualsIgnoreCase(wanted, circulation.TechnicalId))
			{
				return true;
			}

			int wantedNumber = Utils.ExtractCirculationNumber(wanted);
			if (wantedNumber <= 0)
			{
				return false;
			}

			if (circulation.HasServiceNumber
				&& Utils.ExtractCirculationNumber(circulation.ServiceNumber) == wantedNumber)
			{
				return true;
			}

			if (Utils.ExtractCirculationNumber(circulation.Id) == wantedNumber)
			{
				return true;
			}

			if (!string.IsNullOrWhiteSpace(circulation.TechnicalId)
				&& Utils.ExtractCirculationNumber(circulation.TechnicalId) == wantedNumber)
			{
				return true;
			}

			return false;
		}

		private async Task<IReadOnlyList<DiamondPublishedPlanHeaderModel>> EnsureHeadersAsync()
		{
			if (mcolHeaders is not null)
			{
				return mcolHeaders;
			}

			List<DiamondPublishedPlanHeaderModel> all = new List<DiamondPublishedPlanHeaderModel>();
			try
			{
				IReadOnlyList<DiamondPublishedPlanHeaderModel> listed =
					await mvarClient.ListPublishedPlansAsync(activeOnly: false);
				int i = 0;
				while (i < listed.Count)
				{
					all.Add(listed[i]);
					i++;
				}
			}
			catch
			{
			}

			if (all.Count == 0)
			{
				try
				{
					DiamondPublishedPlanHeaderModel? current = await mvarClient.GetPublishedCurrentAsync();
					if (current is not null)
					{
						all.Add(current);
					}
				}
				catch
				{
				}
			}

			mcolHeaders = all;
			return mcolHeaders;
		}

		private async Task<PublishedProjectPackage?> EnsurePackageAsync(DiamondPublishedPlanHeaderModel header)
		{
			if (mcolPackages.TryGetValue(header.Id, out PublishedProjectPackage? cached))
			{
				return cached;
			}

			byte[]? payload = await mvarClient.DownloadPublishedContentAsync(header.Id);
			if (payload is null || payload.Length == 0)
			{
				return null;
			}

			PublishedProjectPackage package = PublishedProjectPackage.FromUtf8Json(MaybeGunzip(payload));
			mcolPackages[header.Id] = package;
			return package;
		}

		private async Task<TopoLayout?> EnsureTopoAsync(Guid topoId, CancellationToken cancellationToken)
		{
			if (Guid.Empty.Equals(topoId))
			{
				return null;
			}

			if (mcolTopos.TryGetValue(topoId, out TopoLayout? cached))
			{
				return cached;
			}

			byte[]? payload = await mvarClient.DownloadTopoContentAsync(topoId);
			cancellationToken.ThrowIfCancellationRequested();
			if (payload is null || payload.Length == 0)
			{
				return null;
			}

			byte[] xmlBytes = MaybeGunzip(payload);
			TopoLayout layout;
			using (MemoryStream stream = new MemoryStream(xmlBytes, writable: false))
			{
				layout = TopoXmlSerializer.Load(stream);
			}

			layout.RebuildAll();
			// Misma infraestructura que el planificador / cabina: sin esto la
			// ficha del cuadrante ve todo el T3+T2 como BAU y pierde temporales
			// de vía 2 en tramos BAB.
			SfmDemoInfrastructure.Apply(layout);
			try
			{
				IReadOnlyList<DiamondTemporaryLimitModel> rows =
					await mvarClient.ListTemporaryLimitsAsync(topoId);
				TopoTemporaryLimits.Apply(layout, DiamondStoreTopoCatalog.ToTopoLimits(rows));
			}
			catch
			{
				// La ficha sigue con las fijas del XML si el almacén de temporales no responde.
			}

			mcolTopos[topoId] = layout;
			return layout;
		}

		/// <summary>
		/// Elige el plan publicado de esa fecha: vigente activo, vigente aunque no esté
		/// en producción, el último anterior o el próximo futuro.
		/// </summary>
		internal static DiamondPublishedPlanHeaderModel? PickPlanForDate(
			IReadOnlyList<DiamondPublishedPlanHeaderModel> plans,
			DateTime day)
		{
			if (plans is null || plans.Count == 0)
			{
				return null;
			}

			DateTime date = day.Date;
			DiamondPublishedPlanHeaderModel? coverActive = null;
			DiamondPublishedPlanHeaderModel? coverAny = null;
			DiamondPublishedPlanHeaderModel? lastBefore = null;
			DiamondPublishedPlanHeaderModel? firstAfter = null;

			int i = 0;
			while (i < plans.Count)
			{
				DiamondPublishedPlanHeaderModel plan = plans[i];
				DateTime from = plan.ValidFrom.Date;
				DateTime? to = plan.ValidTo.HasValue ? plan.ValidTo.Value.Date : null;
				bool covers = from <= date && (!to.HasValue || to.Value >= date);

				if (covers)
				{
					if (plan.IsActive && IsNewerCoverage(plan, coverActive))
					{
						coverActive = plan;
					}

					if (IsNewerCoverage(plan, coverAny))
					{
						coverAny = plan;
					}
				}
				else if (from <= date)
				{
					if (IsNewerCoverage(plan, lastBefore))
					{
						lastBefore = plan;
					}
				}
				else if (firstAfter is null
					|| from < firstAfter.ValidFrom.Date
					|| (from == firstAfter.ValidFrom.Date && plan.CompiledUtc > firstAfter.CompiledUtc))
				{
					firstAfter = plan;
				}

				i++;
			}

			return coverActive ?? coverAny ?? lastBefore ?? firstAfter;
		}

		private static bool IsNewerCoverage(
			DiamondPublishedPlanHeaderModel candidate,
			DiamondPublishedPlanHeaderModel? current)
		{
			if (current is null)
			{
				return true;
			}

			if (candidate.ValidFrom.Date != current.ValidFrom.Date)
			{
				return candidate.ValidFrom.Date > current.ValidFrom.Date;
			}

			return candidate.CompiledUtc > current.CompiledUtc;
		}

		private static bool EqualsIgnoreCase(string a, string? b)
		{
			return !string.IsNullOrWhiteSpace(b)
				&& string.Equals(a, b.Trim(), StringComparison.OrdinalIgnoreCase);
		}

		private static byte[] MaybeGunzip(byte[] payload)
		{
			if (payload.Length >= 2 && payload[0] == 0x1f && payload[1] == 0x8b)
			{
				using MemoryStream input = new MemoryStream(payload, writable: false);
				using GZipStream gzip = new GZipStream(input, CompressionMode.Decompress);
				using MemoryStream output = new MemoryStream();
				gzip.CopyTo(output);
				return output.ToArray();
			}

			return payload;
		}
	}

	public sealed class ScheduleSheetHit
	{
		public ScheduleSheetHit(ProjectCirculation circulation, DayOfWeek day)
		{
			Circulation = circulation;
			Day = day;
		}

		public ProjectCirculation Circulation { get; }

		public DayOfWeek Day { get; }
	}

	public sealed class SchedulePublishedDaySnapshot
	{
		public SchedulePublishedDaySnapshot(
			DiamondPublishedPlanHeaderModel header,
			PublishedProjectPackage package,
			Project? dayProject,
			TopoLayout? topo,
			DateTime date)
		{
			Header = header;
			Package = package;
			DayProject = dayProject;
			Topo = topo;
			Date = date.Date;
		}

		public DiamondPublishedPlanHeaderModel Header { get; }

		public PublishedProjectPackage Package { get; }

		public Project? DayProject { get; }

		public TopoLayout? Topo { get; }

		public DateTime Date { get; }

		public string PlanName
		{
			get
			{
				if (!string.IsNullOrWhiteSpace(Header.Name))
				{
					return Header.Name;
				}

				if (!string.IsNullOrWhiteSpace(Package.Name))
				{
					return Package.Name;
				}

				return "Plan de explotación";
			}
		}
	}
}

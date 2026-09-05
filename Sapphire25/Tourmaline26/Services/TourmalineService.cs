using BlazorBootstrap;
using Diamond.Cabin;
using Diamond.Project;
using Microsoft.EntityFrameworkCore;
using Sapphire2025.Storage;
using Sapphire2025Models.Aeneas;
using Sapphire2025Models.Authentication;
using Sapphire2025Models.Diamond;
using Sapphire2025Models.Expert;
using Sapphire2025Models.Expert.WorkshiftTemplates;
using Sapphire2026.Data.Models;
using Tourmaline26.Logic;
using Tourmaline26.Services.CabinCache;
using Tourmaline26.Services.Catalog;
using Tourmaline26.Services.Correspondence;
using Tourmaline26.Services.LocalDataModel;
using Tourmaline26.Services.TourmalineExperience;

namespace Tourmaline26.Services
{
	/// <summary>
	/// Procesador principal del sistema de información al viajero.
	/// </summary>
	public class TourmalineService
	{
		private SessionConfiguration mvarSessionConfig;
		public SystemConfiguration SystemConfig { get; private set; }
		public DeviceCollection Devices { get; private set; } = new DeviceCollection();
		private ILogger<TourmalineService> mvarLogger;
		private IServiceProvider mvarServiceProvider;
		private IConfiguration mvarConfiguration;
		private LEDDisplayService mvarLedDisplayService;
		private TourmalineExperienceService mvarTourmalineExperienceService;
		private DiamondLocalCache mvarDiamondCache;
		private CorrespondenceBoardService mvarCorrespondenceBoard;

		/// <summary>Guid de topología configurado en appsettings (Diamond:TopoId).</summary>
		public Guid ConfiguredTopoId { get; private set; }

		public event EventHandler? PassengerUpdateRequested;
		public event EventHandler? HMIUpdateRequested;

		public void RaiseHMIUpdate() => HMIUpdateRequested?.Invoke(this, EventArgs.Empty);
		public void RaisePassengerUpdate() => PassengerUpdateRequested?.Invoke(this, EventArgs.Empty);

		public TourmalineService(
			IConfiguration config,
			IServiceProvider serviceProvider,
			ILogger<TourmalineService> logger,
			LEDDisplayService ledDisplayService,
			TourmalineExperienceService tourmalineExperience,
			DiamondLocalCache diamondCache,
			CorrespondenceBoardService correspondenceBoard)
		{
			mvarSessionConfig = new SessionConfiguration();
			SystemConfig = new SystemConfiguration();
			mvarConfiguration = config;
			mvarLogger = logger;
			mvarServiceProvider = serviceProvider;
			mvarLedDisplayService = ledDisplayService;
			mvarTourmalineExperienceService = tourmalineExperience;
			mvarDiamondCache = diamondCache;
			mvarCorrespondenceBoard = correspondenceBoard;
			mvarSessionConfig.Cabin = new CabinEnvironment();
		}

		private async Task InitConfig(IConfiguration config)
		{
			SystemConfiguration? auxConfig = config.GetSection("SystemConfiguration").Get<SystemConfiguration>();
			if (null != auxConfig)
				SystemConfig = auxConfig;

			if (SystemConfig.Cameras is null || SystemConfig.Cameras.Count == 0)
			{
				List<CameraInfo>? rootCameras = config.GetSection("Cameras").Get<List<CameraInfo>>();
				if (rootCameras is { Count: > 0 })
					SystemConfig.Cameras = rootCameras;
			}

			mvarLogger.LogInformation(
				"Cámaras cargadas: {Count}",
				SystemConfig.Cameras?.Count ?? 0);

			//string? topoIdText = config["Diamond:TopoId"];
			//if (Guid.TryParse(topoIdText, out Guid topoId))
			//Cambiado lugar de configuración de Id de TopoId a la sección de sistema en appsettings.json.
			if(Guid.TryParse(SystemConfig.DiamondTopologyId, out Guid topoId))
				ConfiguredTopoId = topoId;
			else
				ConfiguredTopoId = Guid.Empty;

			IConfigurationSection debugStartupSession = config.GetSection("debugStartSession");
			if (debugStartupSession.Exists())
				debugStartupSession.Bind(mvarSessionConfig);
			await auxInitDevices(config);
		}

		private async Task auxInitDevices(IConfiguration config)
		{
			IConfigurationSection section = config.GetSection("Devices");
			if (!section.GetChildren().Any())
				section = config.GetSection("SystemConfiguration:Devices");
			int deviceCount = 0;
			foreach (IConfigurationSection deviceSection in section.GetChildren())
			{
				DeviceMapped nuevo = new DeviceMapped();
				nuevo.SetParameters(
					deviceSection["Address"],
					deviceSection["Type"],
					deviceSection["Coach"],
					deviceSection["Side"],
					deviceSection["HeaderSize"],
					deviceSection["Lines"],
					deviceSection["PublicId"]);
				Devices.Add(nuevo);

				mvarLogger.LogInformation(
					"Dispositivo detectado: Address={Address}, Type={Type}, Coach={Coach}, Side={Side}, HeaderSize={HeaderSize}, Lines={Lines}, PublicId={PublicId}",
					deviceSection["Address"],
					deviceSection["Type"],
					deviceSection["Coach"],
					deviceSection["Side"],
					deviceSection["HeaderSize"],
					deviceSection["Lines"],
					deviceSection["PublicId"]);
				deviceCount++;
			}
			mvarLogger.LogInformation("Total de dispositivos detectados: {DeviceCount}", deviceCount);
			mvarLedDisplayService.Init(Devices);
			await mvarLedDisplayService.Print(true, $"Tourmaline {SystemConfig.Version}", false, Alignment.Center);
			await mvarLedDisplayService.Print(false, "SFM", false);
		}

		public SessionConfiguration SessionConfig
		{
			get => mvarSessionConfig;
		}

		public async Task InitData()
		{
			await InitConfig(mvarConfiguration);
			await EnsureLocalDatabaseSchema();
			await InitializeLocalRegister();
			await InitializeDiamond();
		}

		public async Task<bool> EnsureInitialized()
		{
			if (!mvarSessionConfig.Initialized)
			{
				mvarLogger.LogInformation("Iniciando sistema de información al viajero Tourmaline...");
				try
				{
					await InitData();
					mvarSessionConfig.InitError = string.Empty;
					mvarLogger.LogInformation("Sistema de información al viajero Tourmaline iniciado correctamente.");
				}
				catch (Exception ex)
				{
					mvarSessionConfig.InitError = ex.Message;
					mvarLogger.LogError(ex, "Error durante la inicialización. HMI en modo degradado.");
					if (null == mvarSessionConfig.Cabin)
						mvarSessionConfig.Cabin = new CabinEnvironment();
				}
				finally
				{
					mvarSessionConfig.Initialized = true;
				}
			}
			return mvarSessionConfig.Initialized;
		}

		private async Task EnsureLocalDatabaseSchema()
		{
			using (IServiceScope scope = mvarServiceProvider.CreateScope())
			{
				TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
				await db.Database.EnsureCreatedAsync();
				await EnsureNotesMediaColumnsAsync(db);
				try
				{
					_ = await db.Trains.AsNoTracking().FirstOrDefaultAsync();
					_ = await db.LocalSystem.AsNoTracking().FirstOrDefaultAsync();
					_ = await db.Notes.AsNoTracking().FirstOrDefaultAsync();
					_ = await db.DiamondTopos.AsNoTracking().FirstOrDefaultAsync();
					_ = await db.DiamondPublishedPlans.AsNoTracking().FirstOrDefaultAsync();
				}
				catch (Exception ex) when (IsSqliteSchemaMismatch(ex))
				{
					mvarLogger.LogWarning(ex,
						"Esquema de tourmaline.db incompatible. Regenerando base de datos local vacía.");
					await db.Database.EnsureDeletedAsync();
					await db.Database.EnsureCreatedAsync();
				}
			}
		}

		/// <summary>
		/// Añade columnas de nota multimedia / etiquetado si la SQLite es anterior
		/// (EnsureCreated no altera tablas existentes).
		/// </summary>
		private async Task EnsureNotesMediaColumnsAsync(TourmalineContext db)
		{
			try
			{
				await db.Database.OpenConnectionAsync();
				HashSet<string> columns = await ReadSqliteTableColumnsAsync(db, "Notes");
				if (columns.Count == 0)
				{
					return;
				}

				await AddSqliteColumnIfMissingAsync(db, columns, "Notes", "MediaExtension", "TEXT");
				await AddSqliteColumnIfMissingAsync(db, columns, "Notes", "MediaContentType", "TEXT");
				await AddSqliteColumnIfMissingAsync(db, columns, "Notes", "ClosureTime", "TEXT");
				await AddSqliteColumnIfMissingAsync(db, columns, "Notes", "ClosureUser", "TEXT");
				await AddSqliteColumnIfMissingAsync(db, columns, "Notes", "IsValid", "INTEGER NOT NULL DEFAULT 0");
				await AddSqliteColumnIfMissingAsync(db, columns, "Notes", "IsSymptom", "INTEGER NOT NULL DEFAULT 0");
				await AddSqliteColumnIfMissingAsync(db, columns, "Notes", "SystemAffected", "INTEGER NOT NULL DEFAULT 0");
			}
			catch (Exception ex)
			{
				mvarLogger.LogWarning(ex, "No se pudieron añadir columnas multimedia a Notes.");
			}
			finally
			{
				await db.Database.CloseConnectionAsync();
			}
		}

		private static async Task<HashSet<string>> ReadSqliteTableColumnsAsync(
			TourmalineContext db,
			string table)
		{
			HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			System.Data.Common.DbConnection connection = db.Database.GetDbConnection();
			await using System.Data.Common.DbCommand cmd = connection.CreateCommand();
			cmd.CommandText = "PRAGMA table_info(" + table + ")";
			await using System.Data.Common.DbDataReader reader = await cmd.ExecuteReaderAsync();
			while (await reader.ReadAsync())
			{
				if (!reader.IsDBNull(1))
				{
					names.Add(reader.GetString(1));
				}
			}

			return names;
		}

		private static async Task AddSqliteColumnIfMissingAsync(
			TourmalineContext db,
			HashSet<string> columns,
			string table,
			string column,
			string declaration)
		{
			if (columns.Contains(column))
			{
				return;
			}

			await db.Database.ExecuteSqlRawAsync(
				"ALTER TABLE \"" + table + "\" ADD COLUMN \"" + column + "\" " + declaration);
			columns.Add(column);
		}

		private static bool IsSqliteSchemaMismatch(Exception ex)
		{
			for (Exception? current = ex; null != current; current = current.InnerException)
			{
				if (current is Microsoft.Data.Sqlite.SqliteException sqlite)
				{
					string message = sqlite.Message;
					if (message.Contains("no such column", StringComparison.OrdinalIgnoreCase)
						|| message.Contains("no such table", StringComparison.OrdinalIgnoreCase)
						|| sqlite.SqliteErrorCode == 1)
						return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Carga topo + plan publicado vigente desde la caché local.
		/// </summary>
		public async Task InitializeDiamond()
		{
			if (null == mvarSessionConfig.Cabin)
				mvarSessionConfig.Cabin = new CabinEnvironment();

			Guid topoId = ConfiguredTopoId;
			using (IServiceScope scope = mvarServiceProvider.CreateScope())
			{
				TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
				DBLocalSystem? localSystem = await db.LocalSystem.FirstOrDefaultAsync();
				if (Guid.Empty.Equals(topoId) && localSystem is not null
					&& !Guid.Empty.Equals(localSystem.CurrentTopoId))
				{
					topoId = localSystem.CurrentTopoId;
				}
			}

			if (Guid.Empty.Equals(topoId))
			{
				mvarLogger.LogWarning("Diamond:TopoId no configurado; sin topología local.");
				return;
			}

			bool loaded = await mvarDiamondCache.LoadIntoEnvironmentAsync(
				mvarSessionConfig.Cabin,
				topoId,
				DateTime.Now);
			if (loaded)
			{
				mvarLogger.LogInformation(
					"Diamond cargado: topo {Topo}, plan «{Plan}», día {Day}, trenes {Count}",
					topoId,
					mvarSessionConfig.Cabin.PublishedPlanName,
					mvarSessionConfig.Cabin.DayProject?.PlanningDay,
					mvarSessionConfig.Cabin.DayProject?.Circulations.Count ?? 0);
			}
			else
			{
				mvarLogger.LogWarning(
					"No hay caché Diamond para topo {Topo}. Sincroniza desde el menú Diamond.",
					topoId);
			}
		}

		public async Task InitializeLocalRegister()
		{
			using (IServiceScope scope = mvarServiceProvider.CreateScope())
			{
				TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
				await db.Database.EnsureCreatedAsync();
				Train? auxTrain = await db.Trains.FirstOrDefaultAsync(t => t.Name.Contains(SystemConfig.Name));
				DBLocalSystem? auxLocalSystem = await db.LocalSystem.FirstOrDefaultAsync();
				if (null != auxTrain)
				{
					if (null == auxLocalSystem)
					{
						auxLocalSystem = new DBLocalSystem
						{
							LastSapphireDownload = DateTime.Now,
							LastAeneasSync = DateTime.MinValue,
							LastTopoSync = DateTime.MinValue,
							LastDiamondSync = DateTime.MinValue,
							LastPlanSync = DateTime.MinValue,
							CurrentPublishedPlanId = Guid.Empty,
							CurrentTopoId = ConfiguredTopoId
						};
						db.LocalSystem.Add(auxLocalSystem);
					}
					auxLocalSystem.TrainName = SystemConfig.Name;
					auxLocalSystem.TrainId = auxTrain.Guid;
					if (Guid.Empty.Equals(auxLocalSystem.CurrentTopoId)
						&& !Guid.Empty.Equals(ConfiguredTopoId))
					{
						auxLocalSystem.CurrentTopoId = ConfiguredTopoId;
					}
					await db.SaveChangesAsync();
					SystemConfig.TrainId = auxLocalSystem.TrainId;
					SystemConfig.Name = auxLocalSystem.TrainName;
				}
			}
		}

		/// <summary>
		/// Sincroniza con el servidor Sapphire (topo por hash + planes publicados futuros).
		/// <paramref name="client"/> debe ser el <see cref="DiamondClient"/> del circuito Blazor
		/// (inyectado en el componente), no uno resuelto desde el proveedor raíz: ese scope
		/// no tiene IJSRuntime interactivo y falla al leer la sesión de localStorage.
		/// </summary>
		public async Task<DiamondSyncResult> SyncDiamondAsync(DiamondClient client)
		{
			if (client is null)
			{
				throw new ArgumentNullException(nameof(client));
			}

			Guid topoId = ConfiguredTopoId;
			if (Guid.Empty.Equals(topoId))
			{
				return new DiamondSyncResult
				{
					Success = false,
					Message = "Configure Diamond:TopoId en appsettings.json"
				};
			}

			DiamondSyncResult result = await mvarDiamondCache.SyncFromServerAsync(client, topoId);
			if (result.Success)
			{
				await InitializeDiamond();
			}

			return result;
		}

		public async Task DoCirculationSelect(Circulation rhs)
		{
			if (null == SessionConfig.Cabin)
				return;

			// La misión Diamond se aplica siempre; Experience es opcional.
			SessionConfig.ClearUnscheduledTrain();
			SessionConfig.Cabin.Circulation = rhs;
			UpdatePassengerInformationMode();

			if (null != rhs.Asimilation)
			{
				mvarLogger.LogInformation("Circulación {Id} → asim {Asim}", rhs.Id, rhs.Asimilation.Id);
				try
				{
					await RecallTourmalineExperience(rhs.Asimilation);
				}
				catch (Exception ex)
				{
					// Nunca tumbar el circuito Blazor por el simulador 3D.
					mvarLogger.LogWarning(ex, "Tourmaline Experience no disponible al seleccionar circulación.");
				}
			}
		}

		/// <summary>
		/// Elige un número de tren sin malla (sin plan Diamond ni sesión Zafiro).
		/// </summary>
		public async Task<bool> SelectUnscheduledTrainAsync(string? trainToken)
		{
			if (!UnscheduledCirculation.LooksLikeTrainToken(trainToken))
				return false;

			string token = UnscheduledCirculation.NormalizeToken(trainToken);
			if (null != SessionConfig.Cabin)
				SessionConfig.Cabin.Circulation = null;
			SessionConfig.UnscheduledTrainToken = token;
			UpdatePassengerInformationMode();
			try
			{
				await RecallEndTourmalineExperience();
			}
			catch (Exception ex)
			{
				mvarLogger.LogWarning(ex, "Experience no detenida al elegir tren sin malla.");
			}

			mvarLogger.LogInformation("Tren {Train} seleccionado sin datos de malla.", token);
			RaiseHMIUpdate();
			RaisePassengerUpdate();
			return true;
		}

		public async Task TerminateActiveCirculationAsync()
		{
			if (null != SessionConfig.Cabin)
				SessionConfig.Cabin.Circulation = null;
			SessionConfig.ClearUnscheduledTrain();
			UpdatePassengerInformationMode();
			try
			{
				await RecallEndTourmalineExperience();
			}
			catch (Exception ex)
			{
				mvarLogger.LogWarning(ex, "No se pudo detener Experience al cerrar la circulación.");
			}
		}

		/// <summary>
		/// Cierra la circulación en curso (si hay) y arranca la del número de tren del cuadrante.
		/// </summary>
		public async Task<bool> StartCirculationFromTrainTokenAsync(string? trainId, TimeSpan? startTime = null)
		{
			if (string.IsNullOrWhiteSpace(trainId) || SessionConfig.Cabin is null)
				return false;

			IReadOnlyList<Circulation> matches = SessionConfig.Cabin.CirculationsForShiftTokens(
				new[] { trainId.Trim() },
				null);
			if (matches.Count == 0)
			{
				mvarLogger.LogWarning(
					"No hay circulación {Train} en el plan publicado de hoy; se usa sin malla.",
					trainId);
				return await SelectUnscheduledTrainAsync(trainId);
			}

			Circulation chosen = matches[0];
			if (startTime.HasValue && matches.Count > 1)
			{
				TimeSpan want = startTime.Value;
				int best = 0;
				double bestDiff = double.MaxValue;
				int i = 0;
				while (i < matches.Count)
				{
					double diff = Math.Abs((matches[i].Departure - want).TotalMinutes);
					if (diff < bestDiff)
					{
						bestDiff = diff;
						best = i;
					}

					i++;
				}

				chosen = matches[best];
			}

			Circulation? current = SessionConfig.Cabin.Circulation;
			if (current is not null
				&& string.Equals(current.Id, chosen.Id, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			await TerminateActiveCirculationAsync();
			await DoCirculationSelect(chosen);
			RaiseHMIUpdate();
			RaisePassengerUpdate();
			return true;
		}

		private async Task LogTourmalineTripStartedAsync(Circulation rhs)
		{
			if (SessionConfig.Session is null || Guid.Empty.Equals(SessionConfig.Session.Token))
				return;

			string viaje = rhs.HasServiceNumber ? rhs.ServiceNumber : rhs.Id;
			string dest = rhs.Asimilation?.Destination.Name ?? string.Empty;
			string origin = rhs.Asimilation?.Origin.Name ?? string.Empty;
			System.Text.StringBuilder extra = new System.Text.StringBuilder();
			if (!string.IsNullOrWhiteSpace(viaje))
			{
				extra.Append("viaje=");
				extra.Append(viaje.Trim());
			}

			if (!string.IsNullOrWhiteSpace(origin) || !string.IsNullOrWhiteSpace(dest))
			{
				if (extra.Length > 0)
					extra.Append(' ');
				extra.Append(origin.Trim());
				if (!string.IsNullOrWhiteSpace(origin) && !string.IsNullOrWhiteSpace(dest))
					extra.Append('→');
				extra.Append(dest.Trim());
			}

			string detail = "tourmaline";
			if (!string.IsNullOrWhiteSpace(SystemConfig.Name))
				detail += " tren=" + SystemConfig.Name.Trim();
			if (!Guid.Empty.Equals(SystemConfig.TrainId))
				detail += " id=" + SystemConfig.TrainId.ToString();
			if (extra.Length > 0)
				detail += " " + extra;

			try
			{
				using (IServiceScope scope = mvarServiceProvider.CreateScope())
				{
					AuthenticationClient client = scope.ServiceProvider.GetRequiredService<AuthenticationClient>();
					await client.LogActivity(
						Sapphire2025Models.Common.sessionEventType.tourmalineTripStarted,
						detail,
						SessionConfig.Session.Token);
				}
			}
			catch (Exception ex)
			{
				mvarLogger.LogWarning(ex, "No se pudo registrar el inicio de viaje en el log de Sapphire.");
			}
		}

		public void UpdatePassengerInformationMode()
		{
			SessionConfiguration session = mvarSessionConfig;
			CabinEnvironment? cabin = session.Cabin;
			Circulation? circulation = cabin?.Circulation;

			if (null == circulation || cabin is null)
			{
				session.PreviewArrivalStation = null;
				mvarCorrespondenceBoard.SetContext(
					stationName: null,
					excludeDestination: null);
				if (session.InformationMode != Enums.PassengerInformationMode.Default)
					session.InformationMode = Enums.PassengerInformationMode.Default;
				return;
			}

			TimedCall? announced = ResolveAnnouncedCall(cabin, circulation);
			string? destName = cabin.Asimilation?.Destination.Name
				?? circulation.Asimilation?.Destination.Name;
			mvarCorrespondenceBoard.SetContext(
				announced?.Station,
				destName);

			if (session.ServiceMode.Main
				&& !session.ServiceMode.DemoMode
				&& !session.ServiceMode.RouteSimulation)
				return;

			Enums.PassengerInformationMode next;
			StationInfo? currentStation = cabin.CurrentStation;
			Asimilation? asim = cabin.Asimilation;
			StationInfo? lastStation = asim?.Destination;
			StationInfo? originStation = asim?.Origin;

			int welcomeMeters = SystemConfig.WelcomeDistanceMeters;
			if (welcomeMeters < 0)
				welcomeMeters = 0;

			long? originPk = CabinItinerary.OriginRoutePk(circulation);
			long? destPk = CabinItinerary.DestinationRoutePk(circulation);
			bool nearOrigin = originPk.HasValue
				&& Math.Abs(cabin.PK - originPk.Value) < welcomeMeters;
			bool nearDestination = destPk.HasValue
				&& (!originPk.HasValue || destPk.Value != originPk.Value)
				&& Math.Abs(cabin.PK - destPk.Value) < CabinItinerary.DefaultStationAreaMeters;

			bool sameStation(StationInfo? a, StationInfo? b) =>
				a is not null
				&& b is not null
				&& string.Equals(a.Id, b.Id, StringComparison.Ordinal);

			int lookahead = CorrespondenceLookaheadMeters();
			long remainingMeters = announced is null
				? long.MaxValue
				: Math.Abs(cabin.PK - announced.Pk);
			bool withinLookahead = announced is not null && remainingMeters <= lookahead;

			StationInfo? preview = null;

			if (nearOrigin && !nearDestination)
			{
				next = Enums.PassengerInformationMode.BeginOfTrip;
			}
			else if (currentStation is not null && sameStation(currentStation, lastStation))
			{
				next = Enums.PassengerInformationMode.EndOfTrip;
			}
			else if (currentStation is not null && sameStation(currentStation, originStation))
			{
				next = CruiseOrStopsList(session, remainingMeters);
			}
			else if (IsTechnicalStop(circulation, currentStation))
			{
				next = CruiseOrStopsList(session, remainingMeters);
			}
			else if (withinLookahead)
			{
				next = Enums.PassengerInformationMode.NextStopInfo;
				if (currentStation is null && announced is not null)
					preview = announced.Station;
			}
			else
			{
				next = CruiseOrStopsList(session, remainingMeters);
			}

			session.PreviewArrivalStation = preview;
			if (session.InformationMode != next)
				session.InformationMode = next;
		}

		private static Enums.PassengerInformationMode CruiseOrStopsList(
			SessionConfiguration session,
			long remainingMeters) =>
			PassengerCruiseRules.ShowNextStopsList(session.CurrentSpeed, remainingMeters)
				? Enums.PassengerInformationMode.NextStopsList
				: Enums.PassengerInformationMode.Cruise;

		private int CorrespondenceLookaheadMeters()
		{
			int baseMeters = SystemConfig.CorrespondenceBaseMeters;
			if (baseMeters < 0)
				baseMeters = 0;
			int perBus = SystemConfig.CorrespondenceMetersPerBus;
			if (perBus < 0)
				perBus = 0;
			return baseMeters + perBus * mvarCorrespondenceBoard.AnnouncedBusCount;
		}

		private static TimedCall? ResolveAnnouncedCall(CabinEnvironment cabin, Circulation circulation)
		{
			TimedCall? atStop = FindCallAtStation(circulation, cabin.CurrentStation);
			if (atStop is not null && CabinItinerary.IsCommercial(atStop))
				return atStop;

			IReadOnlyList<TimedCall> remaining = CabinItinerary.RemainingCommercialCalls(
				circulation,
				cabin.PK,
				includeCurrentStation: false);
			return remaining.Count > 0 ? remaining[0] : null;
		}

		private static TimedCall? FindCallAtStation(Circulation circulation, StationInfo? station)
		{
			if (station is null)
				return null;
			int i = 0;
			while (i < circulation.Calls.Count)
			{
				if (string.Equals(circulation.Calls[i].Station.Id, station.Id, StringComparison.Ordinal))
					return circulation.Calls[i];
				i++;
			}
			return null;
		}

		private static bool IsTechnicalStop(Circulation circulation, StationInfo? station)
		{
			TimedCall? atStop = FindCallAtStation(circulation, station);
			return atStop is not null
				&& !atStop.IsDestination
				&& !CabinItinerary.IsCommercial(atStop);
		}

		private async Task RecallTourmalineExperience(Asimilation asimilation)
		{
			if (!mvarTourmalineExperienceService.IsConfigured)
			{
				mvarLogger.LogDebug("Experience no configurado; misión sin simulador 3D.");
				return;
			}

			mvarLogger.LogInformation("Starting TourmalineExperience for asimilation {Name}", asimilation.Id);
			// Stop puede fallar si no hay proceso: no es error de misión.
			await mvarTourmalineExperienceService.Stop();

			LaunchRequest request = new LaunchRequest();
			TourmalineExperienceService.SampleWeatherType climate =
				ExperienceWeather.From(SessionConfig.CurrentWeather);
			request.Climate = (int)climate;
			request.Consist = "Triple81";
			request.Now = DateTime.Now.ToString("HH:mm");
			// Paths hardcodeados SFM (T11/T12/T21/T22/T31/T32) según vista y sentido.
			request.RoutePath = ExperienceRoutePathMap.Resolve(asimilation);
			request.Route = "SFM";
			mvarLogger.LogInformation(
				"Experience RoutePath={Path} (view={View}, sense={Sense}, {Origin}→{Dest})",
				request.RoutePath,
				asimilation.ViewId,
				asimilation.Sense,
				asimilation.Origin.DisplayCode,
				asimilation.Destination.DisplayCode);
			switch (DateTime.Now.Month)
			{
				case 3:
				case 4:
				case 5:
					request.Season = 0; break;
				case 6:
				case 7:
				case 8:
					request.Season = 1; break;
				default:
					request.Season = 2; break;
			}

			bool launched = await mvarTourmalineExperienceService.Launch(request);
			if (!launched)
			{
				mvarLogger.LogWarning(
					"No se pudo lanzar Tourmaline Experience para {Route}. La circulación Diamond queda seleccionada.",
					request.RoutePath);
				return;
			}

			if (SessionConfig.CurrentWeather is not null)
			{
				try
				{
					await mvarTourmalineExperienceService.SetWeather(climate);
					mvarLogger.LogInformation("TE clima al lanzar: {Kind}", climate);
				}
				catch (Exception ex)
				{
					mvarLogger.LogWarning(ex, "TE clima al lanzar: no se pudo aplicar {Kind}", climate);
				}
			}
		}

		public async Task RecallEndTourmalineExperience()
		{
			mvarLogger.LogInformation("Ending TourmalineExperience");
			try
			{
				await mvarTourmalineExperienceService.Stop();
			}
			catch (Exception ex)
			{
				mvarLogger.LogWarning(ex, "Error al detener Tourmaline Experience (se ignora).");
			}
		}

		public async Task RetrieveUsers()
		{
			SessionConfig.ColUsers = new Dictionary<Guid, UserModelBase>();
			using (IServiceScope scope = mvarServiceProvider.CreateScope())
			{
				TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
				IEnumerable<User> usuarios = await db.Users.ToListAsync();
				foreach (User usuario in usuarios)
				{
					UserModelBase nuevo = new UserModelBase();
					nuevo.guid = usuario.guid;
					nuevo.CF = usuario.CF;
					nuevo.Name = usuario.UserName;
					nuevo.CredentialKey = 0;
					SessionConfig.ColUsers.Add(nuevo.guid, nuevo);
				}
			}
		}

		#region Authentication

		public async Task<SessionModel?> UserLogin(string username, string pwd)
		{
			if (null != SessionConfig.Session)
				await UserLogout();

			UserLoginModel modelo = new UserLoginModel();
			SessionModel? sesion = null;
			modelo.userName = username;
			modelo.password = pwd;
			modelo.Client = "tourmaline";
			if (!Guid.Empty.Equals(SystemConfig.TrainId))
				modelo.TrainId = SystemConfig.TrainId.ToString();
			if (!string.IsNullOrWhiteSpace(SystemConfig.Name))
				modelo.TrainName = SystemConfig.Name;
			using (IServiceScope scope = mvarServiceProvider.CreateScope())
			{
				AuthenticationClient auxCliente = scope.ServiceProvider.GetRequiredService<AuthenticationClient>();
				IntStorageService storage = scope.ServiceProvider.GetRequiredService<IntStorageService>();
				try
				{
					mvarLogger.LogInformation("Enviando credenciales para inicio de sesión de {User}", username);
					sesion = await auxCliente.Login(modelo);
				}
				catch (Exception ex)
				{
					mvarLogger.LogError("Fallo técnico en inicio de sesión de {User}: {Symptoms}", username, ex.Message);
				}

				if (null != sesion)
				{
					foreach (Sapphire2025Models.Common.UserRole rol in sesion.Roles)
					{
						mvarLogger.LogInformation("Usuario {User} tiene rol {Role}", username, rol.ToString());
						sesion.User.CredentialKey |= (byte)rol;
					}
				}

				try
				{
					await storage.SetSessionInfo(sesion);
				}
				catch (Exception ex)
				{
					mvarLogger.LogWarning(ex, "No se pudo persistir la sesión local de {User}", username);
				}
			}
			SessionConfig.Session = sesion;
			return SessionConfig.Session;
		}

		/// <summary>
		/// Sliding expiry contra Sapphire. El tren llama esto en segundo plano
		/// para que el token no caduque a las 4 h de inactividad.
		/// </summary>
		public async Task KeepSessionAliveAsync()
		{
			SessionModel? session = SessionConfig.Session;
			if (null == session || Guid.Empty.Equals(session.Token))
				return;

			using (IServiceScope scope = mvarServiceProvider.CreateScope())
			{
				AuthenticationClient client = scope.ServiceProvider.GetRequiredService<AuthenticationClient>();
				IntStorageService storage = scope.ServiceProvider.GetRequiredService<IntStorageService>();
				try
				{
					bool ok = await client.SessionPingAsync();
					if (ok)
					{
						SessionModel? stored = await storage.GetSessionInfo();
						if (null != stored)
							session.ExpiryUtc = stored.ExpiryUtc;
					}
					else
					{
						mvarLogger.LogWarning(
							"Keep-alive de sesión Sapphire no confirmado para {User}",
							session.User?.Name ?? session.Token.ToString());
					}
				}
				catch (Exception ex)
				{
					mvarLogger.LogWarning(ex, "Fallo en keep-alive de sesión Sapphire");
				}
			}
		}

		/// <summary>
		/// Tras un login correcto, registra el odómetro MVB en Sapphire si el bus está vivo
		/// y el valor ha cambiado respecto al último guardado.
		/// No lanza: un fallo no debe deshacer la sesión.
		/// </summary>
		public async Task TryReportOdometerAfterLoginAsync()
		{
			if (SessionConfig.Session is null)
				return;
			if (Guid.Empty.Equals(SystemConfig.TrainId))
			{
				mvarLogger.LogDebug("Odómetro: sin TrainId, no se envía a Sapphire.");
				return;
			}

			ServiceMode mode = SessionConfig.ServiceMode;
			if (!mode.MVBEnabled || mode.MVBDummy)
			{
				mvarLogger.LogInformation("Odómetro: MVB no activo, no se envía a Sapphire.");
				return;
			}

			MVBData? mvb = SessionConfig.CurrentMVBData;
			if (null == mvb)
			{
				mvarLogger.LogInformation("Odómetro: sin lectura MVB, no se envía a Sapphire.");
				return;
			}

			if (SessionConfig.MVBLastUpdate == DateTime.MinValue
				|| (DateTime.Now - SessionConfig.MVBLastUpdate).TotalSeconds > 15)
			{
				mvarLogger.LogInformation("Odómetro: lectura MVB no reciente, no se envía a Sapphire.");
				return;
			}

			long current = mvb.Odometer;
			if (current <= 0)
			{
				mvarLogger.LogInformation("Odómetro: lectura {Value} no válida, no se envía.", current);
				return;
			}

			using (IServiceScope scope = mvarServiceProvider.CreateScope())
			{
				AeneasClient client = scope.ServiceProvider.GetRequiredService<AeneasClient>();
				try
				{
					TrainModel? train = await client.train(SystemConfig.TrainId.ToString());
					if (train is null)
					{
						mvarLogger.LogWarning("Odómetro: Sapphire no devolvió el tren {TrainId}.", SystemConfig.TrainId);
						return;
					}

					if (train.LastOdometer == current)
					{
						mvarLogger.LogInformation(
							"Odómetro: {Value} ya está registrado en Sapphire, no se reenvía.",
							current);
						return;
					}

					if (current < train.LastOdometer)
					{
						mvarLogger.LogWarning(
							"Odómetro MVB {Current} es menor que el de Sapphire {Last}; no se envía.",
							current,
							train.LastOdometer);
						return;
					}

					bool ok = await client.setOdometer(SystemConfig.TrainId, current, client: "tourmaline");
					if (ok)
					{
						mvarLogger.LogInformation(
							"Odómetro {Value} enviado a Sapphire (anterior {Last}).",
							current,
							train.LastOdometer);
					}
					else
					{
						mvarLogger.LogWarning(
							"Sapphire rechazó el odómetro {Value} del tren {Train}.",
							current,
							SystemConfig.Name);
					}
				}
				catch (Exception ex)
				{
					mvarLogger.LogWarning(ex, "No se pudo enviar el odómetro a Sapphire tras el login.");
				}
			}
		}

		/// <summary>
		/// Intenta actualizar topología y planes publicados tras un login.
		/// No lanza: un fallo de red no debe deshacer la sesión.
		/// <paramref name="client"/> debe ser el <see cref="DiamondClient"/> del circuito Blazor.
		/// </summary>
		public async Task<DiamondSyncResult> TrySyncAfterLoginAsync(DiamondClient client)
		{
			if (SessionConfig.Session is null)
			{
				return new DiamondSyncResult
				{
					Success = false,
					Message = "Sin sesión; no se sincroniza."
				};
			}

			try
			{
				DiamondSyncResult result = await SyncDiamondAsync(client);
				if (result.Success)
				{
					mvarLogger.LogInformation("Resincronización Diamond tras login: {Message}", result.Message);
				}
				else
				{
					mvarLogger.LogWarning("Resincronización Diamond tras login no completada: {Message}", result.Message);
				}

				return result;
			}
			catch (Exception ex)
			{
				mvarLogger.LogWarning(ex, "Fallo al resincronizar Diamond tras el login");
				return new DiamondSyncResult
				{
					Success = false,
					Message = ex.Message
				};
			}
			finally
			{
				await TrySyncPlacesCatalogAsync(client);
			}
		}

		/// <summary>
		/// Descarga places.xml de Zafiro solo si el hash remoto cambió.
		/// Cualquier fallo deja el catálogo local intacto.
		/// </summary>
		public async Task TrySyncPlacesCatalogAsync(DiamondClient client)
		{
			if (client is null)
				return;

			try
			{
				PlacesCatalogHeaderModel? header = await client.GetPlacesCatalogHeaderAsync();
				if (header is null || !header.Exists || string.IsNullOrWhiteSpace(header.ContentHash))
				{
					mvarLogger.LogDebug("places.xml remoto no disponible; se conserva el catálogo local.");
					return;
				}

				PlacesCatalog catalog = mvarServiceProvider.GetRequiredService<PlacesCatalog>();
				if (string.Equals(catalog.ContentHash, header.ContentHash, StringComparison.OrdinalIgnoreCase))
				{
					mvarLogger.LogDebug("places.xml ya está al día ({Hash}).", header.ContentHash);
					return;
				}

				PlacesCatalogContentModel? content = await client.DownloadPlacesCatalogAsync();
				if (content is null || string.IsNullOrWhiteSpace(content.Xml))
				{
					mvarLogger.LogWarning("places.xml remoto cambió pero no se pudo descargar; se conserva el local.");
					return;
				}

				if (!string.Equals(content.ContentHash, header.ContentHash, StringComparison.OrdinalIgnoreCase)
					&& !string.IsNullOrWhiteSpace(content.ContentHash))
				{
					mvarLogger.LogWarning("Hash de places.xml inconsistente entre cabecera y contenido; se omite.");
					return;
				}

				if (!catalog.TryReplaceWithXml(content.Xml, out string error))
				{
					mvarLogger.LogWarning("places.xml descargado no es válido ({Error}); se conserva el local.", error);
					return;
				}

				RaiseHMIUpdate();
				RaisePassengerUpdate();
			}
			catch (Exception ex)
			{
				mvarLogger.LogWarning(ex, "Sincronización de places.xml omitida; se conserva el catálogo local.");
			}
		}

		public async Task UserLogout()
		{
			SessionModel? session = SessionConfig.Session;
			if (null != session)
			{
				mvarLogger.LogInformation("Cierre de sesión de {User}",
				session.User.Name);
				using (IServiceScope scope = mvarServiceProvider.CreateScope())
				{
					AuthenticationClient auxCliente = scope.ServiceProvider.GetRequiredService<AuthenticationClient>();
					try
					{
						await auxCliente.Logout(
							session.Token.ToString(),
							client: "tourmaline",
							trainId: Guid.Empty.Equals(SystemConfig.TrainId) ? null : SystemConfig.TrainId.ToString(),
							trainName: SystemConfig.Name);
					}
					catch (Exception ex)
					{
						mvarLogger.LogError("Fallo técnico en cierre de sesión de {User}: {Symptoms}",
						session.User.Name, ex.Message);
					}
				}
			}
			SessionConfig.Session = null;
			SessionConfig.ClearDriverShift();
			try
			{
				using (IServiceScope scope = mvarServiceProvider.CreateScope())
				{
					IntStorageService storage = scope.ServiceProvider.GetRequiredService<IntStorageService>();
					await storage.SetSessionInfo(null);
				}
			}
			catch (Exception ex)
			{
				mvarLogger.LogWarning(ex, "No se pudo borrar la sesión local al cerrar");
			}
		}

		/// <summary>
		/// Si el usuario tiene turno grafiado hoy (maquinista), carga sus trenes.
		/// Si no hay asignación, no se ofrece la lista de turno.
		/// </summary>
		public async Task TryLoadDriverShiftAsync(ExpertClient client)
		{
			if (client is null)
			{
				throw new ArgumentNullException(nameof(client));
			}

			SessionConfig.ClearDriverShift();
			if (SessionConfig.Session?.User is null
				|| Guid.Empty.Equals(SessionConfig.Session.User.guid))
			{
				SessionConfig.DriverShiftLoaded = true;
				return;
			}

			Guid agentId = SessionConfig.Session.User.guid;
			DateTime today = DateTime.Today;
			try
			{
				List<AssignationContentModel>? assignations = await client.Assignations(today, 1);
				AssignationContentModel? mine = null;
				if (assignations is not null)
				{
					int i = 0;
					while (i < assignations.Count)
					{
						AssignationContentModel a = assignations[i];
						if (a.AgentId == agentId && a.Date.Date == today.Date)
						{
							mine = a;
							break;
						}

						i++;
					}
				}

				if (mine is null
					|| mine.TD
					|| string.IsNullOrWhiteSpace(mine.Definitive))
				{
					SessionConfig.DriverShiftLoaded = true;
					return;
				}

				PlansYearSlice? slice = await client.PlansTimeSlice(today, 1);
				WorkShiftTemplateCollectionModel? plan = slice?.GetPlan(today);
				if (plan is null)
				{
					Guid planId = await client.PlanHeader(today);
					if (!Guid.Empty.Equals(planId))
					{
						plan = await client.GetPlan(planId, today, onlyWork: true);
					}
				}

				bool festive = slice is not null && slice.GetFestive(today);
				WorkShiftTemplateModel? template = plan?.Template(mine.Definitive, today, festive);
				if (template is AttTemplateModel att && att.Content is not null)
				{
					SessionConfig.DriverShiftName = mine.Definitive;
					int c = 0;
					while (c < att.Content.Count)
					{
						if (att.Content[c] is TrainWorkShiftContentModel train
							&& !string.IsNullOrWhiteSpace(train.TrainId))
						{
							SessionConfig.DriverShiftTrainTokens.Add(train.TrainId.Trim());
						}

						c++;
					}
				}

				SessionConfig.DriverShiftLoaded = true;
				if (SessionConfig.HasDriverShiftToday)
				{
					mvarLogger.LogInformation(
						"Turno grafiado de hoy «{Shift}»: {Count} tren(es)",
						SessionConfig.DriverShiftName,
						SessionConfig.DriverShiftTrainTokens.Count);
				}
			}
			catch (Exception ex)
			{
				mvarLogger.LogWarning(ex, "No se pudo cargar el turno grafiado de hoy");
				SessionConfig.DriverShiftLoaded = true;
			}
		}

		#endregion Authentication

		#region Zafiro

		public async Task DoAeneasSync()
		{
			using (IServiceScope scope = mvarServiceProvider.CreateScope())
			{
				TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
				DBLocalSystem? localSystem = await db.LocalSystem.FirstOrDefaultAsync();
				if (null != localSystem)
				{
					localSystem.LastAeneasSync = DateTime.Now;
					await db.SaveChangesAsync();
				}
			}
		}

		public async Task DoSapphireDownload()
		{
			using (IServiceScope scope = mvarServiceProvider.CreateScope())
			{
				TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
				DBLocalSystem? localSystem = await db.LocalSystem.FirstOrDefaultAsync();
				if (null != localSystem)
				{
					localSystem.LastSapphireDownload = DateTime.Now;
					await db.SaveChangesAsync();
				}
			}
		}

		#endregion Zafiro
	}
}

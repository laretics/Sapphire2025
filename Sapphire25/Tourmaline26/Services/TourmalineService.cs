using BlazorBootstrap;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Crypto.Operators;
using Sapphire2025.Storage;
using Sapphire2025Models.Authentication;
using Sapphire2026.Data.Models;
using System.Runtime.ConstrainedExecution;
using System.Threading.Tasks;
using TimeNet2026.Production;
using TimeNet2026.Storage;
using TimeNet2026.Timed;
using TimeNet2026.Topo;
using TimeNet2026Data.DBStorage;
using Tourmaline26.Logic;
using Tourmaline26.Services.LocalDataModel;
using Tourmaline26.Services.TourmalineExperience;
using static System.Net.Mime.MediaTypeNames;
namespace Tourmaline26.Services
{
    /// <summary>
    /// Este es el procesador principal del sistema de información al viajero.
    /// Gestiona el modo, la ruta, la comunicación con MVB, etc.    
    /// </summary>
    public class TourmalineService
    {
        private SessionConfiguration mvarSessionConfig; //Contenedor de la configuración de la sesión actual.
        public SystemConfiguration SystemConfig { get; private set; } //Configuración del sistema (desde archivo de config)
        public DeviceCollection Devices { get; private set; } = new DeviceCollection();
        private ILogger<TourmalineService> mvarLogger;
        private IServiceProvider mvarServiceProvider;
        private IConfiguration mvarConfiguration;
        private LEDDisplayService mvarLedDisplayService;
        private TourmalineExperienceService mvarTourmalineExperienceService;
        //Almacén local TimeNet para poder "jugar" con la estructura en modo local sin sobrecargar las comunicaciones.
        public OnyxStorage TimeNetStorage { get; set; }

        public event EventHandler? PassengerUpdateRequested;
        public event EventHandler? HMIUpdateRequested;             

        public void RaiseHMIUpdate() => HMIUpdateRequested?.Invoke(this, EventArgs.Empty);
        public void RaisePassengerUpdate() => PassengerUpdateRequested?.Invoke(this, EventArgs.Empty);
        public TourmalineService(IConfiguration config,
        IServiceProvider serviceProvider,
        ILogger<TourmalineService> logger,
        LEDDisplayService ledDisplayService, 
        TourmalineExperienceService tourmalineExperience
        )
        {
			mvarSessionConfig = new SessionConfiguration();
			SystemConfig = new SystemConfiguration();
            TimeNetStorage = new OnyxStorage();
            mvarConfiguration = config;
            mvarLogger = logger;               
            mvarServiceProvider = serviceProvider;            
            mvarLedDisplayService = ledDisplayService;
            mvarTourmalineExperienceService = tourmalineExperience;
        }
		private async Task InitConfig(IConfiguration config)
		{           			
			SystemConfiguration? auxConfig = config.GetSection("SystemConfiguration").Get<SystemConfiguration>();            
			if (null != auxConfig)
				SystemConfig = auxConfig;
            IConfigurationSection debugStartupSession = config.GetSection("debugStartSession");
            if (debugStartupSession.Exists())
                debugStartupSession.Bind(mvarSessionConfig);
            await auxInitDevices(config);
        }
        private async Task auxInitDevices(IConfiguration config)
        {
            IConfigurationSection section = config.GetSection("Devices");
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
                    deviceSection["PublicId"]
                );
                deviceCount++;
            }
            mvarLogger.LogInformation("Total de dispositivos detectados: {DeviceCount}", deviceCount);
            mvarLedDisplayService.Init(Devices);
            await mvarLedDisplayService.Print("Serveis Ferroviaris de Mallorca",true);

            //await mvarLedDisplayService.ClearAsync();
            //await mvarLedDisplayService.PushAsync("Tourmaline 2026");
            //await PushLEDPanels("Tourmaline 2026. Iniciando estructura del programa " + DateTime.Now.ToString(),true,Alignment.None);
            //await PushBitmapLEDPanels("bmp//Tren81.bmp");
        }
        public SessionConfiguration SessionConfig
        {
            get => mvarSessionConfig;
        }
        /// <summary>
        /// Carga los valores de configuración desde la base de datos.
        /// </summary>
        /// <returns></returns>
        public async Task InitData()
        {
            await InitConfig(mvarConfiguration);
            await EnsureLocalDatabaseSchema(); //Recrea la BD si el esquema está desfasado respecto al modelo EF.
            await InitializeLocalRegister(); //Inicia el registro en la base de datos.
            await InitializeTimeNet(); //Carga los datos de TimeNet en el almacenamiento local.
        }

        public async Task<bool> EnsureInitialized()
        {
            if(!mvarSessionConfig.Initialized)
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
                    // Nunca dejamos el HMI bloqueado en "Iniciando sistema": la BD local se puede
                    // volver a descargar desde el menú de configuración (Ónice / Sapphire).
                    mvarSessionConfig.InitError = ex.Message;
                    mvarLogger.LogError(ex, "Error durante la inicialización. Se abre el HMI en modo degradado para permitir reconfiguración.");
                    if (null == mvarSessionConfig.TNEnvironment)
                        mvarSessionConfig.TNEnvironment = new TimeNetEnvironment(TimeNetStorage, Guid.Empty, Guid.Empty);
                }
                finally
                {
                    mvarSessionConfig.Initialized = true;
                }
            }
            return mvarSessionConfig.Initialized;
        }

        /// <summary>
        /// Garantiza que tourmaline.db existe y es compatible con el modelo EF actual.
        /// EnsureCreated no migra esquemas: si el modelo ha ganado columnas (p. ej. LastPlatformAssign)
        /// y la BD es antigua, se regenera vacía (los datos se vuelven a bajar de Zafiro/TimeNet).
        /// </summary>
        private async Task EnsureLocalDatabaseSchema()
        {
            using (IServiceScope scope = mvarServiceProvider.CreateScope())
            {
                TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
                await db.Database.EnsureCreatedAsync();
                try
                {
                    // Sonda de compatibilidad: consulta mínima sobre la entidad más propensa a crecer.
                    _ = await db.Trains.AsNoTracking().FirstOrDefaultAsync();
                    _ = await db.LocalSystem.AsNoTracking().FirstOrDefaultAsync();
                }
                catch (Exception ex) when (IsSqliteSchemaMismatch(ex))
                {
                    mvarLogger.LogWarning(ex,
                        "Esquema de tourmaline.db incompatible con el modelo actual. Regenerando base de datos local vacía (habrá que re-descargar datos).");
                    await db.Database.EnsureDeletedAsync();
                    await db.Database.EnsureCreatedAsync();
                }
            }
        }

        private static bool IsSqliteSchemaMismatch(Exception ex)
        {
            for (Exception? current = ex; null != current; current = current.InnerException)
            {
                if (current is Microsoft.Data.Sqlite.SqliteException sqlite)
                {
                    // Error 1 = SQLITE_ERROR (no such column / no such table, etc.)
                    string message = sqlite.Message;
                    if (message.Contains("no such column", StringComparison.OrdinalIgnoreCase)
                        || message.Contains("no such table", StringComparison.OrdinalIgnoreCase)
                        || sqlite.SqliteErrorCode == 1)
                        return true;
                }
            }
            return false;
        }

        public async Task InitializeTimeNet()
        {
            using (IServiceScope scope = mvarServiceProvider.CreateScope())
            {
                TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
                await db.Database.EnsureCreatedAsync(); //Asegura que la base de datos local está creada.
                await TimeNetStorage.DeserializeMemory(db);
				DBLocalSystem? localSystem = await db.LocalSystem.FirstOrDefaultAsync();
				Guid auxTopoStorageId = Guid.Empty;
				Guid auxRautaId = Guid.Empty;
				if (null != localSystem)
				{
					auxTopoStorageId = localSystem.CurrentTopoStorage;
					auxRautaId = localSystem.CurrentRauta;
				}
                mvarSessionConfig.TNEnvironment = new TimeNetEnvironment(TimeNetStorage, auxTopoStorageId, auxRautaId);

			}
        }

		/// <summary>
		/// Inicia el registro actual del tren en la base de datos local.
		/// </summary>
		/// <returns></returns>
		public async Task InitializeLocalRegister()
        {
            using (IServiceScope scope = mvarServiceProvider.CreateScope())
            {
                TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
                await db.Database.EnsureCreatedAsync(); //Asegura que la base de datos local está creada.
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
                            LastRautaSync = DateTime.MinValue,
                            LastTimeNetSync = DateTime.MinValue,
                            LastPlanSync = DateTime.MinValue,
                            CurrentPlan = string.Empty,
                            CurrentRauta = Guid.Empty,
                            CurrentTopoStorage = Guid.Empty
                        };
                        db.LocalSystem.Add(auxLocalSystem);
                    }
                    auxLocalSystem.TrainName = SystemConfig.Name;
                    auxLocalSystem.TrainId = auxTrain.Guid;
                    await db.SaveChangesAsync();
                    SystemConfig.TrainId = auxLocalSystem.TrainId;
                    SystemConfig.Name = auxLocalSystem.TrainName;
                }
            }
        }



		/// <summary>
		/// El sistema acaba de obtener los datos de TimeNet
		/// </summary>
		public async Task DoTimeNetSync()
		{
			using (IServiceScope scope = mvarServiceProvider.CreateScope())
			{
				TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
				DBLocalSystem? localSystem = await db.LocalSystem.FirstOrDefaultAsync();
				if (null != localSystem)
				{
					localSystem.LastTimeNetSync = DateTime.Now;
					await db.SaveChangesAsync();
				}
			}
		}
		/// <summary>
		/// El sistema acaba de seleccionar una topología para trabajar.
		/// </summary>
		public async Task DoTopologySelect(Guid topoId)
        {
            using (IServiceScope scope = mvarServiceProvider.CreateScope())
            {
                TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
                DBLocalSystem? localSystem = await db.LocalSystem.FirstOrDefaultAsync();
                if (null != localSystem)
                {
                    localSystem.LastTopoSync = DateTime.Now;
                    localSystem.LastRautaSync = DateTime.Now;
                    localSystem.LastPlanSync = DateTime.Now;
                    localSystem.CurrentTopoStorage = topoId;
                    localSystem.CurrentRauta = Guid.Empty; //Al cambiar de topología, se pierde la sección rauta seleccionada.
                    localSystem.CurrentPlan = string.Empty; //Al cambiar de rauta se pierde la selección de plan.
                    await db.SaveChangesAsync();
                }
            }
        }
        /// <summary>
        /// El sistema acaba de seleccionar un rauta para trabajar.
        /// </summary>
        public async Task DoRautatieSelect(Guid rautaId)
        {
            using (IServiceScope scope = mvarServiceProvider.CreateScope())
            {
                TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
                DBLocalSystem? localSystem = await db.LocalSystem.FirstOrDefaultAsync();
                if (null != localSystem)
                {
                    localSystem.LastRautaSync = DateTime.Now;
                    localSystem.CurrentRauta = rautaId;
                    localSystem.LastPlanSync = DateTime.Now;
					localSystem.CurrentPlan = string.Empty; //Al cambiar de rauta se pierde la selección de plan.
					await db.SaveChangesAsync();
                }
            }
        }

        /// <summary>
        /// El sistema acaba de seleccionar un plan de explotación
        /// </summary>
        /// <param name="planId"></param>
        public async Task DoPlanSelect(string planName)
        {
			using (IServiceScope scope = mvarServiceProvider.CreateScope())
			{
				TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
				DBLocalSystem? localSystem = await db.LocalSystem.FirstOrDefaultAsync();
				if (null != localSystem)
				{
					localSystem.LastPlanSync = DateTime.Now;
					localSystem.CurrentPlan = planName; //Al cambiar de rauta se pierde la selección de plan.
					await db.SaveChangesAsync();
				}
			}
		}

        public async Task DoCirculationSelect(Circulation rhs)
        {
            if (null != SessionConfig && null != SessionConfig.TNEnvironment)
            {
                SessionConfig.TNEnvironment.Circulation = rhs;
                UpdatePassengerInformationMode();

                if (null != rhs.Parent && null != rhs.Parent.asimilation)
                {
                    Console.WriteLine(rhs.Parent.asimilation.Name);
                    await RecallTourmalineExperience(rhs.Parent.asimilation);
                }                    
            }
        }

        /// <summary>
        /// Actualiza <see cref="SessionConfiguration.InformationMode"/> según la fase del viaje:
        /// sin circulación → Default; sin estación actual → Route (&lt;60 km/h) o Cruise (≥60);
        /// en estación → EndOfTrip si es destino final, NextStopInfo (Arriv) en el resto.
        /// En modo de servicio sin DemoMode no se sobreescribe (el radio del menú lateral manda);
        /// con DemoMode la conmutación es automática. Siempre se vuelve a Default al anular la circulación.
        /// </summary>
        public void UpdatePassengerInformationMode()
        {
            SessionConfiguration session = mvarSessionConfig;
            TimeNetEnvironment? enviro = session.TNEnvironment;
            Circulation? circulation = enviro?.Circulation;

            if (null == circulation)
            {
                if (session.InformationMode != Enums.PassengerInformationMode.Default)
                    session.InformationMode = Enums.PassengerInformationMode.Default;
                return;
            }

            // En modo de servicio el radio del menú manda, salvo DemoMode (conmutación automática).
            if (session.ServiceMode.Main && !session.ServiceMode.DemoMode)
                return;

            Enums.PassengerInformationMode next;
            Station? currentStation = enviro!.CurrentStation;

            if (null == currentStation)
            {
                // Umbral 60 km/h: por debajo lista de paradas; a partir de 60, crucero.
                next = session.CurrentSpeed < 60
                    ? Enums.PassengerInformationMode.NextStopsList
                    : Enums.PassengerInformationMode.Cruise;
            }
            else
            {
                Station? lastStation = enviro.Asimilation?.Destination
                    ?? circulation.Parent?.asimilation?.Destination;

                bool isEndOfTrip = null != lastStation
                    && string.Equals(currentStation.Id, lastStation.Id, StringComparison.Ordinal);

                next = isEndOfTrip
                    ? Enums.PassengerInformationMode.EndOfTrip
                    : Enums.PassengerInformationMode.NextStopInfo;
            }

            if (session.InformationMode != next)
                session.InformationMode = next;
        }

        /// <summary>
        /// Al seleccionar la asimilación cargará el itinerario en TourmalineExperience
        /// </summary>
        /// <param name="asimilation">Referencia a la asimilación seleccionada</param>
        /// <returns></returns>
        private async Task RecallTourmalineExperience(Asimilation asimilation)
        {
            mvarLogger.LogInformation($"Starting TourmalineExperience for asimilation {asimilation.Name}");
            //Detengo cualquier simulación en marcha.
            await mvarTourmalineExperienceService.Stop();

            LaunchRequest request = new LaunchRequest();
            request.Climate = 0; //Ajustaremos el clima con los valores reales durante la ejecución
            request.Consist = "Triple81"; //A cambiar cuando tengamos otro tipo de tren
            request.Now = DateTime.Now.ToString("HH:mm");
            request.RoutePath = asimilation.id;
            request.Route = "SFM"; //A cambiar si algún día hay otro escenario
            switch(DateTime.Now.Month)
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
            //Inicio TourmalineExperience con los datos actuales.
            await mvarTourmalineExperienceService.Launch(request);
        }

        /// <summary>
        /// Una vez finalizado el trayecto, pasa al modo de espera.
        /// </summary>
        /// <returns></returns>
        public async Task RecallEndTourmalineExperience()
        {
            mvarLogger.LogInformation("Ending TourmalineExperience");
            //Detengo cualquier simulación en marcha.
            await mvarTourmalineExperienceService.Stop();
        }

        /// <summary>
        /// Carga la colección de usuarios del tren desde la base de datos.
        /// </summary>
        /// <returns></returns>
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
			UserLoginModel modelo = new UserLoginModel();
			SessionModel? sesion = null;
			modelo.userName = username;
			modelo.password = pwd;
			using (IServiceScope scope = mvarServiceProvider.CreateScope())
			{
				AuthenticationClient auxCliente = scope.ServiceProvider.GetRequiredService<AuthenticationClient>();
				try
				{
					mvarLogger.LogInformation("Enviando credenciales para inicio de sesión de {User}", username);
					sesion = await auxCliente.Login(modelo);
				}
				catch (Exception ex)
				{
					mvarLogger.LogError("Fallo técnico en inicio de sesión de {User}: {Symptoms}", username, ex.Message);
				}
			}
			if (null != sesion)
			{
				//Actualizo los roles que tiene el usuario que acaba de abrir sesión.
				foreach (Sapphire2025Models.Common.UserRole rol in sesion.Roles)
				{
					mvarLogger.LogInformation("Usuario {User} tiene rol {Role}", username, rol.ToString());
					sesion.User.CredentialKey |= (byte)rol;
				}
			}
			SessionConfig.Session = sesion;
			return SessionConfig.Session;
		}
		public async Task UserLogout()
		{
			//Hay una sesión abierta. Queremos salir de la sesión
			if (null != SessionConfig.Session)
			{
				mvarLogger.LogInformation("Cierre de sesión de {User}",
				SessionConfig.Session.User.Name);
				using (IServiceScope scope = mvarServiceProvider.CreateScope())
				{
					AuthenticationClient auxCliente = scope.ServiceProvider.GetRequiredService<AuthenticationClient>();
					try
					{
						await auxCliente.Logout(SessionConfig.Session.Token.ToString());
					}
					catch (Exception ex)
					{
						mvarLogger.LogError("Fallo técnico en cierre de sesión de {User}: {Symptoms}",
						SessionConfig.Session.User.Name, ex.Message);
					}
				}
			}
			SessionConfig.Session = null; //En cualquier caso, cierro la sesión abierta.
		}
		#endregion Authentication

		#region Zafiro

		/// <summary>
		/// El sistema acaba de sincronizar con los datos de Aeneas
		/// </summary>
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

		/// <summary>
		/// El sistema acaba de sincronizar con los datos de Aeneas
		/// </summary>
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

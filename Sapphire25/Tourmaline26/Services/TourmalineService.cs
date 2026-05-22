using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Crypto.Operators;
using Sapphire2025.Storage;
using Sapphire2025Models.Authentication;
using Sapphire2026.Data.Models;
using TimeNet2026.Production;
using TimeNet2026.Storage;
using TimeNet2026.Timed;
using TimeNet2026.Topo;
using Tourmaline26.Logic;
using Tourmaline26.Services.LocalDataModel;
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
        //Almacén local TimeNet para poder "jugar" con la estructura en modo local sin sobrecargar las comunicaciones.
        public OnyxStorage TimeNetStorage { get; set; }

        public event EventHandler? PassengerUpdateRequested;
        public event EventHandler? HMIUpdateRequested;             

        public void RaiseHMIUpdate() => HMIUpdateRequested?.Invoke(this, EventArgs.Empty);
        public void RaisePassengerUpdate() => PassengerUpdateRequested?.Invoke(this, EventArgs.Empty);
        public TourmalineService(IConfiguration config,
        IServiceProvider serviceProvider,
        ILogger<TourmalineService> logger
        )
        {
			mvarSessionConfig = new SessionConfiguration();
			SystemConfig = new SystemConfiguration();			
            mvarLogger = logger;               
            mvarServiceProvider = serviceProvider;
            InitConfig(config);
            TimeNetStorage = new OnyxStorage();            
        }
		private void InitConfig(IConfiguration config)
		{           
			auxInitDevices(config);
			SystemConfiguration? auxConfig = config.GetSection("SystemConfiguration").Get<SystemConfiguration>();            
			if (null != auxConfig)
				SystemConfig = auxConfig;
            IConfigurationSection debugStartupSession = config.GetSection("debugStartSession");
            if (debugStartupSession.Exists())
                debugStartupSession.Bind(mvarSessionConfig);
		}
        private void auxInitDevices(IConfiguration config)
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
            await InitializeLocalRegister(); //Inicia el registro en la base de datos.
            await InitializeTimeNet(); //Carga los datos de TimeNet en el almacenamiento local.
        }

        public async Task<bool> EnsureInitialized()
        {
            if(!mvarSessionConfig.Initialized)
            {
                mvarLogger.LogInformation("Iniciando sistema de información al viajero Tourmaline...");
                await InitData();
                mvarSessionConfig.Initialized = true;
                mvarLogger.LogInformation("Sistema de información al viajero Tourmaline iniciado correctamente.");
            }
            return mvarSessionConfig.Initialized;
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
                string auxPlanName = string.Empty;
				if (null != localSystem)
				{
					auxTopoStorageId = localSystem.CurrentTopoStorage;
					auxRautaId = localSystem.CurrentRauta;
                    auxPlanName = localSystem.CurrentPlan;
				}
                mvarSessionConfig.TNEnvironment = new TimeNetEnvironment(TimeNetStorage, auxTopoStorageId, auxRautaId, auxPlanName);

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

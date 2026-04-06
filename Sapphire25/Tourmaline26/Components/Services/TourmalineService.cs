using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Crypto.Operators;
using Sapphire2025.Storage;
using Sapphire2025Models.Authentication;
using Sapphire2026.Data.Models;
using TimeNet2026.Storage;
using TimeNet2026.Timed;
using TimeNet2026.Topo;
using Tourmaline26.Components.Services.LocalDataModel;
using Tourmaline26.Components.Services.Logic;
namespace Tourmaline26.Components.Services
{
    /// <summary>
    /// Este es el procesador principal del sistema de información al viajero.
    /// Gestiona el modo, la ruta, la comunicación con MVB, etc.    
    /// </summary>
    public class TourmalineService
    {
        private bool mvarTourmalineInitialized = false; //Indica si el sistema ha terminado su inicialización.
        private SessionConfiguration mvarSessionConfig; //Contenedor de la configuración de la sesión actual.
        public SystemConfiguration SystemConfig { get; private set; }
        public DeviceCollection Devices { get; private set; } = new DeviceCollection();
        private ILogger<TourmalineService> mvarLogger;
        private IServiceProvider mvarServiceProvider;
        //private IConfiguration mvarConfig;
		private Timer? mvarTimer;
		public DateTime Now { get; set; } //Hora actual sincronizada para todos los paneles.
        public event EventHandler? PassengerUpdateRequested; //Ha ocurrido algo que requiere actualizar los TFT
        public event EventHandler? HMIUpdateRequested;
        //Almacén local TimeNet para poder "jugar" con la estructura en modo local sin sobrecargar las comunicaciones.
        public OnyxStorage TimeNetStorage { get; set; }
        public TourmalineService(IConfiguration config,
        IServiceProvider serviceProvider,
        ILogger<TourmalineService> logger
        )
        {
            auxInitDevices(config);
            mvarLogger = logger;   
            mvarSessionConfig = new SessionConfiguration();
            mvarServiceProvider = serviceProvider;
            TimeNetStorage = new OnyxStorage();
            SystemConfiguration? auxConfig = config.GetSection("SystemConfiguration").Get<SystemConfiguration>();
            if (null == auxConfig)
                SystemConfig = new SystemConfiguration();
            else
                SystemConfig = auxConfig;
            Now = DateTime.Now;
            mvarTimer = new Timer(UpdateClock, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }
        private void auxInitDevices(IConfiguration config)
        {
            //mvarConfig = config;
            IConfigurationSection section = config.GetSection("Devices");
            foreach (IConfigurationSection deviceSection in section.GetChildren())
            {
                DeviceMapped nuevo = new DeviceMapped();
                nuevo.SetParameters(
                    deviceSection["address"],
                    deviceSection["Type"],
                    deviceSection["Coach"],
                    deviceSection["Side"],
                    deviceSection["PublicId"]);
                Devices.Add(nuevo);
            }
        }
        private void UpdateClock(object? state)
        {
            this.Now = DateTime.Now;
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
            if(!mvarTourmalineInitialized)
            {
                mvarLogger.LogInformation("Iniciando sistema de información al viajero Tourmaline...");
                await InitData();
                mvarTourmalineInitialized = true;
                mvarLogger.LogInformation("Sistema de información al viajero Tourmaline iniciado correctamente.");
            }
            return mvarTourmalineInitialized;
        }
        public async Task InitializeTimeNet()
        {
            using (IServiceScope scope = mvarServiceProvider.CreateScope())
            {
                TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
                await db.Database.EnsureCreatedAsync(); //Asegura que la base de datos local está creada.
                await TimeNetStorage.DeserializeMemory(db);                
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
        public async Task<TopoStorage?> GetCurrentTopoStorage()
        {
            using (IServiceScope scope = mvarServiceProvider.CreateScope())
            {
                TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
                DBLocalSystem? localSystem = await db.LocalSystem.FirstOrDefaultAsync();
                if (null != localSystem && TimeNetStorage.Storages.ContainsKey(localSystem.CurrentTopoStorage))
                    return TimeNetStorage.Storages[localSystem.CurrentTopoStorage];
            }
            return null;
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
                    localSystem.CurrentTopoStorage = topoId;
                    localSystem.CurrentRauta = Guid.Empty; //Al cambiar de topología, se pierde la sección rauta seleccionada.
                    await db.SaveChangesAsync();
                }
            }
        }
        public async Task<Rauta?> GetCurrentRauta()
        {
            TopoStorage? auxStorage = await GetCurrentTopoStorage();
            if(null!=auxStorage)
            {
                using (IServiceScope scope = mvarServiceProvider.CreateScope())
                {
                    TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
                    DBLocalSystem? localSystem = await db.LocalSystem.FirstOrDefaultAsync();                  
                    if (null != localSystem)
                    {
                        if (auxStorage.ColRauta.ContainsKey(localSystem.CurrentRauta))
                            return auxStorage.ColRauta[localSystem.CurrentRauta];
                    }
                }
            }
            return null;
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
        public void RaiseEvents(bool passenger = false)
        {
            HMIUpdateRequested?.Invoke(this, EventArgs.Empty);
            if(passenger) PassengerUpdateRequested?.Invoke(this, EventArgs.Empty);
        }
        public void Dispose()
        {
            mvarTimer?.Dispose();
        }
    }
}

using Sapphire2025.Storage;
using Sapphire2025Models.Authentication;
using Sapphire2026.Data.Models;
using Tourmaline26.Components.Services.Logic;
using Microsoft.EntityFrameworkCore;
using Tourmaline26.Components.Services.LocalDataModel;
namespace Tourmaline26.Components.Services
{
    /// <summary>
    /// Este es el procesador principal del sistema de información al viajero.
    /// Gestiona el modo, la ruta, la comunicación con MVB, etc.    
    /// </summary>
    public class TourmalineService
    {
        private SessionConfiguration mvarSessionConfig; //Contenedor de la configuración de la sesión actual.
        private Enums.InformationLevel mvarCurrentInformationLevel = Enums.InformationLevel.Route;
        private ILogger<TourmalineService> mvarLogger;
        private IServiceProvider mvarServiceProvider;
        private IConfiguration mvarConfig;
		private Timer? mvarTimer;
		public DateTime Now { get; set; } //Hora actual sincronizada para todos los paneles.
        public event EventHandler? PassengerUpdateRequested; //Ha ocurrido algo que requiere actualizar los TFT
        public event EventHandler? HMIUpdateRequested;
        public DeviceCollection Devices { get; private set; } = new DeviceCollection();
        public SystemConfiguration SystemConfig{ get; private set; }
        public TourmalineService(IConfiguration config,
        IServiceProvider serviceProvider,
        ILogger<TourmalineService> logger)
        {
            auxInitDevices(config);
            mvarLogger = logger;   
            mvarSessionConfig = new SessionConfiguration();
            mvarServiceProvider = serviceProvider;
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
            mvarConfig = config;
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
            using (IServiceScope scope = mvarServiceProvider.CreateScope())
            {
                TourmalineContext db = scope.ServiceProvider.GetRequiredService<TourmalineContext>();
                Train? auxTrain = await db.Trains.FirstOrDefaultAsync(t => t.Name.Contains(SystemConfig.Name));
                DBLocalSystem? auxLocalSystem = await db.LocalSystem.FirstOrDefaultAsync();
                if(null!=auxTrain)
                {
                    if (null == auxLocalSystem)
                    {
                        auxLocalSystem = new DBLocalSystem{
                            LastSapphireDownload = DateTime.Now,
                            LastAeneasSync = DateTime.MinValue,
                            LastTopoSync = DateTime.MinValue,
                            LastRautaSync = DateTime.MinValue
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

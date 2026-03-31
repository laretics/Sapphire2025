using Tourmaline26.Components.Services.Logic;
using TimeNet2026.Models;
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
        
        private Timer? mvarTimer;
		public DateTime Now { get; set; } //Hora actual sincronizada para todos los paneles.
        public event EventHandler? PassengerUpdateRequested; //Ha ocurrido algo que requiere actualizar los TFT
        public event EventHandler? HMIUpdateRequested;
        public DeviceCollection Devices { get; private set; } = new DeviceCollection();
        public SystemConfiguration SystemConfig{ get; private set; }
        public TourmalineService(IConfiguration config)
        {
            auxInitDevices(config);
            mvarSessionConfig = new SessionConfiguration();
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

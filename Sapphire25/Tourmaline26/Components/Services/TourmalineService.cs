using Tourmaline26.Components.Services.Logic;

namespace Tourmaline26.Components.Services
{
    /// <summary>
    /// Este es el procesador principal del sistema de información al viajero.
    /// Gestiona el modo, la ruta, la comunicación con MVB, etc.    
    /// </summary>
    public class TourmalineService
    {
        private bool mvarServiceMode;
        public SystemConfiguration SystemConfig{ get; private set; }
        public TourmalineService(IConfiguration config)
        {
            SystemConfiguration? auxConfig = config.GetSection("SystemConfiguration").Get<SystemConfiguration>();
            if (null == auxConfig)
                SystemConfig = new SystemConfiguration();
            else
                SystemConfig = auxConfig;
        }
        public bool ServiceMode //Gestiona la entrada en el modo de servicio
        { 
            get => mvarServiceMode;
            set
            {
                if (mvarServiceMode != value)
                {
                    mvarServiceMode = value;
                    ServiceModeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        } 
        public DateTime Now { get; set; } //Hora actual sincronizada para todos los paneles.

        public event EventHandler? ServiceModeChanged;
    }
}

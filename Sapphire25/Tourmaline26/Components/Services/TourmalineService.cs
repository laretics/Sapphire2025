using Tourmaline26.Components.Services.Logic;
namespace Tourmaline26.Components.Services
{
    /// <summary>
    /// Este es el procesador principal del sistema de información al viajero.
    /// Gestiona el modo, la ruta, la comunicación con MVB, etc.    
    /// </summary>
    public class TourmalineService
    {
        private bool mvarServiceMode=false;
        private bool mvarEnabled = true;
        private bool mvarSoundEnabled = true; //Activación de altavoces.
        private bool mvarTFTEnabled = true; //Activación de monitores TFT.
        private bool mvarTeleindicatorsEnabled = true; //Activación de los paneles led.
        private bool mvarExternalTeleindicatorsEnabled = true; //Activación de los paneles de destino.
        private bool mvarAutoCameras = true; //Las cámaras sólo se activan en las paradas con habilitación de puertas.
        private bool mvarManualCameras = true; //Habilitación del botón de cámaras.
        private Enums.InformationLevel mvarCurrentInformationLevel = Enums.InformationLevel.Route;
        
        private Timer? mvarTimer;
		public DateTime Now { get; set; } //Hora actual sincronizada para todos los paneles.
        public event EventHandler? PassengerUpdateRequested; //Ha ocurrido algo que requiere actualizar los TFT
        public event EventHandler? HMIUpdateRequested;
		public SystemConfiguration SystemConfig{ get; private set; }
        public TourmalineService(IConfiguration config)
        {
            SystemConfiguration? auxConfig = config.GetSection("SystemConfiguration").Get<SystemConfiguration>();
            if (null == auxConfig)
                SystemConfig = new SystemConfiguration();
            else
                SystemConfig = auxConfig;
            Now = DateTime.Now;
            mvarTimer = new Timer(UpdateClock, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }
        private void UpdateClock(object? state)
        {
            this.Now = DateTime.Now;
        }
        public bool ServiceMode //Gestiona la entrada en el modo de servicio
        { 
            get => mvarServiceMode;
            set
            {
                if (mvarServiceMode != value)
                {
                    mvarServiceMode = value;
                    PassengerUpdateRequested?.Invoke(this, EventArgs.Empty);
                    HMIUpdateRequested?.Invoke(this, EventArgs.Empty);  
                }
            }
        }
        public bool MainPower //Conexión del sistema de información al viajero.
        {
            get => mvarEnabled;
            set
            {
                if (mvarEnabled != value)
                { 
                    mvarEnabled = value;
                    HMIUpdateRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        public bool SoundEnabled
        {
            get => mvarSoundEnabled;
            set
            {
                if (mvarSoundEnabled != value) 
                {
					mvarSoundEnabled = value;
                    HMIUpdateRequested ??= null;
				}				
			}            
        }
        public bool TeleindicatorsEnabled
        {
            get => mvarTeleindicatorsEnabled;
            set => mvarTeleindicatorsEnabled = value;
        }
        public bool ExternalTeleindicatorsEnabled
        {
            get => mvarExternalTeleindicatorsEnabled;
            set => mvarExternalTeleindicatorsEnabled = value;
        }
        public bool TFTEnabled
        {
            get => mvarTFTEnabled;
            set
            {
                if (mvarTFTEnabled != value)
                {
                    mvarTFTEnabled = value;
					PassengerUpdateRequested?.Invoke(this, EventArgs.Empty);
				}
            }
        }        
        public bool AutoCameras
        {
            get => mvarAutoCameras;
            set => mvarAutoCameras = value;
        }
        public bool ManualCameras
        {
            get => mvarManualCameras;
            set 
            {
                if(mvarManualCameras != value)
                {
                    mvarManualCameras = value;
                    HMIUpdateRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        public Enums.InformationLevel CurrentInformationLevel
        {
            get => mvarCurrentInformationLevel;
            set
            {
                if (mvarCurrentInformationLevel != value)
                {
                    mvarCurrentInformationLevel = value;
                    PassengerUpdateRequested?.Invoke(this, EventArgs.Empty);
                    HMIUpdateRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        public void Dispose()
        {
            mvarTimer?.Dispose();
        }
    }
}

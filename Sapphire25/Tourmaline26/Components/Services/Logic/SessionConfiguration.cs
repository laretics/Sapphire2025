using Sapphire2025Models.Authentication;
using TimeNet2026.Production;
namespace Tourmaline26.Components.Services.Logic
{
    /// <summary>
    /// Estos valores pertenecen a la sesión actual del tren.
    /// Es un contenedor que los engloba a todos.
    /// </summary>
    public class SessionConfiguration
    {
        public bool Initialized { get; set; } = false; //Indica si el sistema ha terminado de cargar los datos.
        public bool ServiceMode { get; set; } = false; //Indica si el sistema de información al viajero está en modo de servicio.
        public bool ServiceKeyboard { get; set; } = false; //Captura los eventos de teclado para simular sucesos de Onice.
        public bool MVBEnabled { get; set; } = true; //Estado del bus MVB.        
        public MVB8100Data? CurrentMVBData { get; set; }//Último paquete de datos recibido del bus MVB.
        public string MVBError { get; set; } = ""; //Mensaje de error relacionado con el bus MVB, si lo hubiera.
        public DateTime MVBLastUpdate { get; set; } = DateTime.MinValue; //Última vez que se recibió una actualización del bus MVB.
        public bool GPSEnabled { get; set; } = true; //Módulo de posicionamiento.
        public GPSData? CurrentGPSData{ get; set; } //Última lectura del GPS
        public bool GPSOK { get; set; } = true; //Indica si el GPS está recibiendo señal.
        public DateTime GPSLastUpdate { get; set; } = DateTime.MinValue; //Última vez que se recibió una actualización del GPS.
        public bool InternetEnabled { get; set; } = true; //Indica si el sistema tiene habilitada la conexión a internet.
        public bool InternetOK { get; set; } = true; //Indica si el sistema tiene conexión a internet.
        public bool PASEnabled { get; set; } = true; //Indica si el sistema de información al viajero está en modo de servicio.
        public bool SoundEnabled { get; set; } = true; //Indica si los altavoces están habilitados.
        public bool TFTEnabled { get; set; } = true; //Indica si los monitores TFT están habilitados.
        public bool TeleindicatorsEnabled { get; set; } = true; //Indica si los paneles led están habilitados.
        public bool ExternalTeleindicatorsEnabled { get; set; } = true; //Indica si los paneles de destino están habilitados.
        public bool AutoCameras { get; set; } = true; //Indica si las cámaras se activan automáticamente en las paradas con habilitación de puertas.
        public bool ManualCameras { get; set; } = true; //Indica si el botón de cámaras está habilitado.
        public bool PassengerCaretOnServiceMode { get; set; } = true; //Muestra la carta de ajuste en los monitores de viajeros cuando está en modo servicio.
        public bool PassengerScreenOnHMI{  get; set; }  = false; //Muestra el monitor de viajeros en el HMI (para ajustar el sistema con una sola pantalla)
        public float Temperature { get; set; } = 20.0f; //Temperatura interior del tren, leída de sensores o MVB

        #region Telemetria
        public int CurrentSpeed { get; set; } = 0; //Velocidad actual, leída de GPS o MVB
        public int CurrentLimitSpeed { get; set; } = 100; //Velocidad máxima del tren en este tramo
        public int CurrentNeutralSpeed { get; set; } = 0; //Velocidad objetivo calculada por ónice


		#endregion Telemetria

		public Enums.InformationLevel InformationLevel { get; set; } = Enums.InformationLevel.Route; //Nivel de información actual.
        public SessionModel? Session { get; set; } = null; //Información sobre el usuario actual
        public Dictionary<Guid, UserModelBase>? ColUsers{ get; set; } = null; //Colección de usuarios del tren, con su Guid de Zafiro como clave.
        public TimeNetEnvironment? TNEnvironment { get; set; }= null; //Todo lo que necesita el programa para mostrar una circulación
    }
}

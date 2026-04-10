using Sapphire2025Models.Authentication;
using TimeNet2026.Models;
namespace Tourmaline26.Components.Services.Logic
{
    /// <summary>
    /// Estos valores pertenecen a la sesión actual del tren.
    /// Es un contenedor que los engloba a todos.
    /// </summary>
    public class SessionConfiguration
    {
        public bool ServiceMode { get; set; } = false; //Indica si el sistema de información al viajero está en modo de servicio.
        public bool MVBEnabled { get; set; } = true; //Estado del bus MVB.        
        public MVB8100Data? CurrentMVBData { get; set; }//Último paquete de datos recibido del bus MVB.
        public string MVBError { get; set; } = ""; //Mensaje de error relacionado con el bus MVB, si lo hubiera.
        public DateTime MVBLastUpdate { get; set; } = DateTime.MinValue; //Última vez que se recibió una actualización del bus MVB.
        public bool GPSEnabled { get; set; } = true; //Módulo de posicionamiento.
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
        
        public Enums.InformationLevel InformationLevel { get; set; } = Enums.InformationLevel.Route; //Nivel de información actual.
        public SessionModel? Session { get; set; } = null; //Información sobre el usuario actual
        public Dictionary<Guid, UserModelBase>? ColUsers = null; //Colección de usuarios del tren, con su Guid de Zafiro como clave.
    }
}

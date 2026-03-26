namespace Tourmaline26.Components.Services.Logic
{
    /// <summary>
    /// Estos valores pertenecen a la sesión actual del tren.
    /// Es un contenedor que los engloba a todos.
    /// </summary>
    public class SessionConfiguration
    {
        public bool ServiceMode { get; set; } = false; //Indica si el sistema de información al viajero está en modo de servicio.
        public bool PASEnabled { get; set; } = true; //Indica si el sistema de información al viajero está en modo de servicio.
        public bool SoundEnabled { get; set; } = true; //Indica si los altavoces están habilitados.
        public bool TFTEnabled { get; set; } = true; //Indica si los monitores TFT están habilitados.
        public bool TeleindicatorsEnabled { get; set; } = true; //Indica si los paneles led están habilitados.
        public bool ExternalTeleindicatorsEnabled { get; set; } = true; //Indica si los paneles de destino están habilitados.
        public bool AutoCameras { get; set; } = true; //Indica si las cámaras se activan automáticamente en las paradas con habilitación de puertas.
        public bool ManualCameras { get; set; } = true; //Indica si el botón de cámaras está habilitado.
        public Enums.InformationLevel InformationLevel { get; set; } = Enums.InformationLevel.Route; //Nivel de información actual.
    }
}

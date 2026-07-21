namespace Tourmaline26.Logic
{
    public class ServiceMode
    {
        public bool Main { get; set; } //Activación del modo de servicio
        private bool mvarServiceKeyboard= false; //Captura los eventos de teclado para simular sucesos de Onice.
        private bool mvarGPSDummy = false;//Posicionamiento Fake para pruebas
        private bool mvarMVBDummy = false;//Emulación del bus MVB para probar interface.
        private bool mvarCaret = false; //Muestra la carta de ajuste en los monitores de viajeros cuando está en modo servicio.
        private bool mvarDemoMode = false; //Modo de demostración para enseñar las capacidades del sistema a tren parado.

        public bool ServiceKeyboard { get => Main&&mvarServiceKeyboard; set => mvarServiceKeyboard = Main; }
        public bool GPSDummy { get => Main && mvarGPSDummy; set => mvarGPSDummy = value; } 
        public bool MVBDummy { get => Main && mvarMVBDummy; set => mvarMVBDummy = value; }
        public bool MVBEnabled { get; set; } = true; //Estado del bus MVB.
        public bool DemoMode 
        { 
            get => mvarDemoMode; 
            set
            {
                mvarDemoMode = value;
                GPSDummy = value;
                MVBDummy = value;
            }
        }
        public bool PassengerCaretOnServiceMode { get => Main&&mvarCaret ; set => mvarCaret = value; }  
        public bool PassengerScreenOnHMI { get; set; } = false; //Muestra el monitor de viajeros en el HMI (para ajustar el sistema con una sola pantalla)        

    }
}

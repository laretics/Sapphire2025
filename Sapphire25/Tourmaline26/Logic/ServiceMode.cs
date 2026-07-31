namespace Tourmaline26.Logic
{
    /// <summary>
    /// Flags de modo de servicio / demo / emulación.
    /// Por defecto todo está desactivado salvo el bus MVB real (<see cref="MVBEnabled"/>).
    /// Los dummies solo se activan con Demo o por acción explícita del usuario.
    /// </summary>
    public class ServiceMode
    {
        private bool mvarMain;
        private bool mvarServiceKeyboard;
        private bool mvarGPSDummy;
        private bool mvarMVBDummy;
        private bool mvarMVBEnabled = true;
        private bool mvarCaret;
        private bool mvarDemoMode;

        public event Action<bool>? MVBEnabledChanged;

        /// <summary>Modo de servicio (opciones de mantenimiento / debug visibles).</summary>
        public bool Main
        {
            get => mvarMain;
            set
            {
                if (value == mvarMain) return;
                mvarMain = value;
                if (!value)
                {
                    // Al salir de servicio: demo y dummies no deben quedar "latentes".
                    ClearDemoAndDummies();
                    mvarServiceKeyboard = false;
                    mvarCaret = false;
                }
            }
        }

        public bool ServiceKeyboard
        {
            get => mvarMain && mvarServiceKeyboard;
            set => mvarServiceKeyboard = value;
        }

        /// <summary>
        /// Posicionamiento fake (Tourmaline Experience). Solo efectivo en modo de servicio.
        /// </summary>
        public bool GPSDummy
        {
            get => mvarMain && mvarGPSDummy;
            set => mvarGPSDummy = value;
        }

        /// <summary>
        /// Emulación del bus MVB. Solo efectivo en modo de servicio.
        /// Arranca en false; Demo o el usuario lo activan.
        /// </summary>
        public bool MVBDummy
        {
            get => mvarMain && mvarMVBDummy;
            set => mvarMVBDummy = value;
        }

        public bool MVBEnabled
        {
            get => mvarMVBEnabled;
            set
            {
                if (value == mvarMVBEnabled) return;
                mvarMVBEnabled = value;
                MVBEnabledChanged?.Invoke(value);
            }
        }

        /// <summary>
        /// Demostración a tren parado. Al activarse enciende GPSDummy y MVBDummy;
        /// al desactivarse los apaga (no deja residuales).
        /// </summary>
        public bool DemoMode
        {
            get => mvarDemoMode;
            set
            {
                if (value == mvarDemoMode) return;
                mvarDemoMode = value;
                if (value)
                {
                    mvarGPSDummy = true;
                    mvarMVBDummy = true;
                }
                else
                {
                    mvarGPSDummy = false;
                    mvarMVBDummy = false;
                }
            }
        }

        public bool PassengerCaretOnServiceMode
        {
            get => mvarMain && mvarCaret;
            set => mvarCaret = value;
        }

        /// <summary>Monitor de viajeros en el HMI (ajuste con una sola pantalla).</summary>
        public bool PassengerScreenOnHMI { get; set; }

        private void ClearDemoAndDummies()
        {
            mvarDemoMode = false;
            mvarGPSDummy = false;
            mvarMVBDummy = false;
        }
    }
}

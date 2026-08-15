using BlazorBootstrap;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;

namespace Tourmaline26.Logic.Generical
{
    /// <summary>
    /// Necesitaba una clase que contuviera toda la lógica de los botones de la barra de Tesla
    /// </summary>
    public class TeslaButton
    {
        private string[] mcolIcon;
        private Guid mvarId;
        public TeslaButton(string iconName, string? alternateIcon=null, bool toggled= false, EventCallback? callback= null, bool disabled = false, bool disabledOnMoving = false,string? comment=null)
        {
            mvarId = Guid.NewGuid();
            mcolIcon = new[] { iconName,iconName };
            if(null!=alternateIcon) mcolIcon[1]= alternateIcon;
			this.Comment = (null==comment)?string.Empty:comment;            
            this.IsToggle = toggled;
            this.Enabled = !disabled; //Por defecto, los botones están habilitados
            this.DisableOnMotion = disabledOnMoving;
            this.Callback = callback??EventCallback.Empty;
        }
        public Guid Id => mvarId;
        public void Press()
        {
            if (!this.Enabled) return;
            this.Pressed = true; 
        }
        public void DoToggle()
        {
			if (!this.Enabled)
				return;
			if (IsToggle)
				this.Selected = !this.Selected;
		}
        public void Release()
        {
            this.Pressed = false;
        }
        public bool SameIcon => mcolIcon[0] == mcolIcon[1];
        public string Icon 
        { 
            get
            {
                return mcolIcon[Selected ? 1 : 0];
            }
        }
        public string Comment { get; private set; }
        public EventCallback Callback { get; set; }
        public bool Enabled { get; set; }
        public bool DisableOnMotion { get; set; } //Este botón se debe deshabilitar en movimiento.
        public bool IsToggle { get; private set; }
        public bool Pressed { get; set; } //Pulsación del botón
        public bool Selected { get; set; } //Toggle activado

        /// <summary>
        /// Color forzado del icono (p. ej. amarillo de aviso al maquinista).
        /// Null = comportamiento normal según Selected/Enabled.
        /// </summary>
        public string? ForceColor { get; set; }

        /// <summary>
        /// Resalte visual adicional (halo) cuando el botón avisa de un estado activo.
        /// </summary>
        public bool Highlight { get; set; }

        public bool Glow
        {
            get => Enabled && Selected && (mcolIcon[0]==mcolIcon[1]);
        }
        public string ForegroundColor
        {
            get
            {
                if (Pressed)
                    return "var(--toolbar-button-foreground-selected)";
                if (!Enabled)
					return "var(--toolbar-button-foreground-disabled)";
                // Aviso prioritario (p. ej. anuncio a viajeros en curso).
                if (!string.IsNullOrEmpty(ForceColor))
                    return ForceColor;
                if (Selected && (mcolIcon[0] == mcolIcon[1]))
                    return "var(--toolbar-button-foreground-selected)";
                return "var(--toolbar-button-foreground)";
			}
        }
        public string Class
        { 
            get
            {
                if (mcolIcon[0] == mcolIcon[1])
                    return string.Format("{0}{1}{2}{3}"
                        , Pressed ? "pressed " : ""
                        , Enabled ? "" : "disabled "
                        , Selected ? "selected " : ""
                        , Highlight ? "highlight " : ""
                        );
                else
                    return string.Format("{0}{1}{2}"
                      , Pressed ? "pressed " : ""
                      , Enabled ? "" : "disabled "
                      , Highlight ? "highlight " : ""
                      );
			}
        }
    }
}

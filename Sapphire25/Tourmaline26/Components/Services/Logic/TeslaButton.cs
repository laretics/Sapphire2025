using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;

namespace Tourmaline26.Components.Services.Logic
{
    /// <summary>
    /// Necesitaba una clase que contuviera toda la lógica de los botones de la barra de Tesla
    /// </summary>
    public class TeslaButton
    {
        public TeslaButton(string iconName, string comment, bool isToggle = false)
        {
            mvarIcon = iconName;
            this.Comment = comment;
            this.IsToggle = isToggle;
            this.Enabled = true; //Por defecto, los botones están habilitados
        }
        public void Press()
        {
            this.Pressed = true; 
            if(IsToggle)
                this.Selected= !this.Selected;
        }
        public void Release()
        {
            this.Pressed = false;
        }
        private string mvarIcon;
        public string Icon 
        { 
            get
            {
                if (IsToggle && AlternateIcon != null)
                    return Selected ? AlternateIcon : mvarIcon;
                else
                    return mvarIcon;
            }
        }
        public string? AlternateIcon { get; set; } //Icono para mostrar imagen alternativa en toggle
        public string Comment { get; private set; }
        public EventCallback Callback { get; set; }
        public bool Enabled { get; set; }
        public bool IsToggle { get; private set; }
        public bool Pressed { get; set; } //Pulsación del botón
        public bool Selected { get; set; } //Toggle activado

        public string Class
        { 
            get
            {
                return string.Format("{0}{1}{2}"
                    , Pressed ? "pressed " : ""
                    , Enabled ? "" : "disabled "
                    , (null==AlternateIcon) && Selected ? "selected " : ""
                    );
            }
        }
    }
}

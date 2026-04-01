using Microsoft.AspNetCore.Components;

namespace Tourmaline26.Components.Services.Logic.Generical
{
    public class MenuOptionModel
    {
        public MenuOptionModel(string id,string title, RenderFragment? icon = null, bool serviceMode=false)
        {
            Id=id; Title=title; Icon=icon; ServiceMode = serviceMode;
        }
        public string Id { get; set; } 
        public string Title { get; set; } = "";
        public bool ServiceMode { get; set; } //Esta opción sólo es visible en el modo de servicio.
        public RenderFragment? Icon { get; set; }
    }
}

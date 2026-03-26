using Microsoft.AspNetCore.Components;

namespace Tourmaline26.Components.Services.Logic.Generical
{
    public class MenuOptionModel
    {
        public MenuOptionModel(string id,string title, RenderFragment? icon = null)
        {
            Id=id; Title=title; Icon=icon;
        }
        public string Id { get; set; } 
        public string Title { get; set; } = "";
        public RenderFragment? Icon { get; set; }
    }
}

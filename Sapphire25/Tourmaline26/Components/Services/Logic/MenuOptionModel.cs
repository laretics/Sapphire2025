using Microsoft.AspNetCore.Components;

namespace Tourmaline26.Components.Services.Logic
{
    public class MenuOptionModel
    {
        public MenuOptionModel(string title, RenderFragment? icon = null)
        {
            Title = title;
            Icon = icon;
        }

        public string Title { get; set; } = "";
        public RenderFragment? Icon { get; set; }
    }
}

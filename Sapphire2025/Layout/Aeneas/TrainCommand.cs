using Microsoft.AspNetCore.Components;

namespace Sapphire2025.Layout.Aeneas
{
	internal class TrainCommand
	{
		public string Label { get; set; } = "";
		public string Style { get; set; } = "outline-secondary";
		public string CssClass { get => string.Format("btn btn-{0}",Style);}
		public Func<bool> ShowIf { get; set; } = () => false;
		public Func<Task> Action { get; set; } = () => Task.CompletedTask;
		public RenderFragment? Icon { get; set; }
	}
}

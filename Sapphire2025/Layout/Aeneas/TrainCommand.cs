using Microsoft.AspNetCore.Components;

namespace Sapphire2025.Layout.Aeneas
{
	public class TrainCommand
	{
		public string Label { get; set; } = "";
		public string Style { get; set; } = "outline-secondary";
		public string CssClass { get => string.Format("btn btn-{0}",Style);}
		public string IconId { get; set; } = string.Empty;
		public string? IconColor{ get; set; }
		public bool Enabled { get; set; } = true;
		public Func<Task> Action { get; set; } = () => Task.CompletedTask;
	}
}

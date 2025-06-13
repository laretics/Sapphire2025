namespace Sapphire2025.Storage
{
	public class InteractiveService
	{
		public event Action? OnChange;
		public void NotifyStateChanged() => OnChange?.Invoke();
	}
}

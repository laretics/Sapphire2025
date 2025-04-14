using System;
namespace Sapphire2025.Storage
{
	/// <summary>
	/// Este servicio sirve para propagar los cambios que afectan a toda la interface desde
	/// formularios o fragmentos que están más hacia dentro.
	/// </summary>
	public class InteractiveService
	{
		public event Action? OnChange;
		private string? mvarCurrentUser;

		public string CurrentUserId
		{
			get => null==mvarCurrentUser?string.Empty:mvarCurrentUser;
			set
			{
				mvarCurrentUser = value;
				NotifyStateChanged();				
			}
		}
		private void NotifyStateChanged() => OnChange?.Invoke();
	}
}

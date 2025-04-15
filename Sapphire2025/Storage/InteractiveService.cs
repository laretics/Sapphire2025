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
		private Sapphire2025Models.Authentication.SessionModel? mvarSessionInfo;

		public Sapphire2025Models.Authentication.SessionModel? SessionInfo
		{
			get => mvarSessionInfo;
			set
			{
				mvarSessionInfo = value;
				NotifyStateChanged();
			}
		}
		private void NotifyStateChanged() => OnChange?.Invoke();
	}
}

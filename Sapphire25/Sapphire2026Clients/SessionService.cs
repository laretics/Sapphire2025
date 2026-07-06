using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2026Clients
{
	public class SessionService
	{
		public event Action? OnSessionExpired;

		private DateTime mvarLastActivityUtc = DateTime.UtcNow;
		private bool mvarExpiredNotified;

		public void RegisterActivity() => mvarLastActivityUtc = DateTime.UtcNow;

		public bool HasRecentActivity(TimeSpan window)
			=> DateTime.UtcNow - mvarLastActivityUtc < window;

		public void NotifyExpired()
		{
			if (mvarExpiredNotified) return;
			mvarExpiredNotified = true;
			OnSessionExpired?.Invoke();
		}

		public void ResetAfterLogin() => mvarExpiredNotified = false;
	}
}

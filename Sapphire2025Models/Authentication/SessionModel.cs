using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Authentication
{
	/// <summary>
	/// Información sobre una sesión abierta por el usuario a nivel cliente
	/// </summary>
	public class SessionModel
	{
		private Guid mvarToken;
		public Guid Token 
		{
			get => mvarToken;
			set
			{
				mvarToken = value;
			
			}
		}
		public UserModelBase User { get; set; }
		public List<Common.UserRole> Roles { get; set; }
		public DateTime ExpiryUtc{ get; set; } //Para sincronizar la caducidad con el cliente.

		public bool IsExpiredUtc() => ExpiryUtc <= DateTime.UtcNow;

		public SessionModel() 
		{ 
			Roles = new List<Common.UserRole>();
			User = new UserModelBase();
		}
		
	}
}

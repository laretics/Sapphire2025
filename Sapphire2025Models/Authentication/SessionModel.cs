using System;
using System.Collections.Generic;
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
		public Guid Token { get; set; }
		public UserModel User { get; set; }
		public List<Common.UserRole> Roles { get; set; }

		public SessionModel() 
		{ 
			Roles = new List<Common.UserRole>();
			User = new UserModel();
		}
	}
}

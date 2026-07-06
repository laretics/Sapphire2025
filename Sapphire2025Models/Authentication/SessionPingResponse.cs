using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Authentication
{
	public class SessionPingResponse
	{
		public bool IsValid { get; set; }
		public DateTime ExpiryUtc{ get; set; }
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Authentication
{
	public class TelegramBroadcastRequestModel
	{
		public string? Message{ get; set; }
		public bool Priority { get; set; } = false;
		public IEnumerable<Common.UserRole>? Roles { get; set; }
	}
}

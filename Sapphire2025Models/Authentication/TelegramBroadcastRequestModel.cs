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
		public string? Filters { get; set; }
		/// <summary>Si hay clave, el worker traduce por destinatario. Message queda como reserva en castellano.</summary>
		public string? CatalogKey { get; set; }
		public string[]? Args { get; set; }
	}
}

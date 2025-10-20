using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Authentication
{
	public class TelegramPairingRequestModel:BasicRequestModel
	{
		public Guid UserId { get; set; }
	}
}

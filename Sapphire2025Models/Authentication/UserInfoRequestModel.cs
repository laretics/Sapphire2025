using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Authentication
{
	public class UserInfoRequestModel:BasicRequestModel
	{
		public Guid UserId { get; set; } //Id del usuario del que pedimos información.
		public long TelegramId { get; set; } //TelegramId del usuario del que pedimos información.
		public UserInfoRequestModel(Guid token, Guid userId) : base(token)
		{
			this.UserId = userId;
		}
		public UserInfoRequestModel(Guid token, long telegramId):base(token)
		{
			this.TelegramId = telegramId;
		}
		public UserInfoRequestModel() : base()
		{
			this.UserId = Guid.Empty;
		}
	}
}

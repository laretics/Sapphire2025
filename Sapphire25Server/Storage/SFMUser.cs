using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sapphire25Server.Storage
{
	public class SFMUser
	{
		[Key]
		public string Id { get; set; }
		public string CF { get; set; } //Carnet ferroviario

		public long TelegramId { get; set; } //Código de chat de Telegram.

		public string UserName { get; set; }
		public string NormalizedUserName { get; set; }
		public string Email { get; set; }
		public string NormalizedEmail { get; set; }
		public bool EmailConfirmed {  get; set; }
		public string PasswordHash { get; set; }
		public string SecurityStamp { get; set; }

		public SFMUser()
		{
			Id = Guid.NewGuid().ToString();
			CF = string.Empty;
			TelegramId = 0;
			UserName = string.Empty;
		}

		[NotMapped]
		public bool hasTelegram { get => 0 != TelegramId; }
	}
}


/*
 * `aspnetusers`.`Id`, `aspnetusers`.`CF`, `aspnetusers`.`TelegramId`, `aspnetusers`.`UserName`, `aspnetusers`.`NormalizedUserName`, `aspnetusers`.`Email`, `aspnetusers`.`NormalizedEmail`, `aspnetusers`.`EmailConfirmed`, `aspnetusers`.`PasswordHash`, `aspnetusers`.`SecurityStamp`, `aspnetusers`.`ConcurrencyStamp`, `aspnetusers`.`PhoneNumber`, `aspnetusers`.`PhoneNumberConfirmed`, `aspnetusers`.`TwoFactorEnabled`, `aspnetusers`.`LockoutEnd`, `aspnetusers`.`LockoutEnabled`, `aspnetusers`.`AccessFailedCount`
 * */

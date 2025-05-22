using Microsoft.AspNetCore.Identity;
using Sapphire2025Server.Controllers;
using Sapphire2025Server.Models;
using System.Diagnostics;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Polling;
using Microsoft.EntityFrameworkCore;

namespace Sapphire2025Server.Telegram
{
	public class BotSoul
	{
		private TelegramBotClient mvarBot;
		private long mvarChatId; //Id del chat actual.
		private IConfiguration mvarConfig;


		public BotSoul (IConfiguration configuration)
		{
			mvarConfig = configuration;
			string? auxToken = mvarConfig["Telegram:Secret"];
			Debug.Assert(null != auxToken,"Valor nulo en token de Telegram desde Config");
			mvarBot = new TelegramBotClient(auxToken);
			CancellationTokenSource cts = new CancellationTokenSource();

			mvarChatId = -1;
			mvarBot.StartReceiving
				(
				HandleUpdateAsync,
				HandleErrorAsync,
				new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() },
				cancellationToken: cts.Token
				);
		}

		private async Task HandleUpdateAsync(ITelegramBotClient botClient,		
			Update update,
			CancellationToken cancellationToken)
		{
			if (update.Type == UpdateType.Message && update.Message is Message message)
			{
				Message mensaje = update.Message;
				Sapphire2025Server.Models.User? auxUser = await getUser(mensaje.Chat.Id);
				if(null==auxUser)
				{

				}
				else
				{

				}
				botClient.SendMessage(mensaje.Chat.Id,string.Format("Has puesto {0}", mensaje.Text));
			}
		}
		private Task HandleErrorAsync(ITelegramBotClient botClient,
			Exception exception,
			CancellationToken cancellationToken)
		{
			Debug.Assert(false, "Error en el bot de Telegram: " + exception.Message);
			return Task.CompletedTask;
		}

		/// <summary>
		/// Obtiene el usuario de Telegram a partir del id del chat.
		/// </summary>
		/// <param name="telegramChatId"></param>
		/// <returns></returns>
		private async Task <Sapphire2025Server.Models.User?> getUser(long telegramChatId)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				Sapphire2025Server.Models.User? auxUser = 
					await almacen.Users.FirstOrDefaultAsync (x => x.TelegramId == telegramChatId);
				if (null == auxUser)
					return null; //No existe un usuario con el chat emparejado todavía.
				else
					return auxUser;
			}
		}




		public async Task sendMessage(string rhs)
		{
			if (-1 == mvarChatId) return;
			await mvarBot.SendMessage(mvarChatId, rhs);
		}

		public async Task sendToSubscriptors(string rhs)
		{
			//using (ApplicationDbContext auxDb = new ApplicationDbContext())
			//{
			//	IQueryable<SFMUser> subscriptors = auxDb.Users.Where(f => f.TelegramId != 0);
			//	foreach (SFMUser s in subscriptors)
			//	{
			//		await mvarClient.SendMessage(s.TelegramId, rhs);
			//	}
			//}
		}

	}
}

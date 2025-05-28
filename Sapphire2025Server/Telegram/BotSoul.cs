using Microsoft.AspNetCore.Identity;
using Sapphire2025Server.Controllers;
using Sapphire2025Server.Models;
using System.Diagnostics;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Polling;
using Microsoft.EntityFrameworkCore;
using Sapphire2025Server.Telegram.Semantics;

namespace Sapphire2025Server.Telegram
{
	public class BotSoul
	{
		private TelegramBotClient mvarBot;
		private IConfiguration mvarConfig;
		private Dictionary<long,BotTask> mcolTasks = new Dictionary<long, BotTask>(); //Contenedor de conversaciones activas.


		public BotSoul (IConfiguration configuration)
		{
			mvarConfig = configuration;
			string? auxToken = mvarConfig["Telegram:Secret"];
			Debug.Assert(null != auxToken,"Valor nulo en token de Telegram desde Config");
			mvarBot = new TelegramBotClient(auxToken);
			CancellationTokenSource cts = new CancellationTokenSource();

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
				if(!mcolTasks.ContainsKey(mensaje.Chat.Id))
				{
					BotTask auxTask = new BotTask(mensaje.Chat.Id);
					BotTask.config = mvarConfig;
					await auxTask.InitializeAsync();
					mcolTasks.Add(mensaje.Chat.Id, auxTask);
				}				
				await mcolTasks[mensaje.Chat.Id].toBot(mensaje.Text);
				Response respuesta = await mcolTasks[mensaje.Chat.Id].fromBot();
				await botClient.SendMessage(mensaje.Chat.Id, respuesta.text);
			}
		}
		private Task HandleErrorAsync(ITelegramBotClient botClient,
			Exception exception,
			CancellationToken cancellationToken)
		{
			Debug.Assert(false, "Error en el bot de Telegram: " + exception.Message);
			return Task.CompletedTask;
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

		#region "Script de políticas de Telegram"
		/// <summary>
		/// El script de Telegram es un texto de configuración donde cada usuario puede
		/// personalizar el acceso que tiene a Telegram.
		/// </summary>		


		public static bool CanUseTelegram(string permissionsScript)
		{
			//TODO: Crear el código más adelante.
			return true;
		}
		#endregion"Script de políticas de Telegram"
	}
}

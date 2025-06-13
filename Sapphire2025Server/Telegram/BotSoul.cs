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
using System.Threading.Tasks.Dataflow;
using Sapphire2025Models;
using System.Threading.Tasks;
using Sapphire2025Models.Authentication;

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
				BotTask tarea = await (getTask(mensaje.Chat.Id));
				await tarea.toBot(mensaje.Text);
				Response respuesta = await tarea.fromBot();
				await botClient.SendMessage(mensaje.Chat.Id, respuesta.text);
			}
		}
		/// <summary>
		/// Al iniciar la aplicación, el bot carga la tabla de sesiones abiertas desde la
		/// base de datos.
		/// </summary>
		/// <returns></returns>
		public async Task InitUsers()
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				IEnumerable<ActiveSessionModel> sessions = await almacen.ActiveSessions.ToListAsync();
				foreach (ActiveSessionModel session in sessions)
				{
					await OpenTask(session.UserId);
				}
			}
		}

		/// <summary>
		/// Abre el usuario de Telegram asociado a una cuenta determinada.
		/// Esto hace que se inicie la suscripción a los mensajes del bot.
		/// </summary>
		/// <param name="userId"></param>
		public async Task<bool> OpenTask(Guid userId)
		{
			return await OpenTask(userId.ToString());
		}
		public async Task<bool> OpenTask(string userId)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				//Obtenemos el usuario a partir del guid.
				Sapphire2025Server.Models.User? usuario = await almacen.Users.Where(x => x.Id == userId).FirstOrDefaultAsync();
				if (usuario != null)
				{
					if (usuario.TelegramEnabled && 0 != usuario.TelegramId)
					{
						BotTask auxTarea = await getTask(usuario.TelegramId); //Ya con esto inicio el chat y abro las notificaciones broadcast.
						return true;
					}
				}
			}
			return false;
		}
		/// <summary>
		/// Cierra las notificaciones para este usuario. Se suele hacer en cierres de sesión.
		/// </summary>
		/// <param name="userId"></param>
		/// <returns></returns>
		public async Task<bool> CloseTask(Guid userId)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				//Obtenemos el usuario a partir del guid.
				Sapphire2025Server.Models.User? usuario = await almacen.Users.Where(x => x.Id == userId.ToString()).FirstOrDefaultAsync();
				if (usuario != null)
				{
					if(mcolTasks.ContainsKey(usuario.TelegramId))
					{
						mcolTasks.Remove(usuario.TelegramId);
						return true;
					}
				}
			}
			return false;
		}

		private async Task<BotTask> getTask(long telegramId)
		{
			if(!mcolTasks.ContainsKey(telegramId))				
			{
				BotTask salida = new BotTask(telegramId);
				BotTask.config = mvarConfig;
				await salida.InitializeAsync();
				mcolTasks.Add(telegramId, salida);
			}
			return mcolTasks[telegramId];
		}


		public async Task Broadcast(string message, Common.UserRole[] roles)
		{
			SapphireAuthenticationController auxController = new SapphireAuthenticationController(mvarConfig);
			foreach (BotTask task in mcolTasks.Values)
			{
				if (task.user.TelegramEnabled)
				{
					List<uint> auxColRoles = await auxController.retrieveUserRoles(task.user.guid);
					bool hasToNotificate = false;
					foreach (Common.UserRole role in roles)
					{
						if (auxColRoles.Contains((uint)role)) hasToNotificate = true; break;
					}
					if (hasToNotificate)
					{
						await mvarBot.SendMessage(task.user.TelegramId, message);
					}
				}
			}
		}
		private Task HandleErrorAsync(ITelegramBotClient botClient,
			Exception exception,
			CancellationToken cancellationToken)
		{
			Debug.Assert(false, "Error en el bot de Telegram: " + exception.Message);
			return Task.CompletedTask;
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

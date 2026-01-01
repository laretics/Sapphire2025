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
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace Sapphire2025Server.Telegram
{
	public class BotSoul
	{
		internal TelegramBotClient mvarBot;
		internal static IConfiguration config;
		private Dictionary<long,BotTask> mcolTasks = new Dictionary<long, BotTask>(); //Contenedor de conversaciones activas. Las conversaciones van por ID de telegram.	
		internal PairingQuew mvarPairingQuew = new PairingQuew();			

		public BotSoul (IConfiguration configuration)
		{
			config = configuration;
			string? auxToken = config["Telegram:Secret"];
			Debug.Assert(null != auxToken,"Valor nulo en token de Telegram desde Config");
			mvarBot = new TelegramBotClient(auxToken);
			CancellationTokenSource cts = new CancellationTokenSource();
			if (IsTelegramEnabled)
			{
				mvarBot.StartReceiving
					(
					HandleUpdateAsync,
					HandleErrorAsync,
					new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() },
					cancellationToken: cts.Token
					);
			}
		}

		/// <summary>
		/// Este valor está en el archivo config. Me dice si arranca el bot al arrancar el servicio.
		/// Lo uso, sobre todo, cuando tengo el servidor en producción y quiero desarrollar en pruebas.
		/// Si quiero trabajar con el bot en pruebas pongo este valor a "false" en el servicio y lo reinicio. Si quiero trabajar en algo que no sea el bot, pondré "false" en la configuración del equipo de pruebas.
		/// </summary>
		private bool IsTelegramEnabled { get => config["Telegram:Enabled"] == "true"; }		

		private async Task HandleUpdateAsync(ITelegramBotClient botClient,
			Update update,
			CancellationToken cancellationToken)
		{
			if (await GetTelegramEnabled()) //Sólo sigo si está habilitado el servidor
			{
				switch (update.Type)
				{
					case UpdateType.Message:
					case UpdateType.EditedMessage:
						if (update.Message is Message auxMensaje)
						{
							Debug.Assert(null != auxMensaje);
							await HandleIncomingMessage(botClient, auxMensaje);
						}			
						break;
					case UpdateType.ChannelPost:

						break;
					case UpdateType.EditedChannelPost:

						break;
					case UpdateType.MessageReaction:

						break;
					case UpdateType.Poll:
					default:

						break;
				}
			}
		}
		private async Task HandleIncomingMessage(ITelegramBotClient botClient, Message message)
		{
			long telegramId = message.Chat.Id;
			if (string.IsNullOrEmpty(message.Text))
				await botClient.SendMessage(telegramId, "Error interno: Mensaje vacío");
			else
			{
				BotTask auxTarea = OpenTask(telegramId);
				await auxTarea.TextToBot(message.Text);
				await auxTarea.ResponseFromBot();
			}
		}

			//if (update.Type == UpdateType.Message && update.Message is Message message)
			//{
			//	//Me llega un mensaje desde Telegram
			//	Message mensaje = update.Message;
			//	if (await GetTelegramEnabled())//Compruebo el estado del servidor en el registro de la base de datos.
			//	{
			//		BotTask tarea = OpenTask(mensaje.Chat.Id);
			//		if (null != mensaje && null != mensaje.Text)
			//		{
			//			if (await PairTelegramChat(mensaje.Chat.Id, mensaje.Text.ToUpper().Trim()))
			//			{
			//				//Usuario emparejado. Respondemos a la petición
			//				BotTask nueva = new BotTask(mensaje.Chat.Id, mvarPairingQuew);
			//				BotTask.config = mvarConfig;
			//				await nueva.InitializeAsync();
			//				mcolTasks.Add(mensaje.Chat.Id, nueva);
			//				await botClient.SendMessage(mensaje.Chat.Id, "¡Ya te tengo! Tu cuenta de Telegram está ahora emparejada con tu usuario en Zafiro.");
			//			}
			//			else
			//			{
			//				await botClient.SendMessage(mensaje.Chat.Id, "Ha ocurrido algún error. Esta cuenta de Telegram no se pudo vincular con ningún usuario de Zafiro.");
			//			}
			//		}
			//		else
			//		{
			//			await botClient.SendMessage(mensaje.Chat.Id, "Ha ocurrido algún error importante. Esta cuenta de Telegram no se pudo vincular con ningún usuario de Zafiro.");
			//		}					
			//	}
			//	else
			//	{
			//		await botClient.SendMessage(mensaje.Chat.Id, "Servidor desconectado.");
			//	}
			//}		



		/// <summary>
		/// Abre una conversación en base a una cuenta de Telegram
		/// </summary>
		/// <param name="telegramId">Id de diálogo de telegram</param>
		/// <returns>La conversación aludida</returns>
		internal BotTask OpenTask(long telegramId)
		{
			if(!mcolTasks.ContainsKey(telegramId))
			{
				BotTask nueva = new BotTask(telegramId, this);

				mcolTasks.Add(telegramId, nueva);
			}
			Debug.Assert(mcolTasks.ContainsKey(telegramId));
			return mcolTasks[telegramId];
		}
		/// <summary>
		/// Cierra las notificaciones para este usuario. Se suele hacer en cierres de sesión.
		/// </summary>
		/// <param name="userId"></param>
		/// <returns></returns>
		internal void CloseTask(long telegramId)
		{
			if (mcolTasks.ContainsKey(telegramId))
				mcolTasks.Remove(telegramId);
		}

		internal async Task EndTask(long telegramId)
		{
			//Elimina el diálogo porque vamos a desconectar al usuario.
			await mvarBot.SendMessage(telegramId, "Has desconectado tu usuario de este bot.");
			if (mcolTasks.ContainsKey(telegramId))
				mcolTasks.Remove(telegramId);
		}

		/// <summary>
		/// Envía el mensaje a todos los usuarios, independientemente de los permisos y el rol que tengan
		/// </summary>
		/// <param name="message">Texto para transmitir</param>
		/// <param name="priority">Si false, sólo lo envía a los usuarios que tengan Telegram 
		/// activo. Si true, se lo envía a todos urgentemente</param>
		/// <param name="filters">Sólo lo envía a aquellos que tengan alguno de estos parámetros en el campo "filter" de su consola Telegram
		public async Task BroadcastToAll(string message, bool priority, string filters="")
		{
			if(await GetTelegramEnabled())
			{
				List<Models.User> auxUsers = new List<Models.User>();
				List<Models.User> auxOrigin = await auxAvailableUsers();
				SapphireAuthenticationController auxController = new SapphireAuthenticationController(config, this);
				string[] auxFilters = filters.ToUpper().Split(',');
				foreach (Models.User candidato in auxOrigin)
				{
					if (candidato.TelegramEnabled || priority)
					{
						if (filters.Length > 0)
						{
							string auxConfig = await auxController.getTelegramRules(candidato.guid);
							if (auxHasFilterActive(auxConfig, auxFilters))
								auxUsers.Add(candidato);
						}
						else
							auxUsers.Add(candidato); //Si no hay filtros meto a todos los usuarios.
					}
				}
				await Broadcast(message, auxUsers);
			}
		}
		public async Task BroadcastByRole(string message, bool priority, Common.UserRole[] roles)
		{
			if(await GetTelegramEnabled())
			{
				List<Models.User> auxUsers = new List<Models.User>();
				List<Models.User> auxOrigin = await auxAvailableUsers();
				SapphireAuthenticationController auxController = new SapphireAuthenticationController(config, this);
				foreach (Models.User candidato in auxOrigin)
				{
					if (priority || candidato.TelegramEnabled)
					{
						List<uint> auxColRoles = await auxController.retrieveUserRoles(candidato.guid);
						bool notificate = false;
						foreach (Common.UserRole role in roles)
						{
							if (auxColRoles.Contains((uint)role))
							{
								notificate = true;
								break;
							}
						}
						if (!notificate)
						{
							string auxConfig = await auxController.getTelegramRules(candidato.guid);
							notificate = auxHasRolesInConfig(auxConfig, roles);
						}
						if (notificate)
							auxUsers.Add(candidato);
					}
				}
				await Broadcast(message, auxUsers);
			}
		}
		private bool auxHasFilterActive(string config, string[] filters)
		{
			foreach (string linea in config.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries))
			{
				if(linea.ToUpper().StartsWith("FILTER:"))
				{
					string[] auxBusca = auxReadValueFromKey(linea).Split(',');
					foreach(string palabra in auxBusca)
					{
						if (filters.Contains(palabra)) return true;
					}
				}
			}
			return false;
		}
		private bool auxHasRolesInConfig(string config, Common.UserRole[] roles)
		{
			//Buscamos la sección de roles
			//Formato: role:role1,role2,role3
			foreach (string linea in config.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries))
			{
				if (linea.ToUpper().StartsWith("ROLE:"))
				{
					string[] auxBusca = auxReadValueFromKey(linea).Split(',');
					foreach (string palabra in auxBusca)
					{
						if (roles.Count(x => x.ToString().ToUpper().Contains(palabra)) > 0)
							return true;
					}
				}
			}
			return false;
		}
		private string auxReadValueFromKey(string line)
		{
			int idx = line.IndexOf(':');
			if(idx>=0 && idx<line.Length-1)
			{
				return line.ToUpper().Substring(idx + 1).Trim();
			}
			return string.Empty;
		}

		private async Task<List<Models.User>> auxAvailableUsers()
		{
			using (DataStorage almacen = new DataStorage(config))
			{
				return await almacen.Users.Where(x => x.TelegramEnabled).ToListAsync();
			}				
		}
		private async Task Broadcast(string message, List<Models.User> users)
		{
			//Llamamos a esta función desde algún sitio donde comprobemos que el bot de Telegram está activo.
			foreach (Models.User usuario in users)
			{
				if(0!=usuario.TelegramId)
					await mvarBot.SendMessage(usuario.TelegramId,message);
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

		private async Task<bool> GetTelegramEnabled()
		{
			if(IsTelegramEnabled)
			{
				using (DataStorage almacen = new DataStorage(config))
				{
					string? auxCadena = await almacen.GetRegisterValue("Telegram", "false");
					if (null != auxCadena)
						return auxCadena.Equals("true");
				}
			}
			return false;
		}

		#endregion"Script de políticas de Telegram"

		///El emparejamiento ahora se hace mediante unos tickets de emparejamiento.
		///Un ticket de emparejamiento es una variable temporal que consiste en un número y el
		///guid de un usuario activo.
		///Si el sistema lee alguno de estos números detectará que es un emparejamiento y hará
		///el vínculo entre el el código de Telegram y el guid del usuario.
		public string GenerateTicket (Guid userId)
		{
			return mvarPairingQuew.GenerateNew(userId);
		}
		public async Task<bool> PairTelegramChat(long telegramChatId, string pairId)
		{
			Guid auxUserPairingId = mvarPairingQuew.getPairingUserId(pairId);
			if(Guid.Empty!=auxUserPairingId)
			{
				//Tenemos un emparejamiento. Buscamos el usuario en la base de datos
				SapphireAuthenticationController auxController = new SapphireAuthenticationController(config, this);
				return await auxController.pairUser(auxUserPairingId, telegramChatId);
			}
			return false;
		}



		
		

		
	}
}

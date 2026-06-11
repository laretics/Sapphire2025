
using Microsoft.EntityFrameworkCore;
using Sapphire2025.Storage;
using Sapphire2025Models;
using Sapphire2025Models.Authentication;
using Sapphire2026.Data;
using Sapphire2026.Data.Models;
using Sapphire2026Telegram;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TorchSharp.Modules;

namespace Sapphire2026Telegram
{
	public class BotSoul
	{
		internal TelegramBotClient? mvarBot;
		internal IConfiguration config;
		internal IServiceProvider services; //Referencias a los servicios inyectables.
		private Dictionary<long,BotTask> mcolTasks = new Dictionary<long, BotTask>(); //Contenedor de conversaciones activas. Las conversaciones van por ID de telegram.	
		internal PairingQuew mvarPairingQuew = new PairingQuew();
		internal Worker? mvarService;
		public static string DummyResponse { get; set; } = "";
		private Sapphire2025Models.Authentication.ExtendedUserModel? mvarDummyUser{ get; set; }
		private long mvarDummyUserTelegramId { get; set; } = -1;
		public static bool DummyMode { get; set; } = false;
		private readonly ILogger<BotSoul> mvarLogger;
		/// <summary>
		/// Este valor está en el archivo config. Me dice si arranca el bot al arrancar el servicio.
		/// Lo uso, sobre todo, cuando tengo el servidor en producción y quiero desarrollar en pruebas.
		/// Si quiero trabajar con el bot en pruebas pongo este valor a "false" en el servicio y lo reinicio. Si quiero trabajar en algo que no sea el bot, pondré "false" en la configuración del equipo de pruebas.
		/// </summary>
		private bool IsTelegramEnabled { get => config["TelegramBot:Enabled"] == "true"; }

		public BotSoul (ILogger<BotSoul> logger, IConfiguration configuration,IServiceProvider servicesProvider, Worker worker)
		{
			config = configuration;
			services = servicesProvider;
			mvarService = worker;
			mvarLogger = logger;
			DummyMode = false;
			mvarLogger.LogInformation("Iniciando bot de Telegram...");
			string? auxToken = config["TelegramBot:Secret"];
			Debug.Assert(null != auxToken, "Valor nulo en token de Telegram desde Config");
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
		public BotSoul(ILogger<BotSoul> logger, IConfiguration configuration,IServiceProvider servicesProvider)
		{
			config = configuration;
			services = servicesProvider;
			mvarLogger = logger;
			mvarLogger.LogInformation("Iniciando bot en modo consola...");
			DummyMode = true;
		}

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
				BotTask auxTarea = await OpenTask(telegramId);
				await auxTarea.TextToBot(message.Text);
				await auxTarea.ResponseFromBot();
			}
		}
		public async Task HandleDummyConsoleMessage(string text)
		{
			if(await RetrieveDummyUser())
			{
				Debug.Assert(null != mvarDummyUser);

				BotTask auxTarea = await OpenTask(mvarDummyUserTelegramId);
				await auxTarea.TextToBot(text);
				await auxTarea.ResponseFromBot();
			}

		}
		private async Task<bool> RetrieveDummyUser()
		{
			if(null==mvarDummyUser)
			{
				string? userId = config["DummyUserId"];
				string? auxTelegramId = config["DummyUserTelegramId"];
				if(null!=userId && null!=auxTelegramId)
				{
					Guid auxUserId = Guid.Parse(userId);
					mvarDummyUserTelegramId = long.Parse(auxTelegramId);
					AuthenticationClient auxClient = services.GetRequiredService<AuthenticationClient>();
					mvarDummyUser = await auxClient.userInfo(auxUserId);
				}
			}
			return null != mvarDummyUser;
		}


		/// <summary>
		/// Abre una conversación en base a una cuenta de Telegram
		/// </summary>
		/// <param name="telegramId">Id de diálogo de telegram</param>
		/// <returns>La conversación aludida</returns>
		internal async Task<BotTask> OpenTask(long telegramId)
		{
			if(!mcolTasks.ContainsKey(telegramId))
			{
				BotTask nueva = new BotTask(telegramId, this,config);
				await nueva.Initialize(); //Recupera el usuario de la base de datos.
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
				List<UserModel> auxUsers = new List<UserModel>();
				IEnumerable<UserModel> auxOrigin = await auxAvailableUsers(priority);
				string[] auxFilters = filters.ToUpper().Split(',');
				foreach (UserModel candidato in auxOrigin)
				{
					if (candidato.TelegramEnabled || priority)
					{
						if (filters.Length > 0)
						{
							if(null!=candidato.TelegramRules)
							{
								if(auxHasFilterActive(candidato.TelegramRules,auxFilters))
									auxUsers.Add(candidato);
							}
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
				List<UserModel> auxUsers = new List<UserModel>();
				IEnumerable<UserModel> auxOrigin = await auxAvailableUsers(priority);
				foreach (UserModel candidato in auxOrigin)
				{
					if (priority || candidato.TelegramEnabled)
					{
						//Localizo el chat de este usuario... si no está, lo genero.
						if(!mcolTasks.ContainsKey(candidato.TelegramId))
							await OpenTask(candidato.TelegramId);
						Debug.Assert(mcolTasks.ContainsKey(candidato.TelegramId));

						BotTask auxTask = mcolTasks[candidato.TelegramId];
						if (auxTask.user.MatchRole(roles))
							auxUsers.Add(candidato);
					}
				}
				await Broadcast(message, auxUsers, priority);
			}
		}
		private async Task Broadcast(string message, List<UserModel> users, bool includeOffline = false)
		{
			//Llamamos a esta función desde algún sitio donde comprobemos que el bot de Telegram está activo.
			if(await GetTelegramEnabled())
			{
				foreach (UserModel usuario in users)
				{

					if (0 != usuario.TelegramId && (includeOffline || usuario.TelegramEnabled))
					{
						try
						{
							await mvarBot.SendMessage(usuario.TelegramId, message);
						}
						catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 403)
						{
							// Usuario ha bloqueado el bot → lo ignoramos silenciosamente o lo registramos
							mvarLogger.LogWarning("Usuario {ChatId} ha bloqueado el bot", usuario.Name);
							// Opcional: marcar al usuario como bloqueado en tu base de datos
						}
						catch (Exception ex)
						{
							mvarLogger.LogError(ex, "Error enviando mensaje a {ChatId}", usuario.Name);
						}
					}
						
				}
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

		private async Task<IEnumerable<UserModel>> auxAvailableUsers(bool priority)
		{
			using IServiceScope scope = services.CreateScope();
			AuthenticationClient auxClient = scope.ServiceProvider.GetRequiredService<AuthenticationClient>();
			IEnumerable<UserModel>? salida = await auxClient.telegramUsersList(priority);
			if (null == salida) return new List<UserModel>();
			return salida;
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
				using IServiceScope scope = services.CreateScope();
				AuthenticationClient auxClient = scope.ServiceProvider.GetRequiredService<AuthenticationClient>();
				string? auxCadena = await auxClient.GetRegisterValue("Telegram", "false", Common.TelegramToken);
				if(null!=auxCadena)
					return auxCadena.Equals("true");			
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
				//SapphireAuthenticationController auxController = new SapphireAuthenticationController(config, this);
				//return await auxController.pairUser(auxUserPairingId, telegramChatId);
			}
			return false;
		}		
	}
}

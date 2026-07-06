
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
		private int BroadcastMaxDegreeOfParallelism
		{
			get
			{
				if (int.TryParse(config["TelegramBot:BroadcastParallelism"], out int value) && value > 0)
					return value;
				return 8;
			}
		}
		private int BroadcastMessagesPerMinute
		{
			get
			{
				if (int.TryParse(config["TelegramBot:BroadcastMessagesPerMinute"], out int value) && value > 0)
					return value;
				return 240;
			}
		}
		private TimeSpan BroadcastSendTimeout
		{
			get
			{
				if (int.TryParse(config["TelegramBot:BroadcastSendTimeoutSeconds"], out int value) && value > 0)
					return TimeSpan.FromSeconds(value);
				return TimeSpan.FromSeconds(10);
			}
		}
		private TimeSpan BroadcastMinimumGap { get => TimeSpan.FromMinutes(1d / BroadcastMessagesPerMinute); }
		private readonly object mvarBroadcastRateLock = new object();
		private DateTimeOffset mvarBroadcastNextAllowedSendUtc = DateTimeOffset.MinValue;

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
				List<UserModel> auxUsers = await BuildBroadcastRecipientsByFilter(priority, filters);
				await Broadcast(message, auxUsers, priority);
			}
		}
		public async Task BroadcastByRole(string message, bool priority, Common.UserRole[] roles)
		{
			if(await GetTelegramEnabled())
			{
				List<UserModel> auxUsers = await BuildBroadcastRecipientsByRole(priority, roles);
				await Broadcast(message, auxUsers, priority);
			}
		}
		private async Task Broadcast(string message, List<UserModel> users, bool includeOffline = false)
		{
			if (null == mvarBot || 0 == users.Count)
				return;

			ParallelOptions auxOptions = new ParallelOptions
			{
				MaxDegreeOfParallelism = BroadcastMaxDegreeOfParallelism
			};

			await Parallel.ForEachAsync(users, auxOptions, async (usuario, cancellationToken) =>
			{
				await SendBroadcastMessageToUser(usuario, message, includeOffline, cancellationToken);
			});
		}
		private async Task SendBroadcastMessageToUser(UserModel usuario, string message, bool includeOffline, CancellationToken cancellationToken)
		{
			if (0 == usuario.TelegramId || (!includeOffline && !usuario.TelegramEnabled) || null == mvarBot)
				return;

			try
			{
				await WaitForBroadcastSlotAsync(cancellationToken);
				await SendBroadcastMessageWithTimeout(usuario.TelegramId, message,usuario.Name??"Unknown", cancellationToken);
			}
			catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 403)
			{
				mvarLogger.LogWarning("Usuario {ChatId} ha bloqueado el bot", usuario.Name);
			}
			catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 429)
			{
				TimeSpan retryDelay = GetTelegramRetryDelay(ex) ?? TimeSpan.FromSeconds(30);
				mvarLogger.LogWarning("Telegram devolvió 429 para {ChatId}. Reintentando en {Delay}.", usuario.TelegramId, retryDelay);
				ApplyBroadcastCooldown(retryDelay);
				//await Task.Delay(retryDelay, cancellationToken);
				try
				{
					await WaitForBroadcastSlotAsync(cancellationToken);
					await SendBroadcastMessageWithTimeout(usuario.TelegramId,  message, usuario.Name??"Unknown", cancellationToken);
				}
				catch (Telegram.Bot.Exceptions.ApiRequestException retryEx) when (retryEx.ErrorCode == 429)
				{
					mvarLogger.LogWarning("Telegram volvió a devolver 429 para {ChatId} tras el reintento.", usuario.TelegramId);
				}
			}
			catch (TimeoutException)
			{
				mvarLogger.LogWarning("Timeout enviando mensaje a {ChatId}", usuario.TelegramId);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				mvarLogger.LogError(ex, "Error enviando mensaje a {ChatId}", usuario.Name);
			}
		}
		private async Task SendBroadcastMessageWithTimeout(long telegramId, string message, string userId,CancellationToken cancellationToken)
		{
			mvarLogger.LogDebug($"Broadcast to {telegramId} ({userId})");
			await mvarBot!.SendMessage(telegramId, message).WaitAsync(BroadcastSendTimeout, cancellationToken);
		}
		private async Task WaitForBroadcastSlotAsync(CancellationToken cancellationToken)
		{
			TimeSpan delay = TimeSpan.Zero;
			lock (mvarBroadcastRateLock)
			{
				DateTimeOffset auxNow = DateTimeOffset.UtcNow;
				if (auxNow < mvarBroadcastNextAllowedSendUtc)
				{
					delay = mvarBroadcastNextAllowedSendUtc - auxNow;
					mvarBroadcastNextAllowedSendUtc = mvarBroadcastNextAllowedSendUtc.Add(BroadcastMinimumGap);
				}
				else
				{
					mvarBroadcastNextAllowedSendUtc = auxNow.Add(BroadcastMinimumGap);
				}
			}

			if (delay > TimeSpan.Zero)
				await Task.Delay(delay, cancellationToken);
		}
		private void ApplyBroadcastCooldown(TimeSpan delay)
		{
			if (delay <= TimeSpan.Zero)
				return;

			lock (mvarBroadcastRateLock)
			{
				DateTimeOffset auxTarget = DateTimeOffset.UtcNow.Add(delay);
				if (auxTarget > mvarBroadcastNextAllowedSendUtc)
					mvarBroadcastNextAllowedSendUtc = auxTarget;
			}
		}
		private TimeSpan? GetTelegramRetryDelay(Telegram.Bot.Exceptions.ApiRequestException exception)
		{
			object? retryAfter = exception.GetType().GetProperty("RetryAfter")?.GetValue(exception);
			if (TryConvertRetryAfter(retryAfter, out TimeSpan auxDelay))
				return auxDelay;

			object? parameters = exception.GetType().GetProperty("Parameters")?.GetValue(exception);
			if (null != parameters)
			{
				object? nestedRetryAfter = parameters.GetType().GetProperty("RetryAfter")?.GetValue(parameters);
				if (TryConvertRetryAfter(nestedRetryAfter, out auxDelay))
					return auxDelay;
			}

			return null;
		}
		private bool TryConvertRetryAfter(object? value, out TimeSpan delay)
		{
			delay = TimeSpan.Zero;
			if (null == value)
				return false;

			if (value is TimeSpan auxSpan && auxSpan > TimeSpan.Zero)
			{
				delay = auxSpan;
				return true;
			}

			if (value is int auxSeconds && auxSeconds > 0)
			{
				delay = TimeSpan.FromSeconds(auxSeconds);
				return true;
			}

			if (int.TryParse(value.ToString(), out int parsedSeconds) && parsedSeconds > 0)
			{
				delay = TimeSpan.FromSeconds(parsedSeconds);
				return true;
			}

			return false;
		}
		private static bool UserMatchesRole(ExtendedUserModel? user, ISet<Common.UserRole> roles)
		{
			if (null == user) return false;

			foreach(ExtendedUserModel.RoleInfo auxInfo in user.roles.Values)
			{
				if (auxInfo.roleId < 256 && roles.Contains((Common.UserRole)auxInfo.roleId))
					return true;
			}
			return false;
		}

		private async Task<List<UserModel>> BuildBroadcastRecipientsByFilter(bool priority, string filters)
		{
			List<UserModel> auxUsers = new List<UserModel>();
			IEnumerable<UserModel> auxOrigin = await auxAvailableUsers(priority);
			HashSet<string>? auxFilters = BuildFilterSet(filters);
			foreach (UserModel candidato in auxOrigin)
			{
				if (!(candidato.TelegramEnabled || priority))
					continue;

				if (null == auxFilters)
				{
					auxUsers.Add(candidato);
					continue;
				}

				if (null != candidato.TelegramRules && auxHasFilterActive(candidato.TelegramRules, auxFilters))
					auxUsers.Add(candidato);
			}
			return auxUsers;
		}
		private async Task<List<UserModel>> BuildBroadcastRecipientsByRole(bool priority, Common.UserRole[] roles)
		{
			if (roles.Length < 1)
				return new List<UserModel>();

			HashSet<Common.UserRole> auxRoles = new HashSet<Common.UserRole>(roles);
			List<UserModel> auxUsers = new List<UserModel>();
			IEnumerable<UserModel> auxOrigin = await auxAvailableUsers(priority);

			using IServiceScope scope = services.CreateScope();
			AuthenticationClient auxClient = scope.ServiceProvider.GetRequiredService<AuthenticationClient>();

			foreach (UserModel candidato in auxOrigin)
			{
				if (!(priority || candidato.TelegramEnabled))
					continue;

				bool auxMatches;
				if (mcolTasks.TryGetValue(candidato.TelegramId, out BotTask? auxTask))
					auxMatches = auxTask.userContext.MatchRole(auxRoles);
				else
				{
					ExtendedUserModel? auxUser = await auxClient.userByTelegramId(candidato.TelegramId);
					auxMatches = UserMatchesRole(auxUser, auxRoles);
				}
				if (auxMatches)
					auxUsers.Add(candidato);
			}
			return auxUsers;
		}
		private HashSet<string>? BuildFilterSet(string filters)
		{
			if (string.IsNullOrWhiteSpace(filters))
				return null;

			HashSet<string> auxFilters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (string auxFilter in filters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			{
				if (!string.IsNullOrWhiteSpace(auxFilter))
					auxFilters.Add(auxFilter);
			}
			return auxFilters.Count == 0 ? null : auxFilters;
		}
		private bool auxHasFilterActive(string config, HashSet<string> filters)
		{
			foreach (string linea in config.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries))
			{
				if(linea.StartsWith("FILTER:", StringComparison.OrdinalIgnoreCase))
				{
					string[] auxBusca = auxReadValueFromKey(linea).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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

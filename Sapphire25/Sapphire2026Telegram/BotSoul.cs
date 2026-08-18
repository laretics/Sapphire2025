
using Microsoft.EntityFrameworkCore;
using Sapphire2025.Storage;
using Sapphire2025Models;
using Sapphire2025Models.Authentication;
using Sapphire2025Models.I18n;
using Sapphire2025Models.Preferences;
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
		private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (UiLocale Locale, DateTime Expires)> mcolLocaleCache = new();
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
		private TimeSpan BroadcastMediaSendTimeout
		{
			get
			{
				if (int.TryParse(config["TelegramBot:BroadcastMediaTimeoutSeconds"], out int value) && value > 0)
					return TimeSpan.FromSeconds(value);
				return TimeSpan.FromSeconds(90);
			}
		}
		private TimeSpan BroadcastMinimumGap { get => TimeSpan.FromMinutes(1d / BroadcastMessagesPerMinute); }
		private readonly object mvarBroadcastRateLock = new object();
		private DateTimeOffset mvarBroadcastNextAllowedSendUtc = DateTimeOffset.MinValue;
		// Debe vivir con la instancia del bot; un CTS local se puede cancelar al hacer GC/Dispose.
		private CancellationTokenSource? mvarReceiveCts;

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
			mvarReceiveCts = new CancellationTokenSource();
			if (IsTelegramEnabled)
			{
				mvarLogger.LogInformation("TelegramBot:Enabled=true. Arrancando StartReceiving...");
				mvarBot.StartReceiving
					(
					HandleUpdateAsync,
					HandleErrorAsync,
					new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() },
					cancellationToken: mvarReceiveCts.Token
					);
			}
			else
			{
				mvarLogger.LogWarning("TelegramBot:Enabled no es 'true'. El bot NO recibirá mensajes de Telegram.");
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
			BotTask auxTarea = await OpenTask(telegramId);
			if (!string.IsNullOrWhiteSpace(message.From?.LanguageCode))
			{
				auxTarea.userContext.HintLocale = UiLocales.Parse(message.From.LanguageCode);
				if (!auxTarea.userContext.Paired)
					await auxTarea.userContext.LoadLocaleAsync();
			}
			if (string.IsNullOrEmpty(message.Text))
				await botClient.SendMessage(telegramId, UiCatalog.Get(auxTarea.userContext.Locale, "tg.empty"));
			else
			{
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
			UiLocale locale = UiLocale.Spanish;
			if (mcolTasks.TryGetValue(telegramId, out BotTask? tarea) && tarea.userContext is not null)
				locale = tarea.userContext.Locale;
			await mvarBot.SendMessage(telegramId, UiCatalog.Get(locale, "tg.unpair"));
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
			await BroadcastToAll(new TelegramBroadcastRequestModel
			{
				Message = message,
				Priority = priority,
				Filters = filters
			});
		}

		public async Task BroadcastToAll(TelegramBroadcastRequestModel request)
		{
			if(await GetTelegramEnabled())
			{
				List<UserModel> auxUsers = await BuildBroadcastRecipientsByFilter(request.Priority, request.Filters ?? string.Empty);
				await Broadcast(request, auxUsers, request.Priority);
			}
		}
		public async Task BroadcastByRole(string message, bool priority, Common.UserRole[] roles)
		{
			await BroadcastByRole(new TelegramBroadcastRequestModel
			{
				Message = message,
				Priority = priority,
				Roles = roles
			});
		}

		public async Task BroadcastByRole(TelegramBroadcastRequestModel request)
		{
			if(await GetTelegramEnabled())
			{
				List<UserModel> auxUsers = await BuildBroadcastRecipientsByRole(request.Priority, request.Roles?.ToArray() ?? Array.Empty<Common.UserRole>());
				await Broadcast(request, auxUsers, request.Priority);
			}
		}

		public async Task BroadcastByRoleWithMedia(TelegramMediaBroadcastModel payload)
		{
			if (!await GetTelegramEnabled())
				return;

			List<UserModel> auxUsers = await BuildBroadcastRecipientsByRole(payload.Priority, payload.Roles ?? Array.Empty<Common.UserRole>());
			if (string.IsNullOrWhiteSpace(payload.MediaPath) || !System.IO.File.Exists(payload.MediaPath))
			{
				mvarLogger.LogWarning("Adjunto de broadcast no encontrado ({Path}). Se envía sólo texto.", payload.MediaPath);
				await Broadcast(ToBroadcastRequest(payload), auxUsers, payload.Priority);
				return;
			}

			await BroadcastMedia(payload, auxUsers);
		}
		private async Task Broadcast(TelegramBroadcastRequestModel request, List<UserModel> users, bool includeOffline = false)
		{
			if (null == mvarBot || 0 == users.Count)
				return;

			ParallelOptions auxOptions = new ParallelOptions
			{
				MaxDegreeOfParallelism = BroadcastMaxDegreeOfParallelism
			};

			await Parallel.ForEachAsync(users, auxOptions, async (usuario, cancellationToken) =>
			{
				string text = await ComposeBroadcastAsync(usuario, request.CatalogKey, request.Args, request.Message);
				await SendBroadcastMessageToUser(usuario, text, includeOffline, cancellationToken);
			});
		}

		private static TelegramBroadcastRequestModel ToBroadcastRequest(TelegramMediaBroadcastModel payload)
		{
			return new TelegramBroadcastRequestModel
			{
				Message = payload.Message,
				CatalogKey = payload.CatalogKey,
				Args = payload.Args,
				Priority = payload.Priority,
				Roles = payload.Roles
			};
		}

		private async Task<string> ComposeBroadcastAsync(UserModel usuario, string? catalogKey, string[]? args, string? fallback)
		{
			if (string.IsNullOrWhiteSpace(catalogKey))
				return fallback ?? string.Empty;
			UiLocale locale = await LocaleForUserAsync(usuario);
			return TelegramI18n.T(locale, catalogKey, args ?? Array.Empty<string>());
		}

		private async Task<UiLocale> LocaleForUserAsync(UserModel usuario)
		{
			string id = usuario.guid.ToString();
			if (mcolLocaleCache.TryGetValue(id, out (UiLocale Locale, DateTime Expires) hit)
				&& hit.Expires > DateTime.UtcNow)
				return hit.Locale;

			UiLocale locale = UiLocale.Spanish;
			try
			{
				using DataStorage almacen = new DataStorage(config);
				UserPreference? row = await almacen.UserPreferences
					.AsNoTracking()
					.FirstOrDefaultAsync(x => x.UserId == id && x.Key == PreferenceKeys.Locale);
				if (!string.IsNullOrWhiteSpace(row?.Value))
					locale = UiLocales.Parse(row.Value);
			}
			catch (Exception ex)
			{
				mvarLogger.LogDebug(ex, "No se pudo leer el idioma de {User}", usuario.Name);
			}

			mcolLocaleCache[id] = (locale, DateTime.UtcNow.AddMinutes(5));
			return locale;
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

		private async Task BroadcastMedia(TelegramMediaBroadcastModel payload, List<UserModel> users)
		{
			if (null == mvarBot || 0 == users.Count)
				return;

			ParallelOptions auxOptions = new ParallelOptions
			{
				MaxDegreeOfParallelism = BroadcastMaxDegreeOfParallelism
			};

			await Parallel.ForEachAsync(users, auxOptions, async (usuario, cancellationToken) =>
			{
				await SendBroadcastMediaToUser(usuario, payload, cancellationToken);
			});
		}

		private async Task SendBroadcastMediaToUser(UserModel usuario, TelegramMediaBroadcastModel payload, CancellationToken cancellationToken)
		{
			if (0 == usuario.TelegramId || (!payload.Priority && !usuario.TelegramEnabled) || null == mvarBot)
				return;

			string caption = await ComposeBroadcastAsync(usuario, payload.CatalogKey, payload.Args, payload.Message);
			try
			{
				await WaitForBroadcastSlotAsync(cancellationToken);
				await SendBroadcastMediaWithTimeout(usuario.TelegramId, payload, usuario.Name ?? "Unknown", cancellationToken, caption);
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
				try
				{
					await WaitForBroadcastSlotAsync(cancellationToken);
					await SendBroadcastMediaWithTimeout(usuario.TelegramId, payload, usuario.Name ?? "Unknown", cancellationToken, caption);
				}
				catch (Telegram.Bot.Exceptions.ApiRequestException retryEx) when (retryEx.ErrorCode == 429)
				{
					mvarLogger.LogWarning("Telegram volvió a devolver 429 para {ChatId} tras el reintento.", usuario.TelegramId);
				}
			}
			catch (TimeoutException)
			{
				mvarLogger.LogWarning("Timeout enviando multimedia a {ChatId}", usuario.TelegramId);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				mvarLogger.LogError(ex, "Error enviando multimedia a {ChatId}. Se intenta texto.", usuario.Name);
				try
				{
					await SendBroadcastMessageWithTimeout(usuario.TelegramId, caption, usuario.Name ?? "Unknown", cancellationToken);
				}
				catch (Exception textEx)
				{
					mvarLogger.LogError(textEx, "Tampoco se pudo enviar el texto a {ChatId}", usuario.Name);
				}
			}
		}

		private async Task SendBroadcastMediaWithTimeout(long telegramId, TelegramMediaBroadcastModel payload, string userId, CancellationToken cancellationToken, string? captionOverride = null)
		{
			mvarLogger.LogDebug("Broadcast media to {TelegramId} ({UserId}) kind={Kind}", telegramId, userId, payload.MediaKind);
			string caption = TruncateTelegramCaption(captionOverride ?? payload.Message);
			string fileName = string.IsNullOrWhiteSpace(payload.FileName)
				? Path.GetFileName(payload.MediaPath)
				: payload.FileName;

			try
			{
				await SendTelegramFile(telegramId, payload.MediaKind, payload.MediaPath!, fileName, caption)
					.WaitAsync(BroadcastMediaSendTimeout, cancellationToken);
			}
			catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 400)
			{
				mvarLogger.LogWarning(ex, "Telegram rechazó {Kind} para {ChatId}. Reintento como documento.", payload.MediaKind, telegramId);
				await SendTelegramFile(telegramId, "document", payload.MediaPath!, fileName, caption)
					.WaitAsync(BroadcastMediaSendTimeout, cancellationToken);
			}
		}

		private async Task SendTelegramFile(long telegramId, string kind, string path, string? fileName, string caption)
		{
			await using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			InputFile file = InputFile.FromStream(stream, fileName);
			string kindNorm = (kind ?? "document").Trim().ToLowerInvariant();
			switch (kindNorm)
			{
				case "photo":
					await mvarBot!.SendPhoto(telegramId, file, caption: caption);
					break;
				case "video":
					await mvarBot!.SendVideo(telegramId, file, caption: caption);
					break;
				case "animation":
					await mvarBot!.SendAnimation(telegramId, file, caption: caption);
					break;
				default:
					await mvarBot!.SendDocument(telegramId, file, caption: caption);
					break;
			}
		}

		private static string TruncateTelegramCaption(string message)
		{
			if (string.IsNullOrEmpty(message) || message.Length <= 1024)
				return message ?? string.Empty;
			return message.Substring(0, 1021) + "...";
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
			// Debug.Assert no sirve en producción: hay que dejar rastro en journalctl.
			mvarLogger.LogError(exception, "Error en el polling del bot de Telegram: {Message}", exception.Message);
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
			if (!IsTelegramEnabled)
				return false;

			try
			{
				using IServiceScope scope = services.CreateScope();
				AuthenticationClient auxClient = scope.ServiceProvider.GetRequiredService<AuthenticationClient>();
				string? auxCadena = await auxClient.GetRegisterValue("Telegram", "false", Common.TelegramToken);
				if (null != auxCadena)
					return auxCadena.Equals("true", StringComparison.OrdinalIgnoreCase);

				mvarLogger.LogWarning("GetRegisterValue('Telegram') devolvió null. Se asume deshabilitado.");
				return false;
			}
			catch (Exception ex)
			{
				// Antes esto tiraba el handler entero (p.ej. SessionService no registrado) y el bot parecía "muerto".
				mvarLogger.LogError(ex, "No se pudo consultar el flag remoto Telegram. Se asume deshabilitado.");
				return false;
			}
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

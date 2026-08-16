using Microsoft.AspNetCore.SignalR.Client;
using Sapphire2025Models;
using Sapphire2025Models.Authentication;
using Sapphire2026Telegram;

namespace Sapphire2026Telegram
{
	public class Worker : BackgroundService
	{
		private readonly ILogger<BotSoul> mvarLogger;
		private readonly HubConnection? mvarHubConnection;
		private readonly IConfiguration mvarConfiguration;
		internal readonly BotSoul mvarBotSoul;
		private readonly bool mvarHubEnabled;

	public Worker(ILogger<BotSoul> logger, HubConnection? hubConnection,IServiceProvider servicesProvider, IConfiguration configuration)
	{
		mvarLogger = logger;
		mvarHubConnection = hubConnection;
		mvarConfiguration = configuration;
		mvarBotSoul = new BotSoul(mvarLogger, mvarConfiguration,servicesProvider, this);
		mvarHubEnabled = mvarHubConnection != null &&
						 configuration.GetValue<bool>("SignalR:Enabled", false);
		
		mvarLogger.LogInformation("Worker inicializado. HubConnection es null: {IsNull}, SignalR Enabled en config: {ConfigEnabled}, Hub habilitado: {HubEnabled}",
			mvarHubConnection == null,
			configuration.GetValue<bool>("SignalR:Enabled", false),
			mvarHubEnabled);
	}

		public async override Task StartAsync(CancellationToken cancellationToken)
		{
			if (mvarHubEnabled && mvarHubConnection != null)
			{
				// Registrar handlers ANTES del try-catch
				mvarHubConnection.Reconnecting += er =>
				{
					_ = Task.Run(async () =>
					{
						try
						{
							await OnReconecting(er);
						}
						catch (Exception ex)
						{
							mvarLogger.LogError(ex, "Error en el handler de Reconnecting");
						}
					});
					return Task.CompletedTask;
				};
				mvarHubConnection.Reconnected += connectionId =>
				{
					_ = Task.Run(async () =>
					{
						try
						{
							await OnReconnected(connectionId);
						}
						catch (Exception ex)
						{
							mvarLogger.LogError(ex, "Error en el handler de Reconnected");
						}
					});
					return Task.CompletedTask;
				};
				mvarHubConnection.Closed += er =>
				{
					_ = Task.Run(async () =>
					{
						try
						{
							await OnClosed(er);
						}
						catch (Exception ex)
						{
							mvarLogger.LogError(ex, "Error en el handler de Closed");
						}
					});
					return Task.CompletedTask;
				};
				mvarHubConnection.On<long, bool>("TelegramMessageAcknowledged", (chatId, success) =>
				{
					_ = Task.Run(async () =>
					{
						try
						{
							await OnTelegramMessageAcknowelged(chatId, success);
						}
						catch (Exception ex)
						{
							mvarLogger.LogError(ex, "Error en el handler de TelegramMessageAcknowledged");
						}
					});
				});
				mvarHubConnection.On<string, string>("RequestTelegramPairingCode", (requestId, userId) =>
				{
					_ = Task.Run(async () =>
					{
						try
						{
							await OnRequestPairingCode(requestId, userId);
						}
						catch (Exception ex)
						{
							mvarLogger.LogError(ex, "Error en el handler de RequestTelegramPairingCode");
						}
					});
				});
				mvarHubConnection.On<TelegramBroadcastRequestModel>("ReceiveBroadcastRequest1", (request) =>
				{
					_ = Task.Run(async () =>
					{
						try
						{
							await OnTelegramBroadcast1(request);
						}
						catch (Exception ex)
						{
							mvarLogger.LogError(ex, "Error en el handler de ReceiveBroadcastRequest1");
						}
					});
				});
				mvarHubConnection.On<TelegramBroadcastRequestModel>("ReceiveBroadcastRequest2", (request) =>
				{
					_ = Task.Run(async () =>
					{
						try
						{
							await OnTelegramBroadcast2(request);
						}
						catch (Exception ex)
						{
							mvarLogger.LogError(ex, "Error en el handler de ReceiveBroadcastRequest2");
						}
					});
				});
				mvarHubConnection.On<TelegramMediaBroadcastModel>("ReceiveBroadcastRequestMedia", (payload) =>
				{
					_ = Task.Run(async () =>
					{
						try
						{
							await OnTelegramBroadcastMedia(payload);
						}
						catch (Exception ex)
						{
							mvarLogger.LogError(ex, "Error en el handler de ReceiveBroadcastRequestMedia");
						}
					});
				});
				// La conexión inicial puede fallar si el API aún no está listo (despliegue paralelo).
				// No abortamos el worker: ExecuteAsync reintentará StartAsync periódicamente.
				await TryConnectHubAsync(cancellationToken);
			}
			else
			{
				mvarLogger.LogInformation("SignalR deshabilitado. El servicio funcionará en modo standalone.");
			}

			await base.StartAsync(cancellationToken);
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				if (mvarHubEnabled && mvarHubConnection != null)
				{
					// WithAutomaticReconnect solo actúa tras una conexión inicial exitosa.
					// Si StartAsync falló al arrancar, o se agotaron los reintentos automáticos,
					// el estado queda en Disconnected y hay que volver a llamar a StartAsync.
					if (mvarHubConnection.State == HubConnectionState.Disconnected)
					{
						await TryConnectHubAsync(stoppingToken);
					}
					else if (mvarHubConnection.State == HubConnectionState.Connected)
					{
						if (mvarLogger.IsEnabled(LogLevel.Information))
						{
							mvarLogger.LogInformation("Worker running at: {time} [Modo: Conectado]", DateTimeOffset.Now);
						}
					}
					else
					{
						// Connecting / Reconnecting: esperar sin spamear ni llamar a StartAsync.
						mvarLogger.LogDebug("Hub en estado transitorio: {State}", mvarHubConnection.State);
					}
				}
				else
				{
					if (mvarLogger.IsEnabled(LogLevel.Information))
					{
						mvarLogger.LogInformation("Worker running at: {time} [Modo: Standalone]", DateTimeOffset.Now);
					}
				}

				await Task.Delay(10000, stoppingToken);
			}
		}

		/// <summary>
		/// Intenta StartAsync solo si el hub está desconectado. Seguro ante carreras de arranque/despliegue.
		/// </summary>
		private async Task TryConnectHubAsync(CancellationToken cancellationToken)
		{
			if (!mvarHubEnabled || mvarHubConnection == null)
				return;

			if (mvarHubConnection.State != HubConnectionState.Disconnected)
				return;

			try
			{
				mvarLogger.LogInformation("Intentando conectar al hub de SignalR ({0})...", mvarConfiguration["SignalR:HubUrl"]);
				await mvarHubConnection.StartAsync(cancellationToken);
				await OnConnected();
				mvarLogger.LogInformation("Conectado exitosamente al hub de SignalR.");
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				mvarLogger.LogWarning(ex, "No se pudo conectar con el hub de SignalR. Se reintentará en unos segundos.");
			}
		}

		/// <summary>
		/// Env�a un mensaje que hayamos recibido del bot al servidor a trav�s del hub SignalR.
		/// Nota importante: En este caso, el mensaje s�lo nos va a servir para sincronizar la recepci�n y
		/// hacer polling sobre la base de datos. Al recibir el mensaje, el servidor va a consultar la base
		/// de datos de mensajes recibidos para procesarlos.
		/// </summary>
		/// <param name="chatId">El Id del chat</param>
		/// <param name="message">El mensaje que acabamos de recibir de Telegram</param>
		/// <returns></returns>
		internal async Task SendMessageToHub(long chatId, string message)
		{
			if (mvarHubConnection?.State == HubConnectionState.Connected)
			{
				// Implementar el env�o al hub
				mvarLogger.LogInformation("Mensaje enviado al hub: ChatId={0}", chatId);
			}
			else
			{
				mvarLogger.LogDebug("Modo standalone: Mensaje no enviado al hub. ChatId={0}", chatId);
				// Aqu� podr�as implementar una l�gica alternativa, como guardar en base de datos local
			}
		}

		#region Handlers
		private async Task OnReconecting(System.Exception? er)
		{
			mvarLogger.LogWarning("Reconectando al hub de SignalR. Error: {0}", er?.Message);
		}
		private async Task OnReconnected(string? connectionId)
		{
			mvarLogger.LogInformation("Reconectado al hub de SignalR. ConnectionId={0}", connectionId);
		}
		private async Task OnConnected()
		{
			mvarLogger.LogInformation("Conectado al hub de SignalR. ConnectionId={0}", mvarHubConnection?.ConnectionId);
		}
		private async Task OnClosed(System.Exception? er)
		{
			mvarLogger.LogError("Conexi�n cerrada con el hub de SignalR. Error: {0}", er?.Message);
		}
		private async Task OnTelegramMessageAcknowelged(long chatId, bool success)
		{
			mvarLogger.LogInformation("Mensaje confirmado por el servidor: ChatId={0}, Success={1}", chatId, success);
		}
		private async Task OnTelegramBroadcast1(TelegramBroadcastRequestModel? request)
		{
			if (request is null)
				return;
			mvarLogger.LogInformation("Transmisión Telegram Message:{0} Priority:{1} Filters:{2} Key:{3}",
				request.Message, request.Priority, request.Filters, request.CatalogKey);
			await mvarBotSoul.BroadcastToAll(request);
		}
		private async Task OnTelegramBroadcast2(TelegramBroadcastRequestModel? request)
		{
			if (request is null)
				return;
			mvarLogger.LogInformation("Transmisión Telegram Message:{0} Priority:{1} Roles:{2} Key:{3}",
				request.Message, request.Priority, request.Roles, request.CatalogKey);
			await mvarBotSoul.BroadcastByRole(request);
		}
		private async Task OnTelegramBroadcastMedia(TelegramMediaBroadcastModel? payload)
		{
			if (payload is null || (string.IsNullOrWhiteSpace(payload.Message) && string.IsNullOrWhiteSpace(payload.CatalogKey)))
			{
				mvarLogger.LogWarning("Broadcast multimedia vacío o nulo.");
				return;
			}
			mvarLogger.LogInformation("Transmisión Telegram multimedia Kind:{0} Path:{1} Message:{2}",
				payload.MediaKind, payload.MediaPath, payload.Message);
			await mvarBotSoul.BroadcastByRoleWithMedia(payload);
		}
		private async Task OnRequestPairingCode(string requestId, string userId)
		{
			mvarLogger.LogInformation("Solicitud de c�digo de emparejamiento recibida: RequestId={0}, UserId={1}", requestId, userId);
			try
			{
				if (Guid.TryParse(userId, out Guid userGuid))
				{
					string pairingCode = mvarBotSoul.GenerateTicket(userGuid);
					//Env�o del c�digo al servidor.
					if (mvarHubConnection?.State == HubConnectionState.Connected)
					{
						await mvarHubConnection.InvokeAsync("SendPairingCodeResponse", requestId, pairingCode);
						mvarLogger.LogInformation("C�digo de emparejamiento {0} generado para el usuario {1}", pairingCode, userId);
					}
					else
					{
						mvarLogger.LogWarning("No se pudo enviar el c�digo de emparejamiento. Hub desconectado.");
					}
				}
				else
				{
					mvarLogger.LogError("User Id inv�lido: {0}", userId);
					if (mvarHubConnection?.State == HubConnectionState.Connected)
					{
						await mvarHubConnection.InvokeAsync("SendPairingCodeResponse", requestId, string.Empty);
					}
				}
			}
			catch (Exception e)
			{
				mvarLogger.LogError(e, "Error al generar el c�digo de emparejamiento");
				if (mvarHubConnection?.State == HubConnectionState.Connected)
				{
					await mvarHubConnection.InvokeAsync("SendPairingCodeResponse", requestId, string.Empty);
				}
			}
		}
		#endregion Handlers
	}
}
using Microsoft.AspNetCore.SignalR.Client;
using Sapphire2025Models;
using Sapphire2026Telegram;

namespace Sapphire2026Telegram
{
	public class Worker : BackgroundService
	{
		private readonly ILogger<Worker> mvarLogger;
		private readonly HubConnection? mvarHubConnection;
		private readonly IConfiguration mvarConfiguration;
		internal readonly BotSoul mvarBotSoul;
		private readonly bool mvarHubEnabled;

	public Worker(ILogger<Worker> logger, HubConnection? hubConnection, IConfiguration configuration)
	{
		mvarLogger = logger;
		mvarHubConnection = hubConnection;
		mvarConfiguration = configuration;
		mvarBotSoul = new BotSoul(mvarLogger, mvarConfiguration, this);
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
				mvarHubConnection.On<string, bool, string>("ReceiveBroadcastRequest1", (message, priority, filter) =>
				{
					_ = Task.Run(async () =>
					{
						try
						{
							await OnTelegramBroadcast1(message, priority, filter);
						}
						catch (Exception ex)
						{
							mvarLogger.LogError(ex, "Error en el handler de ReceiveBroadcastRequest1");
						}
					});
				});
				mvarHubConnection.On<string, bool, Common.UserRole[]>("ReceiveBroadcastRequest2", (message, priority, roles) =>
				{
					_ = Task.Run(async () =>
					{
						try
						{
							await OnTelegramBroadcast2(message, priority, roles);
						}
						catch (Exception ex)
						{
							mvarLogger.LogError(ex, "Error en el handler de ReceiveBroadcastRequest2");
						}
					});
				});
				try
				{
					await mvarHubConnection.StartAsync(cancellationToken);
					await OnConnected();
					mvarLogger.LogInformation("Conectado exitosamente al hub de SignalR.");
				}
				catch (Exception ex)
				{
					mvarLogger.LogWarning(ex, "No se pudo conectar con el hub de SignalR. El servicio continuará en modo standalone.");
					// No relanzamos la excepción para permitir que el servicio continúe en modo standalone
				}
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
				// Lugar para poner la lógica de recepción de mensajes del Telegram.
				if (mvarHubConnection?.State == HubConnectionState.Connected)
				{
					//Esto es una simulación de un evento.
					if (mvarLogger.IsEnabled(LogLevel.Information))
					{
						mvarLogger.LogInformation("Worker running at: {time} [Modo: Conectado]", DateTimeOffset.Now);
					}
				}
				else
				{
					if (mvarHubEnabled)
					{
						mvarLogger.LogWarning("No conectado al hub. Estado:{0}", mvarHubConnection?.State);
					}
					else
					{
						if (mvarLogger.IsEnabled(LogLevel.Information))
						{
							mvarLogger.LogInformation("Worker running at: {time} [Modo: Standalone]", DateTimeOffset.Now);
						}
					}
				}

				await Task.Delay(10000, stoppingToken);
			}
		}

		/// <summary>
		/// Envía un mensaje que hayamos recibido del bot al servidor a través del hub SignalR.
		/// Nota importante: En este caso, el mensaje sólo nos va a servir para sincronizar la recepción y
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
				// Implementar el envío al hub
				mvarLogger.LogInformation("Mensaje enviado al hub: ChatId={0}", chatId);
			}
			else
			{
				mvarLogger.LogDebug("Modo standalone: Mensaje no enviado al hub. ChatId={0}", chatId);
				// Aquí podrías implementar una lógica alternativa, como guardar en base de datos local
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
			mvarLogger.LogError("Conexión cerrada con el hub de SignalR. Error: {0}", er?.Message);
		}
		private async Task OnTelegramMessageAcknowelged(long chatId, bool success)
		{
			mvarLogger.LogInformation("Mensaje confirmado por el servidor: ChatId={0}, Success={1}", chatId, success);
		}
		private async Task OnTelegramBroadcast1(string message, bool priority = false, string filters = "")
		{
			mvarLogger.LogInformation("Transmisión Telegram Message:{0} Priority:{1} Filters:{2}", message, priority, filters);
			await mvarBotSoul.BroadcastToAll(message, priority, filters);
		}
		private async Task OnTelegramBroadcast2(string message, bool priority = false, params Common.UserRole[] roles)
		{
			mvarLogger.LogInformation("Transmisión Telegram Message:{0} Priority:{1} Roles:{2}", message, false, roles);
			await mvarBotSoul.BroadcastByRole(message, priority, roles);
		}
		private async Task OnRequestPairingCode(string requestId, string userId)
		{
			mvarLogger.LogInformation("Solicitud de código de emparejamiento recibida: RequestId={0}, UserId={1}", requestId, userId);
			try
			{
				if (Guid.TryParse(userId, out Guid userGuid))
				{
					string pairingCode = mvarBotSoul.GenerateTicket(userGuid);
					//Envío del código al servidor.
					if (mvarHubConnection?.State == HubConnectionState.Connected)
					{
						await mvarHubConnection.InvokeAsync("SendPairingCodeResponse", requestId, pairingCode);
						mvarLogger.LogInformation("Código de emparejamiento {0} generado para el usuario {1}", pairingCode, userId);
					}
					else
					{
						mvarLogger.LogWarning("No se pudo enviar el código de emparejamiento. Hub desconectado.");
					}
				}
				else
				{
					mvarLogger.LogError("User Id inválido: {0}", userId);
					if (mvarHubConnection?.State == HubConnectionState.Connected)
					{
						await mvarHubConnection.InvokeAsync("SendPairingCodeResponse", requestId, string.Empty);
					}
				}
			}
			catch (Exception e)
			{
				mvarLogger.LogError(e, "Error al generar el código de emparejamiento");
				if (mvarHubConnection?.State == HubConnectionState.Connected)
				{
					await mvarHubConnection.InvokeAsync("SendPairingCodeResponse", requestId, string.Empty);
				}
			}
		}
		#endregion Handlers
	}
}
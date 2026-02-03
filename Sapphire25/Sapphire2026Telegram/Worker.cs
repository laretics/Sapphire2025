using Microsoft.AspNetCore.SignalR.Client;

namespace Sapphire2026Telegram
{
	public class Worker : BackgroundService
	{
		private readonly ILogger<Worker> mvarLogger;
		private readonly HubConnection mvarHubConnection;

		public Worker(ILogger<Worker> logger, HubConnection hubConnection)
		{
			mvarLogger = logger;
			mvarHubConnection = hubConnection;
		}

		public async override Task StartAsync(CancellationToken cancellationToken)
		{
			//Aquí registramos los handlers para los eventos que pueden venirnos del hub.
			mvarHubConnection.Reconnecting += async er =>
			{ await OnReconecting(er); };
			mvarHubConnection.Reconnected += async connectionId =>
			{ await OnReconnected(connectionId); };
			mvarHubConnection.Closed += async er =>
			{ await OnClosed(er); };
			mvarHubConnection.On<long, bool>("TelegramMessageAcknowledged", async (chatId, success) =>
			{await OnTelegramMessageAcknowelged(chatId, success);});
			try
			{
				await mvarHubConnection.StartAsync(cancellationToken);
				await OnConnected();
			}
			catch (Exception ex)
			{
				mvarLogger.LogError(ex, "Error al conectar con el hub de SignalR");
				throw;
			}
			await base.StartAsync(cancellationToken);
		}
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				// Lugar para poner la lógica de recepción de mensajes del Telegram.
				if(mvarHubConnection.State== HubConnectionState.Connected)
				{
					//Esto es una simulación de un evento.
					if (mvarLogger.IsEnabled(LogLevel.Information))
					{
						mvarLogger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
					}
				}
				else
				{
					mvarLogger.LogWarning("No conectado al hub. Estado:{0}", mvarHubConnection.State);
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
			mvarLogger.LogInformation("Conectado al hub de SignalR. ConnectionId={0}", mvarHubConnection.ConnectionId);
		}
		private async Task OnClosed (System.Exception? er)
		{
			mvarLogger.LogError("Conexión cerrada con el hub de SignalR. Error: {0}", er?.Message);
		}
		private async Task OnTelegramMessageAcknowelged(long chatId, bool success)
		{
			mvarLogger.LogInformation("Mensaje confirmado por el servidor: ChatId={0}, Success={1}", chatId, success);
		}

		#endregion Handlers


	}
}

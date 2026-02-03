using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Configuration;
using System.Runtime.CompilerServices;

namespace Sapphire2025Server.Comunications
{
	public class SignalRHub:Hub
	{
		private readonly ILogger<SignalRHub> mvarLogger;

		public SignalRHub(ILogger<SignalRHub> logger)
		{
			mvarLogger = logger;
		}

		/// <summary>
		/// Método que invoca el cliente de Telegram cuando se recibe un mensaje.
		/// </summary>
		/// <param name="chatId">Id de la conversación</param>
		/// <param name="message">Mensaje en texto</param>
		/// <returns></returns>
		public async Task OnTelegramMessageReceived(long chatId, string message)
		{
			mvarLogger.LogInformation("📨 Mensaje de Telegram recibido: ChatId={0}, Message={1}", chatId, message);

			//Confirmación de recepción al cliente.
			await Clients.Caller.SendAsync("TelegramMessageAcknowledged", chatId, true);
		}

		/// <summary>
		/// Evento que se desencadena cuando nos conectamos con el cliente (el manejador de telegram).
		/// </summary>
		/// <returns></returns>
		public async override Task OnConnectedAsync()
		{
			mvarLogger.LogInformation("Cliente conectado al hub de SignalR: ConnectionId={0}", Context.ConnectionId);
			await base.OnConnectedAsync();
		}

		/// <summary>
		/// Evento que ocurre cuando el cliente se desconecta.
		/// </summary>
		/// <param name="exception"></param>
		/// <returns></returns>
		public override Task OnDisconnectedAsync(Exception? exception)
		{
			mvarLogger.LogWarning("El cliente {0} se ha desconectado. Razón: {1}", Context.ConnectionId, exception?.Message);
			return base.OnDisconnectedAsync(exception);
		}
	}
}

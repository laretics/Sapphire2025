using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sapphire2025Models;
using Sapphire2025Server.Controllers;
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
		/// El Worker de Telegram llama a esta función con el código de emparejamiento ya generado.
		/// </summary>
		/// <param name="requestId">Id de la petición original</param>
		/// <param name="pairingCode">Código de emparejamiento generado en el módulo Telegram</param>
		/// <returns></returns>
		public async Task SendPairingCodeResponse(string requestId, string pairingCode)
		{
			mvarLogger.LogInformation("Enviando código de emparejamiento al cliente: RequestId={0}, PairingCode={1}", requestId, pairingCode);
			await Clients.Caller.SendAsync("ReceivePairingCode", requestId, pairingCode);

			//Completamos la petición pendiente.
			SapphireBaseController.CompletePairingRequest(requestId, pairingCode);

			//Envío de la respuesta al servidor que lo solicitó.
			await Clients.All.SendAsync("PairingCodeGenerated", requestId, pairingCode); //No estoy seguro de que esta línea sea correcta
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


		public async Task BroadcastTelegramMessage1(string message, bool priority = false, string filters="")
		{
			mvarLogger.LogInformation("Broadcast: Message={0}, Priority={1}, Filters={2}", message, priority, filters);
			//Envío al cliente worker
			await Clients.All.SendAsync("ReceiveBroadcastRequest1", message, priority, filters);
		}
		public async Task BroadcastTelegramMessage2(string message, bool priority = false, params Common.UserRole[] roles)
		{
			mvarLogger.LogInformation("Broadcast: Message={0}, Priority={1}, Roles={2}", message, priority, roles);
			//Envío al cliente worker
			await Clients.All.SendAsync("ReceiveBroadcastRequest2", message, priority, roles);
		}

		public async Task BroadcastTelegramMedia(Sapphire2025Models.Authentication.TelegramMediaBroadcastModel payload)
		{
			mvarLogger.LogInformation("Broadcast media: Message={0}, Kind={1}, Path={2}",
				payload?.Message, payload?.MediaKind, payload?.MediaPath);
			await Clients.All.SendAsync("ReceiveBroadcastRequestMedia", payload);
		}
	}
}

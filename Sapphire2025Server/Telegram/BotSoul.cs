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
		private TelegramBotClient mvarBot;
		private IConfiguration mvarConfig;
		private Dictionary<long,BotTask> mcolTasks = new Dictionary<long, BotTask>(); //Contenedor de conversaciones activas.				

		public BotSoul (IConfiguration configuration)
		{
			mvarConfig = configuration;
			string? auxToken = mvarConfig["Telegram:Secret"];
			bool auxMainEnabled = mvarConfig["Telegram:Enabled"] == "true"; //Este valor está en el archivo config.
			//Este valor nos dice si arranca el demonio en esta instancia del servidor. Es útil cuando tengo el servidor en producción
			//y al mismo tiempo tengo el servidor de desarrollo en pruebas. (Sólo uno debería funcionar al mismo tiempo.
			Debug.Assert(null != auxToken,"Valor nulo en token de Telegram desde Config");
			mvarBot = new TelegramBotClient(auxToken);
			CancellationTokenSource cts = new CancellationTokenSource();
			if (auxMainEnabled)
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

		private async Task HandleUpdateAsync(ITelegramBotClient botClient,		
			Update update,
			CancellationToken cancellationToken)
		{					
			if (update.Type == UpdateType.Message && update.Message is Message message)
			{
				Message mensaje = update.Message;
				if (await GetTelegramEnabled())//Compruebo el estado del servidor en el registro de la base de datos.
				{
					BotTask tarea = await (getTask(mensaje.Chat.Id));
					await tarea.toBot(mensaje.Text);
					Response respuesta = await tarea.fromBot();
					await botClient.SendMessage(mensaje.Chat.Id, respuesta.text);
				}
				else
				{
					await botClient.SendMessage(mensaje.Chat.Id, "Servidor desconectado.");
				}
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
		/// <summary>
		/// Envía el mensaje a todos los usuarios, independientemente de los permisos y el rol que tengan
		/// </summary>
		/// <param name="message">Texto para transmitir</param>
		/// <param name="priority">Si false, sólo lo envía a los usuarios que tengan Telegram 
		/// activo. Si true, se lo envía a todos urgentemente</param>
		/// <param name="filters">Sólo lo envía a aquellos que tengan alguno de estos parámetros en el campo "filter" de su consola Telegram
		public async Task BroadcastToAll(string message, bool priority, string filters="")
		{
			List<Models.User> auxUsers = new List<Models.User>();
			List<Models.User> auxOrigin = await auxAvailableUsers();
			SapphireAuthenticationController auxController = new SapphireAuthenticationController(mvarConfig);
			string[] auxFilters = filters.ToUpper().Split(',');
			foreach (Models.User candidato in auxOrigin)
			{
				if(candidato.TelegramEnabled || priority)
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
		public async Task BroadcastByRole(string message, bool priority, Common.UserRole[] roles)
		{
			List<Models.User> auxUsers = new List<Models.User>();
			List<Models.User> auxOrigin = await auxAvailableUsers();
			SapphireAuthenticationController auxController = new SapphireAuthenticationController(mvarConfig);
			foreach (Models.User candidato in auxOrigin)
			{
				if(priority || candidato.TelegramEnabled)
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
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				return await almacen.Users.Where(x => x.TelegramEnabled).ToListAsync();
			}				
		}
		private async Task Broadcast(string message, List<Models.User> users)
		{
			if (!await GetTelegramEnabled()) return; //No voy a enviar mensajes si el bot no está habilitado.
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
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				string? auxCadena = await almacen.GetRegisterValue("Telegram", "false");
				if (null != auxCadena)
					return auxCadena.Equals("true");
			}
			return false;
		}

		#endregion"Script de políticas de Telegram"

		#region "Pairing"
		///El emparejamiento ahora se hace mediante unos tickets de emparejamiento.
		///Un ticket de emparejamiento es una variable temporal que consiste en un número y el
		///guid de un usuario activo.
		///Si el sistema lee alguno de estos números detectará que es un emparejamiento y hará
		///el vínculo entre el el código de Telegram y el guid del usuario.
		
		public async Task<bool> IsPairing(string rhs, long sessionId)
		{
			purgePairing();
			string comparer = rhs.Trim().ToUpper();
			foreach(PairingIntent candidato in mcolPairingIntents)
			{
				if(null!=candidato.pairingString)
				{
					if (candidato.pairingString.Equals(comparer))
					{
						//Hemos hecho un match.
						using (DataStorage almacen = new DataStorage(mvarConfig))
						{
							Models.User? auxUser = await almacen.Users.Where(x => x.guid == candidato.userId).FirstOrDefaultAsync();
							if(null!=auxUser)
							{


							}
						}
					}
				}				
			}
			return false;
		}
		
		public string SetNewPairing(Guid userId)
		{
			PairingIntent nuevo = new PairingIntent();
			nuevo.expiry = DateTime.Now.AddMinutes(10); //Expira en 10 minutos.
			nuevo.userId = userId;
			nuevo.pairingString = createPairingId();
			purgePairing();
			mcolPairingIntents.Add(nuevo);
			return nuevo.pairingString;
		}
		/// <summary>
		/// Elimina códigos de emparejamiento caducados.
		/// </summary>
		private void purgePairing()
		{
			List<PairingIntent> nueva = new List<PairingIntent>();
			foreach(PairingIntent candidato in mcolPairingIntents)
			{
				if (candidato.expiry > DateTime.Now)
					nueva.Add(candidato);
			}
			mcolPairingIntents = nueva;
		}
		private string createPairingId()
		{
			const string caracteres = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
			var random = new Random();
			return new string(Enumerable.Range(0, 4)
				.Select(_ => caracteres[random.Next(caracteres.Length)]).ToArray());
		}
		private List<PairingIntent> mcolPairingIntents = new List<PairingIntent>();		
		public class PairingIntent
		{
			public Guid userId { get; set; }
			public DateTime expiry { get; set; }
			public string? pairingString { get; set; }
		}

		#endregion "Pairing"
	}
}

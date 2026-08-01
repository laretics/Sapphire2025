using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Sapphire2025Models;
using Sapphire2025Server.Comunications;
using Sapphire2026.Data;
using Sapphire2026.Data.Models;
using System.Collections.Concurrent;
using System.Configuration;

namespace Sapphire2025Server.Controllers
{
	public abstract class SapphireBaseController : ControllerBase
	{
		protected readonly IHubContext<SignalRHub> mvarHubContext; //Referencia al hub.
		protected static readonly ConcurrentDictionary<string, TaskCompletionSource<string>> mvarPendingRequests 
			= new ConcurrentDictionary<string, TaskCompletionSource<string>>();
		internal TimeSpan EXPIRY_INTERVAL = new TimeSpan(4, 0, 0);
		internal IConfiguration mvarConfig;
		private const string VIP_TOKEN = "a77363a1-d47b-4d67-8f1e-9953597a7755";
		private Guid VIP_TOKEN_GUID = Guid.Parse(VIP_TOKEN);
		internal SapphireBaseController(IConfiguration config, IHubContext<SignalRHub> hubContext)
		{
			mvarConfig = config;
			mvarHubContext = hubContext;
		}

		public static void CompletePairingRequest(string requestId, string pairingCode)
		{
			if (mvarPendingRequests.TryRemove(requestId, out TaskCompletionSource<string>? tcs))
			{
				tcs.SetResult(pairingCode);
			}
		}

		/// <summary>
		/// Actualiza la caché de una tabla. Esto se hace asignando
		/// la fecha y hora actual a la clave especificada
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public async Task udateTableCache(Common.CacheTableKey key)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				TimeCache? auxCache = await almacen.TimeCache.Where(x => x.Key == (byte)key).FirstOrDefaultAsync();
				if (null == auxCache)
				{
					auxCache = new TimeCache();
					auxCache.Id = Guid.NewGuid();
					auxCache.Key = (byte)key;
					auxCache.TimeStamp = DateTime.UtcNow;
					almacen.TimeCache.Add(auxCache);
				}
				else
				{
					auxCache.TimeStamp = DateTime.UtcNow;
				}
				await almacen.SaveChangesAsync();
			}				
		}

		protected async Task<User?> userById(string userId)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				User? salida = await almacen.Users.Where(x => x.Id == userId).FirstOrDefaultAsync();
				return salida;
			}
		}
		protected async Task purgeSessions()
		{
			//Elimina las sesiones que hayan caducado.
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				IEnumerable<ActiveSessionModel> seleccion = almacen.ActiveSessions.Where(s => s.Expiry < DateTime.UtcNow);
				foreach (ActiveSessionModel elemento in seleccion)
				{
					//Añado un log de cierre de sesión por expiración.
					await addLoginRecord(elemento.UserId, Common.sessionEventType.sessionExpiry, elemento.HostIp);
				}
				almacen.ActiveSessions.RemoveRange(seleccion);
				await almacen.SaveChangesAsync();
			}
		}
		/// <summary>
		/// IP del cliente HTTP actual (o cadena vacía si no está disponible).
		/// </summary>
		protected string clientHostPoint()
		{
			try
			{
				return HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? string.Empty;
			}
			catch
			{
				return string.Empty;
			}
		}

		/// <summary>
		/// Registra un evento de actividad de usuario en SessionEvents.
		/// Conserva el nombre histórico addLoginRecord (login, logout y el resto de operaciones).
		/// </summary>
		protected async Task addLoginRecord(string userId, Common.sessionEventType type, string? hostPoint = null)
		{
			if (string.IsNullOrWhiteSpace(userId))
				return;

			await addSessionEventStatic(mvarConfig, userId, type, hostPoint ?? clientHostPoint());
		}

		/// <summary>
		/// Registra un evento a partir de un Guid de usuario ya resuelto.
		/// </summary>
		protected Task addLoginRecord(Guid userId, Common.sessionEventType type, string? hostPoint = null)
		{
			if (Guid.Empty.Equals(userId))
				return Task.CompletedTask;
			return addLoginRecord(userId.ToString(), type, hostPoint);
		}

		/// <summary>
		/// Versión estática para métodos static de controladores (Telegram, etc.).
		/// </summary>
		/// <summary>
		/// Límite práctico de hostPoint. En algunos despliegues la columna es VARCHAR corta
		/// (p. ej. 255); longtext en migraciones EF no garantiza el esquema real.
		/// </summary>
		protected const int SessionEventHostPointMaxLength = 255;

		protected static async Task addSessionEventStatic(
			IConfiguration config,
			string userId,
			Common.sessionEventType type,
			string hostPoint = "")
		{
			if (string.IsNullOrWhiteSpace(userId) || null == config)
				return;

			string safeHost = TruncateHostPoint(hostPoint);

			using (DataStorage almacen = new DataStorage(config))
			{
				SessionEvent nuevo = new SessionEvent
				{
					Id = Guid.NewGuid().ToString(),
					userId = userId,
					type = type,
					hostPoint = safeHost,
					timeSpan = DateTime.UtcNow
				};
				almacen.SessionEvents.Add(nuevo);
				await almacen.SaveChangesAsync();
			}
		}

		protected static string TruncateHostPoint(string? hostPoint)
		{
			if (string.IsNullOrEmpty(hostPoint))
				return string.Empty;
			if (hostPoint.Length <= SessionEventHostPointMaxLength)
				return hostPoint;
			return hostPoint.Substring(0, SessionEventHostPointMaxLength);
		}

		protected static Task addSessionEventStatic(
			IConfiguration config,
			Guid userId,
			Common.sessionEventType type,
			string hostPoint = "")
		{
			if (Guid.Empty.Equals(userId))
				return Task.CompletedTask;
			return addSessionEventStatic(config, userId.ToString(), type, hostPoint);
		}
		protected async Task<bool> hasBasicPermission(Guid tokenId, Common.UserRole role)
		{
			if (Guid.Empty.Equals(tokenId)) return false;
			if (VIP_TOKEN_GUID.Equals(tokenId)) return true;
			if (Common.TelegramToken.Equals(tokenId)) return true; //Petición desde Telegram.

			ActiveSessionModel? auxSession = await retrieveSession(tokenId);
			if (null != auxSession)
			{
				//A partir de la sesión saco los roles del usuario.
				List<Common.UserRole> roles = await retrieveBasicRoles(auxSession.UserId);
				return roles.Contains(role);
			}		
			return false;
		}
		protected async Task<List<uint>> retrieveUserRoles(string userId)
		{
			List<uint> salida = new List<uint>();
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				List<UserAndRole> roles = await almacen.UserAndRoles.Where(x => x.UserId == userId).ToListAsync();
				foreach (UserAndRole rol in roles)
					salida.Add(rol.RoleId);
			}
			return salida;
		}

		protected async Task<List<Common.UserRole>> retrieveBasicRoles(String userId)
		{
			List<Common.UserRole> salida = new List<Common.UserRole>();
			List<uint> auxRoles = await retrieveUserRoles(userId);
			foreach(uint rol in auxRoles)
			{
				if (rol < 8)
				{
					Common.UserRole auxRol = (Common.UserRole)rol;
					if(!salida.Contains(auxRol))
						salida.Add(auxRol);
				}					
			}	
			//Gestionamos el administrador.
			//Si el usuario tiene rol de administrador, damos de alta todos los roles menores de 8.
			if(salida.Contains(Common.UserRole.Root))
			{
				salida= retrieveAllRoles();
			}
			return salida;
		}

		/// <summary>
		/// Devuelve todos los roles enumerados (para usuarios root o vip)
		/// </summary>
		/// <returns></returns>
		private List<Common.UserRole> retrieveAllRoles()
		{
			return new List<Common.UserRole>
				{ Common.UserRole.Anonymous,
					Common.UserRole.Inspector,
					Common.UserRole.Expert,
					Common.UserRole.Oficial,
					Common.UserRole.Mechanic,
					Common.UserRole.Root,
					Common.UserRole.Engineer};
		}

		protected async Task<bool> hasBasicPermission(BasicRequestModel request, Common.UserRole role)
		{
			if (null == request) return false;
			return await hasBasicPermission(request.SessionToken, role);
		}

		/// <summary>
		/// Al recuperar la sesión, aprovechamos para recargar en el servidor el timeout, de forma que tenemos dos copias. Una en el
		/// cliente, para saber que está fuera de tiempo y otra en el servidor para ir modificando el instante de caducidad.
		/// </summary>
		/// <param name="tokenId"></param>
		/// <returns></returns>
		protected async Task<ActiveSessionModel?> retrieveSession(Guid tokenId)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				ActiveSessionModel? salida = await almacen.ActiveSessions
				 .Where(x => x.Id == tokenId)
				 .FirstOrDefaultAsync();

				if(null==salida) return null;

				if(salida.Expiry<DateTime.UtcNow)
				{
					almacen.ActiveSessions.Remove(salida);
					await almacen.SaveChangesAsync();
					await addLoginRecord(salida.UserId, Common.sessionEventType.sessionExpiry, salida.HostIp);
					return null;
				}

				salida.Expiry = DateTime.UtcNow.Add(EXPIRY_INTERVAL);
				await almacen.SaveChangesAsync();
				return salida;
			}
		}
		protected async Task<User?> retrieveSessionUser(Guid tokenId)
		{
			ActiveSessionModel? auxSession = await retrieveSession(tokenId);
			if (null != auxSession)
			{
				User? salida = await userById(auxSession.UserId);
				return salida;
			}
			return null;
		}
	}
}

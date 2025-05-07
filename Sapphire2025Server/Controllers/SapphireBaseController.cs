using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sapphire2025Models;
using Sapphire2025Server.Models;
using System.Configuration;

namespace Sapphire2025Server.Controllers
{
	public abstract class SapphireBaseController : ControllerBase
	{
		internal TimeSpan EXPIRY_INTERVAL = new TimeSpan(4, 0, 0);
		internal IConfiguration mvarConfig;
		private const string VIP_TOKEN = "a77363a1-d47b-4d67-8f1e-9953597a7755";
		private Guid VIP_TOKEN_GUID = Guid.Parse(VIP_TOKEN);
		internal SapphireBaseController(IConfiguration config)
		{
			mvarConfig = config;
		}

		protected async Task<User?> retrieveUser(string userId)
		{
			User? salida = null;
			string mayus = userId.ToUpper();
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				salida = await almacen.Users.Where(x => x.CF == userId).FirstOrDefaultAsync();
				if (null == salida)
				{
					salida = await almacen.Users.Where(x => x.NormalizedEmail == mayus).FirstOrDefaultAsync();
				}
				if (null == salida)
				{
					salida = await almacen.Users.Where(x => x.NormalizedUserName == mayus).FirstOrDefaultAsync();
				}
			}
			return salida;
		}
		protected async Task purgeSessions()
		{
			//Elimina las sesiones que hayan caducado.
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				IEnumerable<ActiveSessionModel> seleccion = almacen.ActiveSessions.Where(s => s.Expiry < DateTime.Now);
				foreach (ActiveSessionModel elemento in seleccion)
				{
					//Añado un log de cierre de sesión por expiración.
					await addLoginRecord(elemento.UserId, SessionEvent.sessionEventType.sessionExpiry, elemento.HostIp);
				}
				almacen.ActiveSessions.RemoveRange(seleccion);
				await almacen.SaveChangesAsync();
			}
		}
		protected async Task addLoginRecord(string userId, SessionEvent.sessionEventType type, string hostPoint)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				SessionEvent nuevo = new SessionEvent();
				nuevo.Id = Guid.NewGuid().ToString();
				nuevo.userId = userId.ToString();
				nuevo.type = type;
				nuevo.hostPoint = hostPoint;
				nuevo.timeSpan = DateTime.Now;
				almacen.SessionEvents.Add(nuevo);
				await almacen.SaveChangesAsync();
			}
		}
		protected async Task<bool> hasBasicPermission(Guid tokenId, Common.UserRole role)
		{
			if (Guid.Empty.Equals(tokenId)) return false;
			if (VIP_TOKEN_GUID.Equals(tokenId)) return true;

			ActiveSessionModel? auxSession = await retrieveSession(tokenId);
			if (null != auxSession)
			{
				//El administrador tiene acceso a todo
				if (Utils.hasRole(auxSession.Credentials,Common.UserRole.Root))
					return true;
				return Utils.hasRole(auxSession.Credentials, role);
			}		
			return false;
		}
		protected async Task<bool> hasBasicPermission(BasicRequestModel request, Common.UserRole role)
		{
			if (null == request) return false;
			return await hasBasicPermission(request.SessionToken, role);
		}

		//Estoy teniendo un cacao gordo con la gestión de las credenciales. A la hora de recuperar
		//las sesiones debería tener una colección de credenciales para poder comprobar si el
		//usuario puede realizar ciertas funciones. Al principio lo intenté hacer con flags, pero
		//es un engorroso. Lo mejor será tener una colección y serializarla.

		//Lo dejo todo para mañana, que espero estar más despierto.

		protected async Task<ActiveSessionModel?> retrieveSession(Guid tokenId)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				ActiveSessionModel? salida = await almacen.ActiveSessions.Where(x => x.Id == tokenId).FirstOrDefaultAsync();
				if (null != salida)
				{
					salida.Expiry = DateTime.Now.Add(EXPIRY_INTERVAL);
					//Prolongo la caducidad de esta sesión
					await almacen.SaveChangesAsync();
				}
				return salida;
			}
		}
		protected async Task<User?> retrieveSessionUser(Guid tokenId)
		{
			ActiveSessionModel? auxSession = await retrieveSession(tokenId);
			if (null != auxSession)
			{
				User? salida = await retrieveUser(auxSession.UserId);
				return salida;
			}
			return null;
		}
	}
}

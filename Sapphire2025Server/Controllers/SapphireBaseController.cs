using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sapphire2025Server.Models;
using System.Configuration;

namespace Sapphire2025Server.Controllers
{
	public abstract class SapphireBaseController:ControllerBase
	{
		internal TimeSpan EXPIRY_INTERVAL = new TimeSpan(4, 0, 0);
		internal IConfiguration mvarConfig;
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
		protected async Task<ActiveSessionModel?> retrieveSession(string tokenId)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				Guid auxTokenId = Guid.Empty;
				Guid.TryParse(tokenId, out auxTokenId);
				ActiveSessionModel? salida = await almacen.ActiveSessions.Where(x => x.Id == auxTokenId).FirstOrDefaultAsync();
				if (null != salida)
				{
					salida.Expiry = DateTime.Now.Add(EXPIRY_INTERVAL);
					//Prolongo la caducidad de esta sesión
					await almacen.SaveChangesAsync();
				}
				return salida;
			}
		}
	}
}

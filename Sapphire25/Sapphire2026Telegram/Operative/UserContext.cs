
using Microsoft.EntityFrameworkCore;
using Sapphire2025Models;
using Sapphire2025Models.Authentication;
using Sapphire2026.Data;
using Sapphire2026.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2026Telegram.Operative
{
    /// <summary>
    /// El módulo de telegram necesitará obtener toda la información posible de un usuario que está
    /// conectado al bot, así como sus permisos.
    /// </summary>
    internal class UserContext
    {
        internal User? mvarUser { get; private set; }
        internal IConfiguration mvarConfig;
        private long mvarTelegramId { get; set; } = -1;
		internal List<ExtendedUserModel.RoleInfo> ColRoles = new List<ExtendedUserModel.RoleInfo>();

        internal UserContext(long telegramChatId, IConfiguration config)
        {
            mvarConfig = config;
            mvarTelegramId = telegramChatId;			
        }
		internal bool Paired { get => (null != mvarUser); }
		internal long TelegramId { get => null==mvarUser?mvarTelegramId:mvarUser.TelegramId; }
		internal string Name { get => (null == mvarUser || null==mvarUser.UserName) ? "Desconocido" : mvarUser.UserName; }
        internal async Task Init()
        {
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				mvarUser = await almacen.Users
					.Where(x => x.TelegramId == mvarTelegramId).FirstOrDefaultAsync();
            }
			ColRoles.Clear();			
			if (null==mvarUser)
			{

			}
			else
			{
				//Ahora recuperamos los roles del usuario	
				Dictionary<uint, ExtendedUserModel.RoleInfo> auxRoles = await retrieveRolesDictionary();
				List<uint> listaRoles = await retrieveUserRoles(mvarUser.guid);
				foreach (uint elemento in listaRoles)
				{
					if (auxRoles.ContainsKey(elemento))
						ColRoles.Add(auxRoles[elemento]);
				}
			}
		}

		private async Task<Dictionary<uint, ExtendedUserModel.RoleInfo>> retrieveRolesDictionary()
		{
			Dictionary<uint, ExtendedUserModel.RoleInfo> salida = new Dictionary<uint, ExtendedUserModel.RoleInfo>();
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				List<RoleDictionary> auxDictionary = await almacen.RoleDictionary.OrderBy(x => x.RoleId).ToListAsync();
				foreach (RoleDictionary auxEntrada in auxDictionary)
				{
					ExtendedUserModel.RoleInfo nuevoRol = new ExtendedUserModel.RoleInfo();
					nuevoRol.roleId = auxEntrada.RoleId;
					nuevoRol.Name = auxEntrada.Name;
					nuevoRol.Comment = auxEntrada.Comment;
					salida.Add(auxEntrada.RoleId, nuevoRol);
				}
			}
			return salida;
		}
		internal async Task<List<uint>> retrieveUserRoles(Guid userId)
		{
			List<uint> salida = new List<uint>();
			string userString = userId.ToString();
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				List<UserAndRole> entrada = await almacen.UserAndRoles.Where(x => x.UserId.Equals(userString)).ToListAsync();
				foreach (UserAndRole role in entrada)
					salida.Add(role.RoleId);
			}
			return salida;
		}
		internal bool MatchRole(Common.UserRole[] roles)
		{
			foreach (ExtendedUserModel.RoleInfo auxInfo in ColRoles)
			{
				if(auxInfo.roleId<256)
				{
					if (roles.Contains((Common.UserRole)auxInfo.roleId))
					{
						return true;
					}
				}
			}
			return false;
		}
	}
}

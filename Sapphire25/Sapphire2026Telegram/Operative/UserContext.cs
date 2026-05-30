
using ICSharpCode.SharpZipLib.Core;
using Microsoft.EntityFrameworkCore;
using Sapphire2025.Storage;
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
        internal Sapphire2025Models.Authentication.ExtendedUserModel? mvarUser { get; private set; }	
        internal IConfiguration mvarConfig;
		private IntStorageService? mvarIntStorage;
		internal IntStorageService IntStorage
		{ 
			get 
			{
				if(null==mvarIntStorage)
					mvarIntStorage = new IntStorageService();
				return mvarIntStorage;
			}
		}
        private long mvarTelegramId { get; set; } = -1;

        internal UserContext(long telegramChatId, IConfiguration config)
        {
            mvarConfig = config;
            mvarTelegramId = telegramChatId;			
        }
		internal bool Paired { get => (null != mvarUser); }
		internal long TelegramId { get => null==mvarUser?mvarTelegramId:mvarUser.TelegramId; }
		internal string Name { get => (null == mvarUser || null==mvarUser.Name) ? "Desconocido" : mvarUser.Name; }
        internal async Task Init(AuthenticationClient client)
        {
			mvarUser = await client.userByTelegramId(mvarTelegramId);			
		}

		internal bool MatchRole(Common.UserRole[] roles)
		{
			if(null!=mvarUser)
			{
				foreach (ExtendedUserModel.RoleInfo auxInfo in mvarUser.roles.Values)
				{
					if (auxInfo.roleId < 256)
					{
						if (roles.Contains((Common.UserRole)auxInfo.roleId))
						{
							return true;
						}
					}
				}
			}
			return false;
		}
	}
}

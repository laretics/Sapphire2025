
using ICSharpCode.SharpZipLib.Core;
using Microsoft.EntityFrameworkCore;
using Sapphire2025.Storage;
using Sapphire2025Models;
using Sapphire2025Models.Authentication;
using Sapphire2025Models.I18n;
using Sapphire2025Models.Preferences;
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
        internal ExtendedUserModel? mvarUser { get; private set; }	
        internal IConfiguration mvarConfig;
		private IntStorageService? mvarIntStorage;
		//internal IntStorageService IntStorage
		//{ 
		//	get 
		//	{
		//		if(null==mvarIntStorage)
		//			mvarIntStorage = new IntStorageService();
		//		return mvarIntStorage;
		//	}
		//}
        private long mvarTelegramId { get; set; } = -1;

        internal UserContext(long telegramChatId, IConfiguration config)
        {
            mvarConfig = config;
            mvarTelegramId = telegramChatId;			
        }
		internal bool Paired { get => (null != mvarUser); }
		internal long TelegramId { get => null==mvarUser?mvarTelegramId:mvarUser.TelegramId; }
		internal string Name { get => (null == mvarUser || null==mvarUser.Name) ? UiCatalog.Get(Locale, "tg.unknown.name") : mvarUser.Name; }
		internal UiLocale Locale { get; private set; } = UiLocale.Spanish;
		internal UiLocale HintLocale { get; set; } = UiLocale.Spanish;
        internal async Task Init(AuthenticationClient client)
        {
			mvarUser = await client.userByTelegramId(mvarTelegramId);
			await LoadLocaleAsync();
		}

		internal async Task LoadLocaleAsync()
		{
			if (mvarUser is null || Guid.Empty.Equals(mvarUser.guid))
			{
				Locale = HintLocale;
				return;
			}

			try
			{
				using DataStorage almacen = new DataStorage(mvarConfig);
				string userId = mvarUser.guid.ToString();
				UserPreference? row = await almacen.UserPreferences
					.AsNoTracking()
					.FirstOrDefaultAsync(x => x.UserId == userId && x.Key == PreferenceKeys.Locale);
				Locale = string.IsNullOrWhiteSpace(row?.Value)
					? HintLocale
					: UiLocales.Parse(row.Value);
			}
			catch
			{
				Locale = HintLocale;
			}
		}

		internal bool MatchRole(ISet<Common.UserRole> roles)
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

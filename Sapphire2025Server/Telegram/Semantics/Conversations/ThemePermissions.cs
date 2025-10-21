using Microsoft.EntityFrameworkCore;
using Sapphire2025Server.Models;
using Sapphire2025Server.Telegram.Semantics;
using Sapphire2025Server.Telegram.Semantics.Concepts;
namespace Sapphire2025Server.Telegram.Semantics.Conversations
{
	internal class ThemePermissions:BotTheme
	{
		private string mvarTelegramScript = string.Empty; //Script de Telegram para el usuario.
		private User? mvarUser = null; //Usuario de Zafiro al que pertenece el chat de Telegram.
		internal ThemePermissions(BotTask parent) : base(parent){}
		internal async override Task InitializeAsync()
		{
			mvarUser = await getUser(mvarParent.user.TelegramId);
			if(null==mvarUser)
			{
				//No tenemos usuario... hay que emparejar
				//child = new ThemePairing(mvarParent);				
			}
			else
			{
				//mvarParent = new BotTask(mvarParent.user.TelegramId);
				//await mvarParent.InitializeAsync();
				////Remplazamos el usuario vacío por el de la base de datos.
				//child = new ThemeMenu(mvarParent);
				//await child.InitializeAsync();
			}			
		}

		//internal override async Task<Response> ResponseFromBot()
		//{
		//	System.Diagnostics.Debug.Assert(null != child,"InitializeAsync debería haber creado la instancia");
		//	if (child.isEnded)
		//	{
		//		child = new ThemeMenu(mvarParent);
		//		await child.InitializeAsync();
		//	}

		//	//if (child.GetType() == typeof(ThemePairing))
		//	//{
		//	//	//Para emparejar NO necesitamos comprobar permisos.
		//	//	return await child.ResponseFromBot();
		//	//}
		//	//else
		//	//{
		//	//	//Si hemos llegado hasta aquí, es porque el tema hijo es algún tipo que necesita permisos.
		//	//	Response? check = await checkPermissions();
		//	//	if (null == check)
		//	//		return await child.ResponseFromBot();
		//	//	else
		//	//	{
		//	//		return check;
		//	//	}

		//	//}
		//	return null;
		//}
		//internal override async Task textToBot(string text)
		//{
		//	System.Diagnostics.Debug.Assert(null != child, "InitializeAsync debería haber creado la instancia");
		//	if (child.isEnded)
		//	{
		//		child = new ThemeMenu(mvarParent);
		//		await child.InitializeAsync();
		//	}
		//	await child.textToBot(text);
		//}

		//internal async Task<Response?> checkPermissions() //Comprobamos los permisos. Si los tiene, devolvemos null
		//{
		//	if(!mvarParent.user.TelegramEnabled)
		//		return new NoPermissionResponse(NoPermissionResponse.PermissionType.ZafiroDisabled);
		//		mvarUser = mvarParent.user;
		//		System.Diagnostics.Debug.Assert(null!=mvarUser, "El usuario no debería ser nulo en este punto.");
		//	using (DataStorage almacen = new DataStorage(BotTask.config))
		//	{
		//		mvarTelegramScript = await almacen.GetRegisterValue(mvarUser.Id, "TGRULES", string.Empty);
		//		if (!BotSoul.CanUseTelegram(mvarTelegramScript))
		//			return new NoPermissionResponse(NoPermissionResponse.PermissionType.ZafiroScript);
		//	}
		//	return null;
		//}

		private async Task<User?> getUser(long telegramChatId)
		{
			using (DataStorage almacen = new DataStorage(BotTask.config))
			{
				User? auxUser =
					await almacen.Users.FirstOrDefaultAsync(x => x.TelegramId == telegramChatId);
				return auxUser;
			}
		}
	}
}

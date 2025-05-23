using Microsoft.EntityFrameworkCore;
using Sapphire2025Server.Models;
namespace Sapphire2025Server.Telegram
{
	internal class ThemePermissions:BotTheme
	{
		private string mvarTelegramScript = string.Empty; //Script de Telegram para el usuario.
		private User? mvarUser = null; //Usuario de Zafiro al que pertenece el chat de Telegram.
		internal ThemePermissions(BotTask parent) : base(parent)
		{

			//Tenemos que emparejar el usuario de Telegram con un usuario de la base de datos.
		}
		internal async override Task InitializeAsync()
		{
			mvarUser = await getUser(mvarParent.user.TelegramId);
			if(null==mvarUser)
			{
				//No tenemos usuario... hay que emparejar
				child = new ThemePairing(mvarParent);				
			}
			else
			{
				mvarParent.user = mvarUser;
				//Remplazamos el usuario vacío por el de la base de datos.
				child = new ThemeMenu(mvarParent);
			}
			await child.InitializeAsync();
		}

		internal override async Task<string> textFromBot()
		{
			System.Diagnostics.Debug.Assert(null != child,"InitializeAsync debería haber creado la instancia");
			if (child.isEnded)
			{
				child = new ThemeMenu(mvarParent);
				await child.InitializeAsync();
			}

			if (child.GetType() == typeof(ThemePairing))
			{
				//Para emparejar NO necesitamos comprobar permisos.
				return await child.textFromBot();
			}
			else
			{
				//Si hemos llegado hasta aquí, es porque el tema hijo es algún tipo que necesita permisos.
				string? check = await checkPermissions();
				if (null == check)
					return await child.textFromBot();
				else
					return check;
			}
		}
		internal override async Task textToBot(string text)
		{
			System.Diagnostics.Debug.Assert(null != child, "InitializeAsync debería haber creado la instancia");
			if (child.isEnded)
			{
				child = new ThemeMenu(mvarParent);
				await child.InitializeAsync();
			}
			await child.textToBot(text);
		}

		internal async Task<string?> checkPermissions() //Comprobamos los permisos. Si los tiene, devolvemos null
		{
			if(!mvarParent.user.TelegramEnabled)
				return "No tienes permisos para acceder a Zafiro a través de Telegram. Ponte en contacto con el administrador para habilitarlo.";
			System.Diagnostics.Debug.Assert(null!=mvarUser, "El usuario no debería ser nulo en este punto.");
			using (DataStorage almacen = new DataStorage(mvarParent.config))
			{
				mvarTelegramScript = await almacen.GetRegisterValue(mvarUser.Id, "TGRULES", string.Empty);
				if (!BotSoul.CanUseTelegram(mvarTelegramScript))
					return "No tienes permisos definidos en el script de configuración por usuario. Ponte en contacto con el administrador para cambiar la configuración.";
			}
			return null;
		}

		private async Task<User?> getUser(long telegramChatId)
		{
			using (DataStorage almacen = new DataStorage(mvarParent.config))
			{
				User? auxUser =
					await almacen.Users.FirstOrDefaultAsync(x => x.TelegramId == telegramChatId);
				return auxUser;
			}
		}
	}
}

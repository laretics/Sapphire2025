
using Microsoft.EntityFrameworkCore;
using Sapphire2025Server.Models;
using Sapphire2025Server.Telegram.Semantics;
using Sapphire2025Server.Telegram.Semantics.Responses;

namespace Sapphire2025Server.Telegram
{

	internal class ThemePairing:BotTheme
	{
		private string? mvarUserId { get; set; } //ID del usuario en Zafiro
		private bool mvarInitialized = false; //Indica si el tema ha sido inicializado.
		private bool mvarError = false; //Error en la autenticación previa.


		private enum typeFase:byte
		{
			tfInitial=0,
			tfUserId=1,
			tfPassword = 2,
			tfEnd = 3
		}
		private typeFase mvarFase = typeFase.tfInitial; //Fase de la conversación actual.
		internal ThemePairing(BotTask parent) : base(parent){}
		internal async override Task<Response> ResponseFromBot()
		{
			if(mvarError)
			{
				mvarError = false; //Quitamos el error para la siguiente vez.
				return new PairingResponse(3); //Datos desconocidos o erróneos.
			}
			else
			{
				switch (mvarFase)
				{
					case typeFase.tfInitial:
						return new PairingResponse(0); //Fase inicial, pedimos el ID de usuario.
					case typeFase.tfUserId:
						return new PairingResponse(1); //Fase de ID de usuario, pedimos el ID de usuario.
					case typeFase.tfPassword:
						return new PairingResponse(2); //Fase de contraseña, pedimos la contraseña del usuario.
					default:
						return new NonImplementedResponse(); //Fase final, no debería llegar aquí.
				}
			}
		}
		internal override async Task textToBot(string text)
		{
			switch(mvarFase)
			{
				case typeFase.tfInitial:					
					mvarFase = typeFase.tfUserId;
					break;
				case typeFase.tfUserId:
					mvarUserId = text;
					mvarFase = typeFase.tfPassword;
					break;
				case typeFase.tfPassword:
					using (DataStorage almacen = new DataStorage(BotTask.config))
					{
						User? auxUser = await almacen.retrieveUser(mvarUserId);
						if (null != auxUser)
						{
							if (almacen.authenticate(auxUser, text))
							{
								//Credenciales correctas.
								User? usuarioCambio = await almacen.Users.Where(x => x.Id == auxUser.Id).FirstOrDefaultAsync();
								System.Diagnostics.Debug.Assert(null!=usuarioCambio, "No se ha encontrado el usuario en la base de datos.");
								usuarioCambio.TelegramId = mvarParent.user.TelegramId;
								await almacen.SaveChangesAsync();
								mvarParent.user = usuarioCambio;							
								mvarFase = typeFase.tfEnd;
								endTheme(); //Finalizamos la conversación.
							}
							else
							{
								//Credenciales incorrectas. Volvemos a empezar.
								mvarError = true;
								mvarUserId = null;
								mvarFase = typeFase.tfInitial;
							}
						}
					}
					break;
			}
		}
	}
}

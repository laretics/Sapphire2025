


using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

namespace Sapphire2025Server.Telegram.Semantics.Conversations
{
	internal class PairingTheme:BotTheme
	{
		private PairingQuew mvarQuew { get; set; }
		private ImageResponse mvarFirstResponse = new ImageResponse();
		private ImageResponse mvarSecondResponse = new ImageResponse();
		private bool mvarFirstError;
		internal PairingTheme(BotTask parent):base(parent)
		{
			initResponses();
			mvarQuew = parent.parent.mvarPairingQuew;
		}
		private void initResponses()
		{
			mvarFirstError = false;
			mvarFirstResponse.ImageUrl = "PairingScreen.png";
			mvarFirstResponse.addText("Hola. Soy el bot de Zafiro. No te has identificado todavía. Antes de acceder al servicio desde tu cuenta de Telegram necesito que generes una clave pulsando el botón de la página \"YO\" del panel izquierdo de la aplicación y me la envíes.");
			mvarFirstResponse.addText("Hola. Soy el bot de Zafiro. Parece que todavía no te tengo en la base de datos. Para que podamos comunicarnos tienes que generar una clave pulsando el botón de la página \"YO\" del panel izquierdo de la aplicación y me la envíes.");
			mvarSecondResponse.ImageUrl = "PairingScreen.png";
			mvarSecondResponse.addText("El código que me has enviado parece incorrecto. Antes de acceder al servicio desde tu cuenta de Telegram necesito que generes una clave pulsando el botón de la página \"YO\" del panel izquierdo de la aplicación y me la envíes.");
			mvarSecondResponse.addText("El código que acabas de teclear no es válido. Todavía no te tengo en la base de datos. Para que podamos comunicarnos tienes que generar una clave pulsando el botón de la página \"YO\" del panel izquierdo de la aplicación y me la envíes.");
		}
		internal override async Task InternalResponseFromBot(ITelegramBotClient client)
		{
			//Saludo para el emparejamiento.
			if(mvarFirstError)
				await mvarSecondResponse.Send(client, mvarParent.mvarTelegramId);
			else
				await mvarFirstResponse.Send(client, mvarParent.mvarTelegramId);
		}
		internal override async Task InternalTextToBot(string text)
		{
			//Código de emparejamiento desde el cliente.			
			Guid pairingUser = mvarQuew.getPairingUserId(text);			
			if (Guid.Empty == pairingUser)
				mvarFirstError = true;
			else
			{
				//Emparejamos el usuario y se lo asignamos al padre...
				if (await mvarParent.PairUser(pairingUser))
					endTheme(); //Podemos terminar el emparejado.
			}
		}
	}
}

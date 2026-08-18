using Microsoft.EntityFrameworkCore;
using Sapphire2025.Storage;
using Sapphire2025Models;
using Sapphire2025Models.Authentication;
using Sapphire2025Models.I18n;
using Sapphire2026.Data;
using Sapphire2026Telegram.Operative;
using Telegram.Bot;
using TorchSharp.Modules;

namespace Sapphire2026Telegram.Semantics.Conversations
{
	internal class PairingTheme:BotTheme
	{
		private PairingQuew mvarQuew { get; set; }
		private ImageResponse mvarFirstResponse = new ImageResponse();
		private ImageResponse mvarSecondResponse = new ImageResponse();
		private bool mvarFirstError;
		internal PairingTheme(BotTask parent):base(parent)
		{
			mvarQuew = parent.parent.mvarPairingQuew;
			initResponses();
		}
		/// <summary>
		/// Asignación del ID del usuario una vez tenemos el emparejado.
		/// </summary>
		/// <param name="userId"></param>
		/// <returns></returns>
		internal async Task PairUser(Guid userId, long telegramChatId)
		{
			//Tengo que obtener el usuario de la base de datos para hacer el emparejamiento.
			using IServiceScope scope = mvarParent.parent.services.CreateScope();
			AuthenticationClient auxClient = scope.ServiceProvider.GetRequiredService<AuthenticationClient>();
			await auxClient.pairTelegram(userId, telegramChatId);
			mvarParent.userContext = new UserContext(telegramChatId, mvarParent.mvarConfig);
			await mvarParent.userContext.Init(auxClient);
		}
		private void initResponses()
		{
			mvarFirstError = false;
			mvarFirstResponse.ImageUrl = "PairingScreen.png";
			mvarSecondResponse.ImageUrl = "PairingScreen.png";
		}

		private void PreparePairingPhrases(ImageResponse target, string key1, string key2, UiLocale locale)
		{
			string me = UiCatalog.Get(locale, "nav.me");
			target.addText(TelegramI18n.T(locale, key1, me));
			target.addText(TelegramI18n.T(locale, key2, me));
		}
        internal override async Task InternalPreprocess()
        {		
			if (mvarParent.userContext.Paired)
			{
				// El usuario está emparejado. Hay que ver qué pasa con el hijo.
				if (null != child && child.isEnded)
					child = null;
				if (null == child)
					child = new InitialTheme(mvarParent);

				//Delegación al hijo
				await child.Preprocess();
			}
        }
		internal override async Task InternalResponseFromBot(ITelegramBotClient client)
		{
			//Saludo para el emparejamiento.
			UiLocale locale = mvarParent.userContext?.Locale ?? UiLocale.Spanish;
			if(mvarFirstError)
			{
				mvarSecondResponse = new ImageResponse { ImageUrl = "PairingScreen.png" };
				PreparePairingPhrases(mvarSecondResponse, "tg.pair.bad.1", "tg.pair.bad.2", locale);
				await mvarSecondResponse.Send(client, mvarParent.userContext);
			}
			else
			{
				mvarFirstResponse = new ImageResponse { ImageUrl = "PairingScreen.png" };
				PreparePairingPhrases(mvarFirstResponse, "tg.pair.first.1", "tg.pair.first.2", locale);
				await mvarFirstResponse.Send(client, mvarParent.userContext);
			}
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
				await PairUser(pairingUser, mvarParent.userContext.TelegramId);
			}
		}
	}
}

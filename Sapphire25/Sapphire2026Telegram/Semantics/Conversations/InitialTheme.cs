using Sapphire2026Telegram.Semantics;
using Sapphire2026Telegram.Semantics.Concepts;
using System.Diagnostics;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Sapphire2026Telegram.Semantics.Conversations
{
	internal class InitialTheme:BotTheme
	{
		private bool mvarError { get; set; }		
		internal InitialTheme(BotTask parent):base(parent)
		{
		}

		//private string mvarErrorText;

		internal override async Task InternalResponseFromBot(ITelegramBotClient client)
		{
		
			if(mvarError)
			{
				TextResponse equivocado = new TextResponse();
				equivocado.addCatalog("tg.err.1", "tg.err.2", "tg.err.3", "tg.err.4", "tg.err.5");
				await equivocado.Send(client, mvarParent.userContext);
				mvarError = false;
			}
			else
			{
				TextResponse auxPrompt = new TextResponse();
				auxPrompt.addCatalog("tg.hello.1", "tg.hello.2", "tg.hello.3");
				if(null!=mvarParent.userContext)
				{
					auxPrompt.addKey("username", mvarParent.userContext.Name);
					await auxPrompt.Send(client, mvarParent.userContext);
				}
					
			}						
		}
		private async Task<BotTheme?> Match(string text)
		{
			switch(IntentClassifier.Instance.Predict(text))
			{
				case "AbrirIncidencia":
					TrainNoteConcept auxNota = 
						new TrainNoteConcept(mvarParent.mvarConfig,mvarParent.parent.services, true);
					await auxNota.AddText(text);
					return new TrainIncidenceTheme(mvarParent, auxNota);


				case "Nota":
					TrainNoteConcept auxNota2 = 
						new TrainNoteConcept(mvarParent.mvarConfig,mvarParent.parent.services , false);
					await auxNota2.AddText(text);
					return new TrainIncidenceTheme(mvarParent, auxNota2);

				case "Disponibles": //Tengo que hacer un concepto sólo para ver el material disponible.
				case "EstadoTren": //Tengo que hacer un concepto sólo para ver el estado de un tren.
				case "EnTaller": //Hacer un concepto para ver los trenes que están en taller y su estado.
				case "VerInforme":
					return new TrainReportTheme(mvarParent);
				default:
					return null;
			}
		}

		internal async override Task InternalTextToBot(string text)
		{	
			BotTheme? detectado = await Match(text);
			mvarError = (null == detectado);
			if(null!=detectado)
			{
				this.child = detectado;
				mvarError = false;
				return;
			}
			mvarError = true;
		}

	}
}

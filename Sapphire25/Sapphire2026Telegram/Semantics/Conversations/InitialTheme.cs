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
				equivocado.addText("No he entendido lo que quieres decir. ¿Quieres abrir un parte de avería o de incidencia?");
				equivocado.addText("Por favor escribe o habla más claro. ¿Te gustaría conocer el estado de los trenes disponibles?");
				equivocado.addText("Estoy aprendiendo versión a versión. De momento no soy capaz de entender lo que acabas de decirme. Puedo abrir partes de incidencias, mostrar informes de un tren o mostrar históricos de uso.");
				equivocado.addText("¿Perdón? ¿Qué querías decirme?");
				equivocado.addText("¿Puedes repetir con otras palabras?");
				//equivocado.addText(mvarErrorText);
				await equivocado.Send(client, mvarParent.user);
				mvarError = false;
			}
			else
			{
				TextResponse auxPrompt = new TextResponse();
				auxPrompt.addText("Hola #username. ¿Qué te gustaría hacer?");
				auxPrompt.addText("Bienvenido #username. Dime qué quieres de mí.");
				auxPrompt.addText("¿Qué tal #username? Cuéntame qué puedo hacer por ti.");				
				if(null!=mvarParent.user)
				{
					auxPrompt.addKey("username", mvarParent.user.Name);
					await auxPrompt.Send(client, mvarParent.user);
				}
					
			}						
		}
		private async Task<BotTheme?> Match(string text)
		{
			switch(IntentClassifier.Instance.Predict(text))
			{
				case "AbrirIncidencia":
					TrainNoteConcept auxNota = new TrainNoteConcept(mvarParent.mvarConfig, true);
					await auxNota.AddText(text);
					return new TrainIncidenceTheme(mvarParent, auxNota, text);


				case "Nota":
					TrainNoteConcept auxNota2 = new TrainNoteConcept(mvarParent.mvarConfig, false);
					await auxNota2.AddText(text);
					return new TrainIncidenceTheme(mvarParent, auxNota2, text);

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

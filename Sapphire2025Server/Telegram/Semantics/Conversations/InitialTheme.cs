using Sapphire2025Server.Telegram.Semantics.Concepts;
using System.Diagnostics;
using Telegram.Bot;

namespace Sapphire2025Server.Telegram.Semantics.Conversations
{
	internal class InitialTheme:BotTheme
	{
		private bool mvarError { get; set; }
		private IAConceptPerceptron? mvarPerceptron;
		internal InitialTheme(BotTask parent):base(parent){}

		internal override async Task InternalResponseFromBot(ITelegramBotClient client)
		{
			if(null==mvarPerceptron)
				initConcepts();
		
			if(mvarError)
			{
				TextResponse equivocado = new TextResponse();
				equivocado.addText("No he entendido lo que quieres decir. ¿Quieres abrir un parte de avería o de incidencia?");
				equivocado.addText("Por favor escribe o habla más claro. ¿Te gustaría conocer el estado de los trenes disponibles?");
				equivocado.addText("Estoy aprendiendo versión a versión. De momento no soy capaz de entender lo que acabas de decirme. Puedo abrir partes de incidencias, mostrar informes de un tren o mostrar históricos de uso.");
				equivocado.addText("¿Perdón? ¿Qué querías decirme?");
				equivocado.addText("¿Puedes repetir con otras palabras?");
				await equivocado.Send(client, mvarParent.mvarTelegramId);
				mvarError = false;
			}
			else
			{
				TextResponse auxPrompt = new TextResponse();
				auxPrompt.addText("Hola #username. ¿Qué te gustaría hacer?");
				auxPrompt.addText("Bienvenido #username. Dime qué quieres de mí.");
				auxPrompt.addText("¿Qué tal #username? Cuéntame qué puedo hacer por ti.");
				if (null == mvarParent.user || null == mvarParent.user.UserName)
					auxPrompt.addKey("username", "desconocido");
				else
					auxPrompt.addKey("username", mvarParent.user.UserName);
				await auxPrompt.Send(client, mvarParent.mvarTelegramId);
			}						
		}
		private void initConcepts()
		{
			mvarError = false;
			mvarPerceptron = new IAConceptPerceptron();
			mvarPerceptron.addConcept( new GeneralConcept("InformeEstado","disponible,disponibilidad,disponibles,trenes,disposicion,informe,lista"));
			mvarPerceptron.addConcept(new GeneralConcept("ParteAveria", "parte,averia,averias,incidencia,incidencias,tren,daño,daños,ut"));		
		}

		internal async override Task InternalTextToBot(string text)
		{
			Debug.Assert(null != mvarPerceptron);
			GeneralConcept? detectado = await mvarPerceptron.Concept(text);
			mvarError = (null == detectado);
			if(null!=detectado)
			{
				if (detectado.name.Equals("InformeEstado"))
				{

				}
				else if (detectado.name.Equals("ParteAveria"))
					this.child = new TrainDamageTheme(mvarParent, text);
				else
					mvarError = true;
			}
		}

	}
}

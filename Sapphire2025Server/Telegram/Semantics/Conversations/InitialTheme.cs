using Sapphire2025Server.Telegram.Semantics.Concepts;
using System.Diagnostics;
using Telegram.Bot;

namespace Sapphire2025Server.Telegram.Semantics.Conversations
{
	internal class InitialTheme:BotTheme
	{
		private TextResponse? mvarPrompt;
		private IAConceptPerceptron? mvarPerceptron;
		internal InitialTheme(BotTask parent):base(parent){}

		internal override async Task InternalResponseFromBot(ITelegramBotClient client)
		{
			if (null == mvarPrompt)
				initResponses();

			if(null==mvarPerceptron)
				initConcepts();
				
			Debug.Assert(null != mvarPrompt);
			await mvarPrompt.Send(client, mvarParent.mvarTelegramId);



		}
		private void initConcepts()
		{
			mvarPerceptron = new IAConceptPerceptron();
			mvarPerceptron.addConcept( new GeneralConcept("InformeEstado","disponible,disponibilidad,disponibles,trenes,disposicion"));
			mvarPerceptron.addConcept(new GeneralConcept("ParteAveria", "parte,averia,averias,incidencia,incidencias,tren"));
			

		}

		private void initResponses()
		{
			mvarPrompt = new TextResponse();
			mvarPrompt.addText("Hola #username. ¿Qué te gustaría hacer?");
			mvarPrompt.addText("Bienvenido #username. Dime qué quieres de mí.");
			mvarPrompt.addText("¿Qué tal #username? Cuéntame qué puedo hacer por ti.");
			if (null == mvarParent.user || null == mvarParent.user.UserName)
				mvarPrompt.addKey("username", "desconocido");
			else
				mvarPrompt.addKey("username", mvarParent.user.UserName);
		}
		

	}
}

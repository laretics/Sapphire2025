using Sapphire2026Telegram.Semantics;
using Sapphire2026Telegram.Semantics.Concepts;
using System.Diagnostics;
using Telegram.Bot;

namespace Sapphire2026Telegram.Semantics.Conversations
{
	internal class InitialTheme:BotTheme
	{
		private bool mvarError { get; set; }
		private IAConceptPerceptron mvarPerceptron;
		internal InitialTheme(BotTask parent):base(parent)
		{
			mvarPerceptron = new IAConceptPerceptron();
		}

		//private string mvarErrorText;

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
		private void initConcepts()
		{
			mvarError = false;
			mvarPerceptron = new IAConceptPerceptron();
			mvarPerceptron.addConcept(new TrainNoteConcept(mvarParent.mvarConfig,true)); //Abrir un parte de averías.
			mvarPerceptron.addConcept(new TrainNoteConcept(mvarParent.mvarConfig, false)); //Abrir una nota
			mvarPerceptron.addConcept( new ReportRequestConcept(mvarParent.mvarConfig)); //Pedir un informe

			mvarPerceptron.addConcept(new TrainOrderConcept(mvarParent.mvarConfig,Sapphire2025Models.Common.OperationType.BeginCorrective)); //Entrar en taller.


						
		}


		internal async override Task InternalTextToBot(string text)
		{
			Debug.Assert(null != mvarPerceptron);
			NlpProcessor auxProcessor = new NlpProcessor();
			string[] auxTokens = auxProcessor.Process(text);
			GeneralConcept? detectado = await mvarPerceptron.ConceptMatch(auxTokens);
			mvarError = (null == detectado);
			if(null!=detectado)
			{
				if(detectado.GetType() == typeof(TrainNoteConcept))
				{// Queremos abrir un parte de avería.
					//this.child = new TrainIncidenceTheme(mvarParent,detectado,text);
				}
			//	if (detectado.name.Equals("InformeEstado"))
			//	{

			//	}
			//	else if (detectado.name.Equals("Train_Incidence_Report"))
			//		this.child = new TrainDamageTheme(mvarParent, text);
			//	else
			//		mvarError = true;
			}
			//mvarErrorText = string.Join("|", auxTokens);

			mvarError = true;
		}

	}
}

using Sapphire2025Server.Telegram.Semantics;
using Sapphire2025Server.Telegram.Semantics.Concepts;
using Telegram.Bot;

namespace Sapphire2025Server.Telegram
{
	/// <summary>
	/// Tema de una conversación.
	/// </summary>
	internal abstract class BotTheme
	{
		private bool mvarEnd = false; //Esta conversación puede haber terminado o seguir activa
		internal void endTheme() {mvarEnd = true;} //Terminamos el diálogo.
		internal bool isEnded { get => mvarEnd; }
		internal BotTheme(BotTask parent)
		{
			mvarEnd = false;
			mvarParent = parent;
			if (null == mvarParent)
				throw new ArgumentNullException(nameof(parent), "El tema no puede ser nulo");
		}
		internal BotTask mvarParent { get; set; } //Contenedor con la info del chat 
		internal BotTheme? child { get; set; } //Tema hijo (Las conversaciones funcionan como pilas)
		internal virtual async Task InitializeAsync(){}
		internal virtual async Task InternalResponseFromBot(ITelegramBotClient client) {}
		internal virtual async Task InternalTextToBot(string text){}
		public async Task ResponseFromBot(ITelegramBotClient client)
		{
			if (null != child && child.isEnded)
				child = null;

			if (null == child)
				await InternalResponseFromBot(client);
			else
				await child.InternalResponseFromBot(client);			
		}
		public async Task TextToBot(string? text)
		{
			if (null != child && child.isEnded)
				child = null;
			string auxTexto = string.Empty;
			if (null != text)
				auxTexto = text;

			if (null == child)
				await this.InternalTextToBot(auxTexto);
			else
				await child.InternalTextToBot(auxTexto);
		}

		internal async Task<TrainConcept?> seekTrainConcept(string text)
		{
			SemanticAnalyzer analizador = new SemanticAnalyzer();
			analizador.availableConcepts = new List<GeneralConcept>
			{
				//new TrainConcept()
				};
			//List<GeneralConcept> conceptosEncontrados = await analizador.setQuestionForObjects(text);
			//if (conceptosEncontrados.Count > 0)
			//{
			//	foreach (GeneralConcept concepto in conceptosEncontrados)
			//	{
			//		if (concepto.GetType() == typeof(TrainConcept))
			//		{
			//			return (TrainConcept)concepto;
			//		}
			//	}
			//}
			return null;
		}

	}
}

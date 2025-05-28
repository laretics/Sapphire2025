using Sapphire2025Server.Telegram.Semantics;
using Sapphire2025Server.Telegram.Semantics.Concepts;
using Sapphire2025Server.Telegram.Semantics.Responses;

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
		internal virtual async Task<Response> ResponseFromBot() {return new NonImplementedResponse();}
		internal virtual async Task textToBot(string text){}
		public async Task<Response> fromBot()
		{			
			if (null == child||child.isEnded)
			{
				child = null;
				return await this.ResponseFromBot();
			}				
			else
				return await child.ResponseFromBot();
		}
		public async Task ToBot(string? text)
		{
			string auxTexto = string.Empty;
			if (null != text)
				auxTexto = text;

			if (null == child||child.isEnded)
			{
				child = null;
				await this.textToBot(auxTexto);
			}				
			else
				await child.textToBot(auxTexto);
		}

		internal async Task<TrainConcept?> seekTrainConcept(string text)
		{
			SemanticAnalyzer analizador = new SemanticAnalyzer();
			analizador.availableConcepts = new List<Concept>
			{
				new TrainConcept()
				};
			List<Concept> conceptosEncontrados = await analizador.setQuestionForObjects(text);
			if (conceptosEncontrados.Count > 0)
			{
				foreach (Concept concepto in conceptosEncontrados)
				{
					if (concepto.GetType() == typeof(TrainConcept))
					{
						return (TrainConcept)concepto;
					}
				}
			}
			return null;
		}

	}
}

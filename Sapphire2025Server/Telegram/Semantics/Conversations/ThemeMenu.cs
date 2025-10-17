
using Sapphire2025Models.Aeneas;
using Sapphire2025Server.Controllers;
using Sapphire2025Server.Telegram.Semantics;
using Sapphire2025Server.Telegram.Semantics.Concepts;
using Sapphire2025Server.Telegram.Semantics.Responses;

namespace Sapphire2025Server.Telegram.Semantics.Conversations
{
	/// <summary>
	/// Este tema es el menú inicial de opciones
	/// Se llega a él con un usuario emparejado, autenticado y con los permisos en orden.
	/// </summary>
	internal class ThemeMenu:BotTheme
	{
		internal ThemeMenu(BotTask parent) : base(parent){}
		internal async override Task textToBot(string text)
		{
			if(null==child || child.isEnded)
			{
				child = null; //Si la conversación hubiera terminado, lo hago null.
				SemanticAnalyzer analizador = new SemanticAnalyzer();
				analizador.availableConcepts = new List<Concept>
				{
					new AvailableReportConcept(),
					new TrainReportConcept(),
					new DamageReportConcept()
				};
				List<VerbConcept> conceptosEncontrados = await analizador.setQuestion(text);
				if (conceptosEncontrados.Count > 0)
				{
					VerbConcept encontrado = conceptosEncontrados[0];
					if (encontrado.GetType() == typeof(AvailableReportConcept))
					{
						child = new ThemeAvailableReport((AvailableReportConcept)encontrado, mvarParent);
					}
					else if (encontrado.GetType() == typeof(TrainReportConcept))
					{
						child = new ThemeTrainReport((TrainReportConcept)encontrado, mvarParent);
					}
					else if (encontrado.GetType() == typeof(DamageReportConcept))
					{
						child = new ThemeDamageReport((DamageReportConcept)encontrado, mvarParent);
					}
				}
			}
			else
			{
				await child.textToBot(text);
			}			
		}
		internal async override Task<Response> ResponseFromBot()
		{
			if (null == child)
				return await base.ResponseFromBot();
			else
				return await child.ResponseFromBot();
		}
	}
}

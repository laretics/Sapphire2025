using Sapphire2026Telegram.Semantics.Concepts;
using Telegram.Bot;

namespace Sapphire2026Telegram.Semantics.Conversations
{
	internal class TrainIncidenceTheme:BotTheme
	{
		protected TrainNoteConcept? mvarConcept { get; set; } = null;
		private string mvarMessage { get; set; }



		internal TrainIncidenceTheme(BotTask parent, GeneralConcept concept, string message) : base(parent)
		{
			mvarMessage = message;
			if(concept is TrainNoteConcept)
				mvarConcept = (TrainNoteConcept)concept;
		}
		internal async override Task InternalTextToBot(string text)
		{
			if (null == mvarConcept) this.endTheme();
			else
			{


			}
				//mvarMessage = string.Concat(text, mvarMessage);
				//if (null == mvarConcept)
				//{
				//	mvarConcept = new TrainIncidenceConcept(mvarParent.mvarConfig);
				//	await mvarConcept.match(mvarMessage.Split(' ').ToList());
				//}
				//if (mvarConcept.mcolTrains.Count>0)
				//{
				//	await mvarConcept.match(text.Split(',').ToList());
				//}
				//else if(null==mvarConcept.Sympthoms)
				//{
				//	mvarConcept.Sympthoms = text;
				//}				
		}
		internal async override Task InternalResponseFromBot(ITelegramBotClient client)
		{
			if(null==mvarConcept)
			{
				TextResponse auxQueryPrompt = new TextResponse();
				auxQueryPrompt.addText("No te he entendido. ¿Puedes preguntar otra cosa?");
				auxQueryPrompt.addText("No estoy preparado para manejar esta pregunta. Prueba con otra.");
				await auxQueryPrompt.Send(client, mvarParent.user);
			}
			else
			{
				await mvarConcept.Confirmation().Send(client, mvarParent.user);
			}
		}
	}
}

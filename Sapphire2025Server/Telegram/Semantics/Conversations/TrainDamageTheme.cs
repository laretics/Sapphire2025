
using MySqlX.XDevAPI;
using Telegram.Bot;

namespace Sapphire2025Server.Telegram.Semantics.Conversations
{
	internal class TrainDamageTheme:BotTheme
	{
		private List<Models.Train> mcoltrains; //Conjunto de trenes afectados por el parte.
		private string? mvarSympthoms { get; set; } //Síntomas detectados por el informante
		private string mvarArguments { get; set; }		

		internal TrainDamageTheme(BotTask parent, string arguments) : base(parent)
		{
			mcoltrains = new List<Models.Train>();
			mvarArguments = arguments;
		}
		internal async override Task InternalTextToBot(string text)
		{
			this.endTheme();
		}
		internal async override Task InternalResponseFromBot(ITelegramBotClient client)
		{
			if(null==mvarSympthoms)
			{
				TextResponse auxPideSintomas = new TextResponse();
				auxPideSintomas.addText("¿Qué le ocurre a #ut ?");
				auxPideSintomas.addText("¿Qué síntomas tiene #ut ?");
				auxPideSintomas.addText("Por favor, describe la incidencia de #ut .");
				if (mcoltrains.Count == 0)
					auxPideSintomas.addKey("ut", "la unidad de tren");
				else
					auxPideSintomas.addKey("ut", string.Format("la UT {0}", mcoltrains[0].Name));
				await auxPideSintomas.Send(client, mvarParent.mvarTelegramId);
			}
			else
			{
				TextResponse auxPrompt = new TextResponse();
				auxPrompt.addText("Has querido abrir un parte de incidencia");
				auxPrompt.addText("Nuevo parte de incidencia");
				auxPrompt.addText("Este es el parte de incidencia");
				await auxPrompt.Send(client, mvarParent.mvarTelegramId);
			}
		}
	}
}

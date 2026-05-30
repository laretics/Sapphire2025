using Sapphire2025.Storage;
using Sapphire2025Models.Aeneas;
using Sapphire2026.Data.Models;
using Sapphire2026Telegram.Operative;
using Sapphire2026Telegram.Semantics.Concepts;
using Telegram.Bot;

namespace Sapphire2026Telegram.Semantics.Conversations
{
	internal class TrainIncidenceTheme:BotTheme
	{
		protected TrainNoteConcept? mvarConcept { get; set; } = null;
		private string mvarMessage { get; set; }
		private IServiceCollection mcolServices;



		internal TrainIncidenceTheme(BotTask parent, GeneralConcept concept, string message) : base(parent)
		{
			mvarMessage = message;
			if (concept is TrainNoteConcept noteConcept)
			{			
				mvarConcept = noteConcept;
			}				
		}
		internal async override Task InternalTextToBot(string text)
		{			
			if (null == mvarConcept) 
			{
				this.endTheme();
				return;
			}

			//Petición de cancelación explícita
			string auxTexto = text.Trim();
			if(auxTexto.Equals("cancelar",StringComparison.OrdinalIgnoreCase) ||
			auxTexto.Equals("salir",StringComparison.OrdinalIgnoreCase) ||
			auxTexto.Equals("no",StringComparison.OrdinalIgnoreCase))
			{
				this.endTheme();
				return;
			}			
			await mvarConcept.AddText(text);
			if(mvarConcept.Validated)
			{
				//Aquí se genera el parte...
				Console.WriteLine("Realizando el proceso del parte de incidencia o anotación");
				AeneasClient auxCliente = mvarParent.parent.services.GetRequiredService<AeneasClient>();
				foreach (Train tren in mvarConcept.mcolTrains)
					await OpenNoteToTrain(tren,auxCliente);

				this.endTheme();
				return;
			}

		}
		private async Task OpenNoteToTrain(Train train, AeneasClient client)
		{
			//TODO: Meter el código de cliente aquí.
			NoteModel auxNota = new NoteModel();
			auxNota.parent = train.Guid;
			


			//if (null != mvarConcept.tr)


				
			//await auxCliente.addNote(auxNota);
			//if (1 == auxNota.Type)
				//await auxCliente.openFailReport(auxNota.tr)
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

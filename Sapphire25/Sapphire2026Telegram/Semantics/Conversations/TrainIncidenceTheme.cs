using Sapphire2025.Storage;
using Sapphire2025Models;
using Sapphire2025Models.Aeneas;
using Sapphire2026.Data.Models;
using Sapphire2026Telegram.Operative;
using Sapphire2026Telegram.Semantics.Concepts;
using Telegram.Bot;
using TorchSharp.Modules;

namespace Sapphire2026Telegram.Semantics.Conversations
{
	internal class TrainIncidenceTheme:BotTheme
	{
		protected TrainNoteConcept? mvarConcept { get; set; } = null;

		internal TrainIncidenceTheme(BotTask parent, GeneralConcept concept) : base(parent)
		{
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
			if (Sapphire2025Models.I18n.TelegramI18n.IsCancel(auxTexto))
			{
				this.endTheme();
				return;
			}			
			await mvarConcept.AddText(text);
			if(mvarConcept.Validated)
			{
				//Aquí se genera el parte...
				Console.WriteLine("Realizando el proceso del parte de incidencia o anotación");
				using IServiceScope scope = mvarParent.parent.services.CreateScope();
				AeneasClient auxCliente = scope.ServiceProvider.GetRequiredService<AeneasClient>();
				foreach (TrainModel tren in mvarConcept.mcolTrains)
					await OpenNoteToTrain(tren,auxCliente);

				this.endTheme();
				return;
			}

		}
		private async Task OpenNoteToTrain(TrainModel train, AeneasClient client)
		{
			//TODO: Meter el código de cliente aquí.
			System.Diagnostics.Debug.Assert(null != mvarConcept);
			System.Diagnostics.Debug.Assert(null != mvarParent.userContext && null != mvarParent.userContext.mvarUser);
			NoteModel auxNota = new NoteModel();
			auxNota.parent = train.id;
			auxNota.TimeStamp = DateTime.UtcNow;
			auxNota.Text = mvarConcept.Sympthoms;
			auxNota.UserId = mvarParent.userContext.mvarUser.guid;
			auxNota.SessionToken = Common.TelegramToken;
			auxNota.Type = (byte)(mvarConcept.Incidence ? 1 : 0);
			using IServiceScope scope = mvarParent.parent.services.CreateScope();
			AeneasClient auxCliente = scope.ServiceProvider.GetRequiredService<AeneasClient>();
			await auxCliente.addNote(auxNota);
			if (mvarConcept.Incidence)
			{
				await auxCliente.openFailReport(train);
			}						
		}
		internal async override Task InternalResponseFromBot(ITelegramBotClient client)
		{
			if(null==mvarConcept)
			{
				TextResponse auxQueryPrompt = new TextResponse();
				auxQueryPrompt.addCatalog("tg.ask.other.1", "tg.ask.other.2");
				await auxQueryPrompt.Send(client, mvarParent.userContext);
			}
			else
			{
				await mvarConcept.Confirmation(mvarParent.userContext?.Locale ?? Sapphire2025Models.I18n.UiLocale.Spanish).Send(client, mvarParent.userContext);
			}
		}
	}
}

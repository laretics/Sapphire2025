using Sapphire2025Models.Aeneas;
using Sapphire2025Server.Controllers;
using Sapphire2025Server.Models;
using Sapphire2025Server.Telegram.Semantics;
using Sapphire2025Server.Telegram.Semantics.Concepts;
using Sapphire2025Server.Telegram.Semantics.Responses;
using System.Diagnostics;
using static System.Net.Mime.MediaTypeNames;

namespace Sapphire2025Server.Telegram.Semantics.Conversations
{
	internal class ThemeDamageReport:BotTheme
	{
		internal DamageReportConcept concept { get; set; }
		internal string damageDescription { get; set; } = string.Empty; //Descripción del daño reportado
		internal ThemeDamageReport(DamageReportConcept concept, BotTask parent) : base(parent)
		{
			this.concept = concept;
		}
		internal async override Task<Response> ResponseFromBot()
		{
			if (null == concept.ToTrain)
				return new NoTrainSelectedResponse();				
			if(string.Empty == damageDescription)
				return new DamageReportDataRequestResponse(concept.ToTrain);
			if(damageDescription.Length>0 && null!=concept && null!=concept.ToTrain && null!=concept.ToTrain.mvarTren)				
			{
				endTheme();
				return new DamageReportSucessfullResponse(concept.ToTrain.mvarTren);				
			}

			return await base.ResponseFromBot();
		}
		internal async override Task textToBot(string text)
		{
			if(null == concept.ToTrain)
			{
				concept.ToTrain = await seekTrainConcept(text);
			}
			else //Lo que queda es el texto... aquí damos de alta el informe
			{
				damageDescription = text.Trim();
				if(damageDescription.Length > 0)
				{
					await createDamageReport(); //Damos de alta el informe					
				}
			}
		}

		internal async Task createDamageReport()
		{
			//Debug.Assert(null != concept);
			Debug.Assert(null != concept.ToTrain);
			Debug.Assert(null != concept.ToTrain.mvarTren);
			Train auxTren = concept.ToTrain.mvarTren; //Guardamos el tren para no perderlo al crear el informe.
			NoteModel nueva = new NoteModel();
			nueva.parent = auxTren.Guid;
			nueva.Type = 1; //Parte de avería.
			nueva.UserId = mvarParent.user.guid;
			nueva.TimeStamp = DateTime.Now;
			nueva.Text = damageDescription;
			await SapphireAeneasController.addNoteStatic(nueva,BotTask.config); //Damos de alta el informe de daños en la base de datos.
												 //Si el tren estaba en estado disponible, lo pasamos a "pendiente de diagnóstico"

			await SapphireAeneasController.CommitTrainStatusFromTelegram(auxTren.Guid, mvarParent.user.guid, Sapphire2025Models.Common.OperationType.CorrectiveRequest,BotTask.config);
		}
	}
}

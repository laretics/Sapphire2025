using Microsoft.EntityFrameworkCore;
using Sapphire2025Models.Aeneas;
using Sapphire2025Models.Authentication;
using Sapphire2025Server.Controllers;
using Sapphire2025Server.Models;
using Sapphire2025Server.Telegram.Semantics;
using Sapphire2025Server.Telegram.Semantics.Concepts;
using System.Diagnostics;

namespace Sapphire2025Server.Telegram.Semantics.Conversations
{
	internal class ThemeTrainReport:BotTheme
	{
		internal TrainReportConcept concept { get; private set; }
		internal ThemeTrainReport(TrainReportConcept concept, BotTask parent) : base(parent)
		{
			this.concept = concept;
		}
		//internal override async Task<Response> ResponseFromBot()
		//{
		//	if(null == concept.ToTrain) //Si no hay tren, no hay nada que reportar.
		//		return new NoTrainSelectedResponse();
		//	else
		//	{
		//		Debug.Assert(concept.ToTrain.mvarTren != null);
		//		endTheme();
		//		return await makeResponse();
		//	}
		//}

		//internal async Task<TrainInfoResponse> makeResponse()
		//{
		//	//Aquí vamos a recopilar toda la información que podemos mostrar al usuario.
		//	Debug.Assert(null!=concept.ToTrain);
		//	Debug.Assert(null != concept.ToTrain.mvarTren);
		//	TrainModel auxModelo = await SapphireAeneasController.trainFromTrain(concept.ToTrain.mvarTren, BotTask.config);
		//	UserModel? auxUserModel = null;
		//	using (DataStorage almacen = new DataStorage(BotTask.config))
		//	{
		//		User? usuario = await almacen.Users.Where(x => x.Id == auxModelo.lastUserInfo.ToString()).FirstOrDefaultAsync();
		//		if (usuario != null)
		//		{
		//			auxUserModel = await SapphireAuthenticationController.modeloFromUser(usuario,BotTask.config);
		//		}				
		//	}
		//	return new TrainInfoResponse(auxModelo,auxUserModel,mvarParent.user.guid);
		//}

		//internal override async Task textToBot(string text)
		//{
			
		//	if (null == concept.ToTrain)
		//	{
		//		concept.ToTrain = await seekTrainConcept(text);
		//	}
		//	else
		//	{
		//		//De alguna manera no queremos el informe. Nos salimos
		//		endTheme();
		//	}
		//}
	}
}

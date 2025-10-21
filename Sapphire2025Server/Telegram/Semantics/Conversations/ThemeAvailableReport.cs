using Microsoft.EntityFrameworkCore;
using Sapphire2025Models.Aeneas;
using Sapphire2025Server.Controllers;
using Sapphire2025Server.Models;
using Sapphire2025Server.Telegram.Semantics;
using Sapphire2025Server.Telegram.Semantics.Concepts;

namespace Sapphire2025Server.Telegram.Semantics.Conversations
{
	internal class ThemeAvailableReport:BotTheme
	{
		internal AvailableReportConcept concept { get; private set; }
		internal ThemeAvailableReport(AvailableReportConcept concept, BotTask parent):base(parent)
		{
			this.concept = concept;
		}
		//internal async override Task<Response> ResponseFromBot()
		//{
		//	if(null==concept.target) //Disponibilidad de toda la flota
		//	{
		//		List<TrainModel> trenes = await availableTrains();
		//		endTheme();
		//		return new TrainsAvailableResponse(trenes);
		//	}
		//	else //Disponibilidad de un tren en concreto
		//	{
		//		if(concept.target.GetType() == typeof(TrainConcept))
		//		{
		//			TrainConcept concepto = (TrainConcept)concept.target;
		//			if (null!=concepto)
		//			{
		//				if(null!=concepto.mvarTren)
		//				{
		//					TrainModel? tren = await trainFromBase(concepto.mvarTren.Guid);
		//					if (null != tren)
		//					{
		//						endTheme();
		//						return new TrainAvailabilityResponse(tren);
		//					}
		//				}
		//			}									
		//		}				
		//	}
		//	return await base.ResponseFromBot();
		//}
		private async Task<TrainModel?> trainFromBase(Guid trainId)
		{
			using (DataStorage almacen = new DataStorage(BotTask.config))
			{
				Train? auxTren = await almacen.Trains.Where(x => x.Guid == trainId).FirstOrDefaultAsync();
				if(null!=auxTren)
				{
					TrainModel salida = await SapphireAeneasController.trainFromTrain(auxTren,BotTask.config);
				}
			}
			return null;
		}
		private async Task<List<TrainModel>> availableTrains()
		{
			List<TrainModel> entrada = new List<TrainModel>();
			using (DataStorage almacen = new DataStorage(BotTask.config))
			{
				IEnumerable<Train> trenes = await almacen.Trains.ToListAsync();
				foreach (Train tren in  trenes)
					entrada.Add(await SapphireAeneasController.trainFromTrain(tren,BotTask.config));
			}
			List<TrainModel> salida =
			entrada.Where(t => t.lastStatus== Sapphire2025Models.Common.TrainStatus.Available ||
			t.lastStatus == Sapphire2025Models.Common.TrainStatus.DepotRequested ||
			t.lastStatus == Sapphire2025Models.Common.TrainStatus.RequestToDiagnose ||
			t.lastStatus == Sapphire2025Models.Common.TrainStatus.RequestToRepair).OrderBy(x=>x.name).ToList();
			return salida;
		}
	}
}

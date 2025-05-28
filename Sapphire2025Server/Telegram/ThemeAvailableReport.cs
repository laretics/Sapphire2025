using Sapphire2025Models.Aeneas;
using Sapphire2025Server.Controllers;
using Sapphire2025Server.Telegram.Semantics;
using Sapphire2025Server.Telegram.Semantics.Concepts;
using Sapphire2025Server.Telegram.Semantics.Responses;

namespace Sapphire2025Server.Telegram
{
	internal class ThemeAvailableReport:BotTheme
	{
		internal AvailableReportConcept concept { get; private set; }
		internal ThemeAvailableReport(AvailableReportConcept concept, BotTask parent):base(parent)
		{
			this.concept = concept;
		}
		internal async override Task<Response> ResponseFromBot()
		{
			if(null==concept.target) //Disponibilidad de toda la flota
			{
				List<TrainModel> trenes = await availableTrains();
				return new TrainsAvailableResponse(trenes);
			}
			else //Disponibilidad de un tren en concreto
			{
				if(concept.target.GetType() == typeof(TrainConcept))
				{
					TrainConcept concepto = (TrainConcept)concept.target;
					if (null!=concepto)
					{
						if(null!=concepto.mvarTren)
						{
							TrainModel? tren = await trainFromBase(concepto.mvarTren.Guid);
							if (null != tren)
							{
								return new TrainAvailabilityResponse(tren);
							}
						}
					}									
				}				
			}
			return await base.ResponseFromBot();
		}
		private async Task<TrainModel?> trainFromBase(Guid trainId)
		{
			SapphireAeneasController auxControlador = new SapphireAeneasController(BotTask.config);
			return await auxControlador.TrainInfo(trainId.ToString());
		}
		private async Task<List<TrainModel>> availableTrains()
		{
			List<TrainModel> salida = new List<TrainModel>();
			SapphireAeneasController auxControlador = new SapphireAeneasController(BotTask.config);
			List<TrainModel> entrada = await auxControlador.TrainsRequest();
			salida = entrada.Where(t => t.lastStatus== Sapphire2025Models.Common.TrainStatus.Available ||
			t.lastStatus == Sapphire2025Models.Common.TrainStatus.DepotRequested ||
			t.lastStatus == Sapphire2025Models.Common.TrainStatus.RequestToDiagnose ||
			t.lastStatus == Sapphire2025Models.Common.TrainStatus.RequestToRepair).OrderBy(x=>x.name).ToList();
			return salida;
		}
	}
}

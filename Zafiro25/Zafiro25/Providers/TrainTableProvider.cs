using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Zafiro25.Data;
using Zafiro25.Models.GMao;
using Zafiro25.Models.Model;

namespace Zafiro25.Providers
{
    [ApiController]
    [Route("api/[controller]")]
	public class TrainTableProviderController:ControllerBase
	{
		private IDbContextFactory<ApplicationDbContext> mvarFactory;
        public TrainTableProviderController(IDbContextFactory<ApplicationDbContext> factory)
        {
			mvarFactory = factory;
        }

        [HttpGet]
        public async Task<FlowModel> getCurrentModel()
        {
			int maxChanges = 32;
			FlowModel salida = new FlowModel();
			ApplicationDbContext auxDb = mvarFactory.CreateDbContext();

			List<Train> auxTrains = await auxDb.Trains.ToListAsync();
			foreach (Train auxTrain in auxTrains)
			{
				TrainModel auxTrainModelo = await (deserializeTrain(auxTrain, maxChanges));
				salida.add(auxTrainModelo);
			}
			return salida;
		}
		private async Task<TrainModel> deserializeTrain(Train rhs, int maxChanges)
		{
			TrainModel salida = new TrainModel();
			if (null != rhs)
			{
				salida.id = rhs.Guid;
				salida.name = rhs.Name;
				salida.nameCloud = rhs.NameCloud;

				//Cambios de estado
				ApplicationDbContext auxDb = mvarFactory.CreateDbContext();
				ApplicationDbContext auxUserDb = mvarFactory.CreateDbContext();
				List<StatusChange> auxColChanges = await auxDb.StatusChanges.
					Where(xx => xx.TrainId.Equals(rhs.Guid)).
					OrderByDescending(xx => xx.TimeStamp).
					Take(maxChanges).
					ToListAsync();
				foreach (StatusChange auxOriginalChange in auxColChanges)
				{
					StatusChangeModel auxChange = new StatusChangeModel();
					auxChange.guid = auxOriginalChange.Guid;
					auxChange.comment = auxOriginalChange.Comment;
					auxChange.status = auxOriginalChange.Status;
					auxChange.timeStamp = auxOriginalChange.TimeStamp;
					auxChange.user = await auxUserDb.getUserModel(auxOriginalChange.UserId);
					salida.statusChanges.Add(auxChange);
				}
			}
			return salida;
		}
	}
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sapphire25Server.Storage;
using Zafiro25.Models.Model;

namespace Sapphire25Server.Controllers
{
	[ApiController]
	[Route("[controller]")]
	public class GMaoController : ControllerBase
	{
		private IDbContextFactory<DataStorage> mvarFactory;

		public GMaoController(IDbContextFactory<DataStorage> factory)
		{
			mvarFactory = factory;
		}
		[HttpGet(Name ="GetGMao")]
		[Authorize]
		public async Task<IEnumerable<TrainModel>> getCurrentModel()
		{
			int maxChanges = 32;
			List<TrainModel> salida = new List<TrainModel>();
			DataStorage auxDb = mvarFactory.CreateDbContext();

			List<Train> auxTrains = await auxDb.Trains.ToListAsync();
			foreach (Train auxTrain in auxTrains)
			{
				TrainModel auxTrainModelo = await (deserializeTrain(auxTrain, maxChanges));
				salida.Add(auxTrainModelo);
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
				DataStorage auxDb = mvarFactory.CreateDbContext();
				
				List<StatusChange> auxColChanges = await auxDb.StatusChanges.
					Where(xx => xx.TrainId.Equals(rhs.Guid)).
					OrderByDescending(xx => xx.TimeStamp).
					Take(maxChanges).
					ToListAsync();
				foreach (StatusChange auxOriginalChange in auxColChanges)
				{
					StatusChangeModel auxChange = new StatusChangeModel();
					auxChange.guid = auxOriginalChange.Guid;
					auxChange.userId = auxOriginalChange.UserId;
					auxChange.comment = auxOriginalChange.Comment;
					auxChange.status = auxOriginalChange.Status;
					auxChange.timeStamp = auxOriginalChange.TimeStamp;					
					salida.statusChanges.Add(auxChange);
				}
			}
			return salida;
		}
	}
}
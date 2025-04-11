using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sapphire2025Models.Aeneas;
using Sapphire2025Server.Models;
using System.Net;

namespace Sapphire2025Server.Controllers
{
	[ApiController]
	[Route("[controller]")]
	public class SapphireAeneasController:SapphireBaseController
	{
		public SapphireAeneasController(IConfiguration configuration) : base(configuration) 
		{ 
		}

		/// <summary>
		/// Lista de trenes actualizada.
		/// Contiene los trenes y las últimas operaciones que éstos han realizado
		/// Es la base de la representación del nuevo Aeneas
		/// </summary>
		/// <returns>La lista con los trenes</returns>
		[HttpGet("trains")]
		public async Task<List<TrainModel>> TrainsRequest()
		{
			List<TrainModel> salida = new List<TrainModel>();
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				List<Train> trenes = await almacen.Trains.ToListAsync();
				foreach (Train tren in trenes)
				{
					TrainModel auxTrain = new TrainModel();
					auxTrain.id = tren.Guid;
					auxTrain.name = tren.Name;
					auxTrain.nameCloud = tren.NameCloud;
					//Ahora obtiene los últimos movimientos de este tren...
					StatusChange? lastChange = await almacen.StatusChanges.Where(x => x.TrainId == auxTrain.id).OrderByDescending(x => x.TimeStamp).FirstOrDefaultAsync();
					if (null == lastChange)
					{
						auxTrain.lastUpdateTime = DateTime.MinValue;
						auxTrain.lastStatus = Sapphire2025Models.Common.TrainStatus.Unknown;
						auxTrain.lastUserInfo = string.Empty;
					}
					else
					{
						auxTrain.lastUpdateTime = lastChange.TimeStamp;
						auxTrain.lastStatus = lastChange.Status;
						auxTrain.lastUserInfo = lastChange.UserId;
					}
					salida.Add(auxTrain);
				}
			}
			return salida;
		}
		


	}
}

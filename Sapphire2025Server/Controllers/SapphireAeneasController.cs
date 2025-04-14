using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using Sapphire2025Models.Aeneas;
using Sapphire2025Models.Authentication;
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
		/// <summary>
		/// Obtiene un diccionario con todos los usuarios implicados en los últimos movimientos
		/// de los trenes del estado actual
		/// </summary>
		/// <returns></returns>
		[HttpGet("userstrains")]
		public async Task<Dictionary<string, UserModel>> TrainsUsers()
		{
			Dictionary<string, UserModel> salida = new Dictionary<string, UserModel>();
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				List<Train> trenes = await almacen.Trains.ToListAsync();
				foreach (Train train in trenes)
				{
					StatusChange? lastChange = await almacen.StatusChanges.Where(x => x.TrainId==train.Guid).FirstOrDefaultAsync();
					if (null !=lastChange)
					{
						if(!salida.ContainsKey(lastChange.UserId))
						{
							User? auxUser = await almacen.Users.Where(x => x.Id.Equals(lastChange.UserId)).FirstOrDefaultAsync();
							if (null != auxUser)
							{
								UserModel nuevo = new UserModel();
								nuevo.CF = auxUser.CF;
								nuevo.Name = auxUser.UserName;
								nuevo.PhoneNumber = auxUser.PhoneNumber;
								nuevo.guid = auxUser.guid;
								nuevo.Email = auxUser.Email;
								salida.Add(nuevo.guid.ToString(), nuevo);
							}
						}
					}
				}
			}
			return salida;
		}

		[HttpGet("stchngs")]
		public async Task<List<StatusChangeModel>> ChangesRequest(string trainId)
		{
			List<StatusChangeModel> salida = new List<StatusChangeModel>();
			Guid auxId = Guid.Empty;
			Guid.TryParse(trainId, out auxId);
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				List<StatusChange> auxChanges = await almacen.StatusChanges.Where(x => x.TrainId == auxId).ToListAsync();
				foreach(StatusChange auxChange in  auxChanges)
				{
					StatusChangeModel modelo = new StatusChangeModel();
					modelo.guid = auxChange.Guid;
					modelo.status = auxChange.Status;
					modelo.userId = auxChange.UserId;
					modelo.timeStamp = auxChange.TimeStamp;
					salida.Add(modelo);
				}
			}
			return salida;
		}
		


	}
}

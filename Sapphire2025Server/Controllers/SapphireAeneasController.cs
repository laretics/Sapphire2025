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
					salida.Add(await trainFromTrain(tren));
			}
			return salida;
		}



		[HttpGet("traininfo")]
		public async Task<TrainModel?> TrainInfo(string trainid)
		{
			Guid auxId = Guid.Empty;
			Guid.TryParse(trainid, out auxId);
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				Train? auxsalida = await almacen.Trains.Where(x => x.Guid == auxId).FirstOrDefaultAsync();
				if (null != auxsalida)
					return await trainFromTrain(auxsalida);
			}
			return null;
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
								salida.Add(auxUser.guid.ToString(), userFromUser(auxUser));
						}
					}
				}
			}
			return salida;
		}

		/// <summary>
		/// Lista de cambios para un tren determinado. De momento sin especificar un máximo.
		/// </summary>
		/// <param name="trainid"></param>
		/// <returns>La lista de los cambios ordenados por fecha</returns>
		[HttpGet("stchngs")]
		public async Task<List<StatusChangeModel>> ChangesRequest(string trainid)
		{
			List<StatusChangeModel> salida = new List<StatusChangeModel>();
			Guid auxId = Guid.Empty;
			Guid.TryParse(trainid, out auxId);
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				List<StatusChange> auxChanges = await almacen.StatusChanges.Where(x => x.TrainId == auxId).OrderByDescending(xx=>xx.TimeStamp).ToListAsync();
				foreach(StatusChange auxChange in  auxChanges)
					salida.Add(changeFromChange(auxChange));
			}
			return salida;
		}

		[HttpGet("rcchngs")]
		public async Task<List<StatusChangeModel>> recentUpdatesRequest(string timestamp)
		{
			List<StatusChangeModel> salida = new List<StatusChangeModel>();
			DateTime auxFecha = DateTime.Now;
			DateTime.TryParse(timestamp, out auxFecha);
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				List<StatusChange> auxChanges = await almacen.StatusChanges.Where(x=>x.TimeStamp>auxFecha).OrderBy(x=>x.TimeStamp).ToListAsync();
				foreach (StatusChange auxChange in auxChanges)
					salida.Add(changeFromChange(auxChange));
			}
			return salida;
		}

		/// <summary>
		/// Obtiene un diccionario relleno con los usuarios que han realizado alguna intervención a este tren
		/// </summary>
		/// <returns></returns>

		[HttpGet("usersstchngs")]
		public async Task<Dictionary<string,UserModel>> ChangesUsers(string trainid)
		{
			Dictionary<string, UserModel> salida = new Dictionary<string, UserModel>();
			Guid auxId = Guid.Empty;
			Guid.TryParse(trainid, out auxId);
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				List<StatusChange> auxChanges = await almacen.StatusChanges.Where(x => x.TrainId == auxId).ToListAsync();
				foreach (StatusChange auxChange in auxChanges)
				{
					if(null!=auxChange)
					{
						if(!salida.ContainsKey(auxChange.UserId))
						{
							User? auxUser = await almacen.Users.Where(x => x.Id.Equals(auxChange.UserId)).FirstOrDefaultAsync();
							if (null != auxUser)
								salida.Add(auxUser.guid.ToString(), userFromUser(auxUser));
						}
					}					
				}
			}
			return salida;
		}	

		protected UserModel userFromUser(User rhs)
		{
			UserModel salida = new UserModel();
			salida.guid = rhs.guid;
			salida.CF = rhs.CF;
			salida.Name = rhs.UserName;
			salida.PhoneNumber = rhs.PhoneNumber;
			salida.Email = rhs.Email;
			return salida;
		}

		private async Task<TrainModel> trainFromTrain(Train train)
		{
			TrainModel salida = new TrainModel();
			salida.id = train.Guid;
			salida.name = train.Name;
			salida.nameCloud = train.NameCloud;
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				//Ahora obtiene los últimos movimientos de este tren...
				StatusChange? lastChange = await almacen.StatusChanges.Where(x => x.TrainId == train.Guid).OrderByDescending(x => x.TimeStamp).FirstOrDefaultAsync();
				if (null == lastChange)
				{
					salida.lastUpdateTime = DateTime.MinValue;
					salida.lastStatus = Sapphire2025Models.Common.TrainStatus.Unknown;
					salida.lastUserInfo = string.Empty;
				}
				else
				{
					salida.lastUpdateTime = lastChange.TimeStamp;
					salida.lastStatus = lastChange.Status;
					salida.lastUserInfo = lastChange.UserId;
				}
			}
			return salida;
		}
	
		private StatusChangeModel changeFromChange(StatusChange rhs)
		{
			StatusChangeModel modelo = new StatusChangeModel();
			modelo.guid = rhs.Guid;
			modelo.trainId = rhs.TrainId;
			modelo.status = rhs.Status;
			modelo.userId = rhs.UserId;
			modelo.timeStamp = rhs.TimeStamp;
			return modelo;
		}
	}
}

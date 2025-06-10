using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using MySql.Data.MySqlClient;
using Sapphire2025Models;
using Sapphire2025Models.Aeneas;
using Sapphire2025Models.Authentication;
using Sapphire2025Server.Models;
using Sapphire2025Server.Telegram;
using System.Net;
using System.Net.Http.Headers;

namespace Sapphire2025Server.Controllers
{
	[ApiController]
	[Route("[controller]")]
	public class SapphireAeneasController:SapphireBaseController
	{
		internal static BotSoul mvarTelegramBot { get; set; }
		public SapphireAeneasController(IConfiguration configuration, BotSoul myBotSoul) : base(configuration) 
		{			
			mvarTelegramBot = myBotSoul;
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
					salida.Add(await trainFromTrain(tren,mvarConfig));
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
					return await trainFromTrain(auxsalida, mvarConfig);
			}
			return null;
		}

		/// <summary>
		/// Obtiene un diccionario con todos los usuarios implicados en los últimos movimientos
		/// de los trenes del estado actual
		/// </summary>
		/// <returns></returns>
		[HttpGet("userstrains")]
		public async Task<Dictionary<Guid, UserModel>> TrainsUsers()
		{
			Dictionary<Guid, UserModel> salida = new Dictionary<Guid, UserModel>();
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
							User? auxUser = await almacen.Users.Where(x => x.Id.Equals(lastChange.UserId.ToString())).FirstOrDefaultAsync();
							if (null != auxUser)
							{
								salida.Add(auxUser.guid, userFromUser(auxUser));
							}								
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
		public async Task<Dictionary<Guid,UserModel>> ChangesUsers(string trainid)
		{
			Dictionary<Guid, UserModel> salida = new Dictionary<Guid, UserModel>();
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
							User? auxUser = await almacen.Users.Where(x => x.Id.Equals(auxChange.UserId.ToString())).FirstOrDefaultAsync();
							if (null != auxUser)
								salida.Add(auxUser.guid, userFromUser(auxUser));
						}
					}					
				}
			}
			return salida;
		}



		[HttpPost("cmtstatus")]
		public async Task<bool> CommitStatus(TrainStatusCommitModel commit)
		{
			bool salida = false;
			if (await credentialValidForTrainOperation(commit))
			{
				using (DataStorage almacen = new DataStorage(mvarConfig))
				{
					Train? auxTrain = await almacen.Trains.Where(x => x.Guid == commit.trainId).FirstOrDefaultAsync();
					if (null != auxTrain)
					{
						StatusChange nuevoCambio = new StatusChange();
						nuevoCambio.Guid = Guid.NewGuid();
						nuevoCambio.TrainId = auxTrain.Guid;
						nuevoCambio.Operation = commit.operation;
						nuevoCambio.TimeStamp = DateTime.Now;
						User? auxUser = await retrieveSessionUser(commit.SessionToken);
						if(null!=auxUser)
							nuevoCambio.UserId = auxUser.guid;
						almacen.StatusChanges.Add(nuevoCambio);						
						auxTrain.lastChange = nuevoCambio.Guid;
						salida = (await almacen.SaveChangesAsync() > 0);
						await TelegramNotify(nuevoCambio, auxTrain,mvarConfig);
					}
				}
			}
			return salida;
		}

		public static async Task<bool> CommitTrainStatusFromTelegram(Guid trainId, Guid userId, Common.OperationType operation, IConfiguration config)
		{
			using (DataStorage almacen = new DataStorage(config))
			{
				Train? auxTrain = await almacen.Trains.Where(x => x.Guid == trainId).FirstOrDefaultAsync();
				if (null != auxTrain)
				{
					if(operation== Common.OperationType.CorrectiveRequest)
					{
						StatusChange? ultimoCambio = await almacen.StatusChanges.Where(x => x.TrainId == auxTrain.Guid).OrderByDescending(x => x.TimeStamp).FirstOrDefaultAsync();
						if (null != ultimoCambio && !(ultimoCambio.Operation == Common.OperationType.EndMaintenance ||
							ultimoCambio.Operation == Common.OperationType.EndCorrective))
						{
							//No metemos un cambio a diagnóstico si es incompatible la situación.
							return false;
						}
					}

					StatusChange nuevoCambio = new StatusChange();
					nuevoCambio.Guid = Guid.NewGuid();
					nuevoCambio.TrainId = auxTrain.Guid;
					nuevoCambio.Operation = operation;
					nuevoCambio.TimeStamp = DateTime.Now;
					nuevoCambio.UserId = userId;
					almacen.StatusChanges.Add(nuevoCambio);
					auxTrain.lastChange = nuevoCambio.Guid;
					await TelegramNotify(nuevoCambio,auxTrain,config);
					return (await almacen.SaveChangesAsync() > 0);
				}
				else
				{
					return false; //No se ha encontrado el tren.
				}
			}
		}

		/// <summary>
		/// Notificación a todos los usuarios registrados en Telegram del cambio de estado
		/// en uno de los trenes (Sólo si le afecta).
		/// </summary>
		/// <param name="statusChange"></param>
		/// <returns></returns>
		private static async Task TelegramNotify(StatusChange statusChange, Train train, IConfiguration config)
		{
			User? usuario = await retrieveUserStatic(statusChange.UserId,config);
			string nombreUsuario = "un usuario desconocido";
			if (usuario != null) nombreUsuario = usuario.UserName;
			switch(statusChange.Operation)
			{
				case Common.OperationType.EndMaintenance:
					await mvarTelegramBot.Broadcast(string.Format("La UT {0} acaba de reincorporarse a la circulación tras terminar {1} los trabajos planificados.", train.Name, nombreUsuario), new Common.UserRole[] { Common.UserRole.Inspector }); break;
				case Common.OperationType.EndCorrective:
					await mvarTelegramBot.Broadcast(string.Format("La UT {0} acaba de reincorporarse a la circulación tras dar {1} por terminada la reparación.", train.Name, nombreUsuario), new Common.UserRole[] { Common.UserRole.Inspector }); break;
				case Common.OperationType.CorrectiveRequest:
					await mvarTelegramBot.Broadcast(string.Format("{1} acaba de hacer un parte de avería sobre la UT {0}.", train.Name, nombreUsuario), new Common.UserRole[]{ Common.UserRole.Inspector, Common.UserRole.Expert, Common.UserRole.Oficial, Common.UserRole.Mechanic }); break;
				case Common.OperationType.DiagnoseToFault:
					await mvarTelegramBot.Broadcast(string.Format("{1} acaba de declarar una avería. La UT {0} debe ser retirada de la circulación.", train.Name, nombreUsuario), new Common.UserRole[] { Common.UserRole.Inspector }); break;
				case Common.OperationType.DiagnoseToAvailable:
					await mvarTelegramBot.Broadcast(string.Format("{1} considera que la UT {0} puede seguir en servicio.", train.Name, nombreUsuario), new Common.UserRole[] { Common.UserRole.Inspector }); break;
				case Common.OperationType.BeginCorrective:
					await mvarTelegramBot.Broadcast(string.Format("{1} ha dado entrada en taller a la UT {0} para correctivo.", train.Name, nombreUsuario), new Common.UserRole[] { Common.UserRole.Oficial, Common.UserRole.Engineer }); break;
				case Common.OperationType.DepotRequest:
					await mvarTelegramBot.Broadcast(string.Format("{1} solicita apartar la UT {0} para mantenimiento planificado.", train.Name, nombreUsuario), new Common.UserRole[] { Common.UserRole.Inspector }); break;
				case Common.OperationType.DepotRequestAccept:
					await mvarTelegramBot.Broadcast(string.Format("{1} acaba de apartar la UT {0} para mantenimiento planificado.", train.Name, nombreUsuario), new Common.UserRole[] { Common.UserRole.Oficial, Common.UserRole.Mechanic }); break;
				case Common.OperationType.MaintenanceRescue:
				case Common.OperationType.DiferMaintenance:
					await mvarTelegramBot.Broadcast(string.Format("{1} devuelve a la circulación la UT {0} que había solicitado taller para mantenimiento planificado.", train.Name, nombreUsuario), new Common.UserRole[] { Common.UserRole.Inspector }); break;
				case Common.OperationType.SendToStandStill:
					await mvarTelegramBot.Broadcast(string.Format("{1} envía la UT {0} al estado \"Stand-Still\" .", train.Name, nombreUsuario), new Common.UserRole[] { Common.UserRole.Engineer, Common.UserRole.Oficial }); break;
				case Common.OperationType.Activate:
					await mvarTelegramBot.Broadcast(string.Format("{1} acaba de activar la UT {0} en el sistema.", train.Name, nombreUsuario), new Common.UserRole[] { Common.UserRole.Engineer, Common.UserRole.Oficial }); break;
				case Common.OperationType.RescueFromStandStill:
					await mvarTelegramBot.Broadcast(string.Format("{1} ha reactivado la UT {0} desde el estado de Stand-Still. Ahora está asignada a taller para revisión.", train.Name, nombreUsuario), new Common.UserRole[] { Common.UserRole.Engineer, Common.UserRole.Oficial, Common.UserRole.Mechanic }); break;
			}	
		}

		[HttpPost("addnote")]
		public async Task<bool> AddNote(NoteModel note)
		{
			return await addNoteStatic(note, mvarConfig);
		}
		[HttpPost("getnotes")]
		public async Task<List<NoteModel>> RetrieveNotes(NoteChatRequestModel model)
		{
			List<NoteModel> salida = new List<NoteModel>();
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				List<Note> auxNotas;
				if (model.TakeMax > 0)
				{
					auxNotas = await almacen.Notes.Where(x => x.Parent == model.ParentId).OrderByDescending(x => x.TimeStamp).Take(model.TakeMax).ToListAsync();
				}
				else
				{
					auxNotas = await almacen.Notes.Where(x => x.Parent == model.ParentId).OrderByDescending(x => x.TimeStamp).ToListAsync();
				}
				foreach (Note auxNota in auxNotas)
				{
					NoteModel nuevoModelo = new NoteModel();
					nuevoModelo.parent = auxNota.Parent;
					nuevoModelo.Text = auxNota.Text;
					nuevoModelo.TimeStamp = auxNota.TimeStamp;
					nuevoModelo.UserId = auxNota.UserId;
					nuevoModelo.Type = auxNota.Type;
					salida.Add(nuevoModelo);
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

		public static async Task<bool> addNoteStatic(NoteModel note, IConfiguration config)
		{
			bool salida = false;
			//Todos los usuarios tienen permiso para añadir notas.
			using (DataStorage almacen = new DataStorage(config))
			{
				if (null != note.Text && note.Text.Length > 0)
				{
					Note nuevaNota = new Note();
					nuevaNota.Id = Guid.NewGuid();
					nuevaNota.Parent = note.parent;
					nuevaNota.TimeStamp = DateTime.Now;
					nuevaNota.UserId = note.UserId;
					nuevaNota.Text = note.Text;
					nuevaNota.Type = note.Type;
					almacen.Notes.Add(nuevaNota);
					salida = (await almacen.SaveChangesAsync() > 0);
				}
			}
			return salida;
		}
		internal static async Task<TrainModel> trainFromTrain(Train train, IConfiguration config)
		{
			TrainModel salida = new TrainModel();
			salida.id = train.Guid;
			salida.name = train.Name;
			salida.nameCloud = train.NameCloud;
			using (DataStorage almacen = new DataStorage(config))
			{
				//Ahora obtiene los últimos movimientos de este tren...
				StatusChange? lastChange = await almacen.StatusChanges.Where(x => x.TrainId == train.Guid).OrderByDescending(x => x.TimeStamp).FirstOrDefaultAsync();
				if (null == lastChange)
				{
					salida.lastUpdateTime = DateTime.MinValue;
					salida.lastStatus = Sapphire2025Models.Common.TrainStatus.Unknown;
					salida.lastUserInfo = Guid.Empty;
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
		private async Task<User?> retrieveUser(Guid userId)
		{
			return await retrieveUserStatic(userId, mvarConfig);
		}
		private async static Task<User?> retrieveUserStatic(Guid userId, IConfiguration config)
		{
			using (DataStorage almacen = new DataStorage(config))
			{
				User? salida = await almacen.Users.Where(x => x.Id.Equals(userId.ToString())).FirstOrDefaultAsync();
				return salida;
			}
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
		private async Task<bool> credentialValidForTrainOperation(TrainStatusCommitModel? request)
		{
			if(null == request) return false;
			bool salida = false;
			switch (request.operation)
			{
				case Common.OperationType.Activate:
					return await hasBasicPermission(request, Common.UserRole.Oficial);
				case Common.OperationType.BeginCorrective:
					salida = await hasBasicPermission(request, Common.UserRole.Oficial); //El oficial de taller puede reintegra un tren stand-still
					if(!salida)
						salida = await hasBasicPermission(request, Common.UserRole.Inspector); //El inspector puede mandar un tren a reparar
					return salida;							
				case Common.OperationType.DepotRequestAccept: //El inspector acepta enviar un tren a mantenimiento
					salida = await hasBasicPermission(request, Common.UserRole.Inspector); //Es importante que ningún otro pueda tomar esta decisión.
					return salida;
				case Common.OperationType.DepotRequestDeny: //El oficial de taller puede rescatar un tren del que se ha pedido un mantenimiento sin querer.
					return await hasBasicPermission(request, Common.UserRole.Oficial);
				case Common.OperationType.DepotRequest: //Solicitud de preventivo
				case Common.OperationType.BeginMaintenance: //Puede comenzar el mantenimiento un oficial o un mecánico
				case Common.OperationType.EndMaintenance: //Cualquier mecánico y cualquier oficial pueden terminar el mantenimiento
				case Common.OperationType.MaintenanceRescue: //Puede devolver a la vía un tren que ha sido retirado para mantenimiento un oficial o un mecánico
				case Common.OperationType.EndCorrective:
					salida = await hasBasicPermission(request, Common.UserRole.Oficial);
					if (!salida)
						salida = await hasBasicPermission(request, Common.UserRole.Mechanic);
					return salida;
				case Common.OperationType.CorrectiveRequest: //Abrimos parte de avería, para diagnóstico.
					return true; //Aquí puede abrir un parte hasta el apuntador.
				case Common.OperationType.DiagnoseToFault: //Evaluación del experto sobre retirada de un tren
				case Common.OperationType.DiagnoseToAvailable:
					salida = await hasBasicPermission(request, Common.UserRole.Expert);
					if (!salida)
						salida = await hasBasicPermission(request, Common.UserRole.Oficial);
					if(!salida)
						salida = await hasBasicPermission(request, Common.UserRole.Mechanic);
					if(!salida)
						salida = await hasBasicPermission(request, Common.UserRole.Inspector);
					return salida;
				case Common.OperationType.SendToStandStill:
					salida = await hasBasicPermission(request, Common.UserRole.Engineer);
					return salida;
				case Common.OperationType.SendToDisabled:
					salida = await hasBasicPermission(request, Common.UserRole.Engineer);
					return salida;


				//TODO: Agregar la gestión de permisos para el resto de operaciones
				default:
					return false;			
			}
		}

	}
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Crypto.Operators;
using Sapphire2025Models;
using Sapphire2025Models.Aeneas;
using Sapphire2025Models.Authentication;
using Sapphire2025Models.GMao;
using Sapphire2025Server.Comunications;
using Sapphire2026.Data;
using Sapphire2026.Data.Models;
using Sapphire2026Data.Models;
using Sapphire2026Data.Models.GMAO;
using System.Configuration;

namespace Sapphire2025Server.Controllers
{	
	[ApiController]
	[Route("api/[controller]")]
	public partial class SapphireAeneasController:SapphireBaseController
	{
		private ILogger<SapphireAeneasController> mvarLogger;


		public SapphireAeneasController
			(IConfiguration configuration,
			IHubContext<SignalRHub> hubContext,
			ILogger<SapphireAeneasController> logger) : base(configuration, hubContext) { mvarLogger = logger; }
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
				List<TrainSnapshot> trenes = await almacen.Trains.AsNoTracking()
					.Select(train => new TrainSnapshot
					{
						Train = train,
						LastChange = almacen.StatusChanges.AsNoTracking()
							.Where(change => change.TrainId == train.Guid)
							.OrderByDescending(change => change.TimeStamp)
							.FirstOrDefault(),
						LastNote = almacen.Notes.AsNoTracking()
							.Where(note => note.Parent == train.Guid && note.Text != null)
							.OrderByDescending(note => note.TimeStamp)
							.Select(note => note.Text)
							.FirstOrDefault() ?? string.Empty,
						Locked = almacen.WorkOrders.AsNoTracking()
							.Any(order => order.TrainId == train.Guid &&
								!order.Rejected &&
								order.Atomic &&
								(order.OpenTime != null || order.CloseTime != null))
					}
					)
					.ToListAsync();

				foreach (TrainSnapshot tren in trenes)
					salida.Add(trainFromTrain(tren.Train, tren.LastChange, tren.LastNote, tren.Locked));
			}
			return salida;
		}
		[HttpGet("platforms")]
		public async Task<List<PlatformModel>> PlatformsRequest()
		{
			List<PlatformModel> salida = new List<PlatformModel>();
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				List<Platform> andenes = await almacen.Platforms.ToListAsync();
				foreach(Platform platform in andenes)
				{
					PlatformModel nuevo = new PlatformModel();
					nuevo.Id = platform.Id;
					nuevo.StationName = platform.StationId;
					nuevo.PlatformName = platform.PlatformId;
					salida.Add(nuevo);
				}
			}
			return salida;
		}

		private sealed class TrainSnapshot
		{
			public Train Train { get; set; } = default!;
			public StatusChange? LastChange { get; set; }
			public string LastNote { get; set; } = string.Empty;
			public bool Locked { get; set; }
		}
		private static TrainModel trainFromTrain(Train train, StatusChange? lastChange, string lastNote, bool locked)
		{
			TrainModel salida = new TrainModel();
			salida.id = train.Guid;
			salida.name = train.Name;
			salida.nameCloud = train.NameCloud;
			salida.PlatformId = train.PlatformId;
			salida.LastWash = train.LastWash;
			salida.Locked = locked;
			salida.lastNote = lastNote;
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
				List<Guid> trainIds = await almacen.Trains.AsNoTracking()
					.Select(train => train.Guid)
					.ToListAsync();
				if (trainIds.Count == 0)
					return salida;

				Dictionary<Guid, StatusChange> lastChanges = new Dictionary<Guid, StatusChange>();
				List<StatusChange> statusChanges = await almacen.StatusChanges.AsNoTracking()
					.Where(change => trainIds.Contains(change.TrainId))
					.OrderByDescending(change => change.TimeStamp)
					.ToListAsync();
				foreach (StatusChange change in statusChanges)
				{
					if (!lastChanges.ContainsKey(change.TrainId))
						lastChanges.Add(change.TrainId, change);
				}

				HashSet<string> userIds = new HashSet<string>(lastChanges.Values.Select(change => change.UserId.ToString()));
				if (userIds.Count == 0)
					return salida;

				List<User> users = await almacen.Users.AsNoTracking()
					.Where(user => userIds.Contains(user.Id))
					.ToListAsync();

				foreach (User auxUser in users)
				{
					if (!salida.ContainsKey(auxUser.guid))
					{
							salida.Add(auxUser.guid, userFromUser(auxUser));
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
		[HttpPost("stchngs")]
		public async Task<List<StatusChangeModel>> ChangesRequest(StatusChangeRequestModel request)
		{
			List<StatusChangeModel> salida = new List<StatusChangeModel>();
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				List<StatusChange> auxChanges = await
				almacen.StatusChanges.Where(x => x.TrainId == request.trainId &&
						x.TimeStamp > request.oldestRecord).
						OrderByDescending(xx => xx.TimeStamp).ToListAsync();
				foreach (StatusChange auxChange in auxChanges)
					salida.Add(changeFromChange(auxChange));
			}
			return salida;
		}

		[HttpGet("rcchngs")]
		public async Task<List<StatusChangeModel>> recentUpdatesRequest(string timestamp)
		{
			List<StatusChangeModel> salida = new List<StatusChangeModel>();
			DateTime auxFecha = DateTime.UtcNow;
			DateTime.TryParse(timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out auxFecha);
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				List<StatusChange> auxChanges = await almacen.StatusChanges
					.Where(x=>x.TimeStamp>auxFecha)
					.OrderBy(x=>x.TimeStamp)
					.ToListAsync();
				foreach (StatusChange auxChange in auxChanges)
					salida.Add(changeFromChange(auxChange));
			}
			return salida;
		}

		/// <summary>
		/// Obtiene un diccionario relleno con los usuarios que han realizado alguna intervención a este tren
		/// </summary>
		/// <returns></returns>

		public async Task<Dictionary<Guid, UserModel>> ChangesUsers()
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				Guid? auxTrainId = await almacen.Trains.AsNoTracking()
					.Select(train => (Guid?)train.Guid)
					.FirstOrDefaultAsync();
				if (!auxTrainId.HasValue)
					return new Dictionary<Guid, UserModel>();

				return await ChangesUsers(auxTrainId.Value.ToString());
			}
		}

		[HttpGet("usersstchngs")]
		public async Task<Dictionary<Guid,UserModel>> ChangesUsers(string trainid)
		{
			Dictionary<Guid, UserModel> salida = new Dictionary<Guid, UserModel>();
			Guid auxId = Guid.Empty;
			Guid.TryParse(trainid, out auxId);
			if (Guid.Empty == auxId)
				return salida;
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				List<string> userIds = await almacen.StatusChanges.AsNoTracking()
					.Where(x => x.TrainId == auxId)
					.Select(x => x.UserId.ToString())
					.Distinct()
					.ToListAsync();

				if (userIds.Count == 0)
					return salida;

				List<User> users = await almacen.Users.AsNoTracking()
					.Where(user => userIds.Contains(user.Id))
					.ToListAsync();

				foreach (User auxUser in users)
					if (!salida.ContainsKey(auxUser.guid))
						salida.Add(auxUser.guid, userFromUser(auxUser));
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
						nuevoCambio.TimeStamp = DateTime.UtcNow; // Cambiar de DateTime.Now
						User? auxUser = await retrieveSessionUser(commit.SessionToken);
						if(null!=auxUser)
							nuevoCambio.UserId = auxUser.guid;
						almacen.StatusChanges.Add(nuevoCambio);						
						auxTrain.lastChange = nuevoCambio.Guid;
						salida = (await almacen.SaveChangesAsync() > 0);
						await TelegramNotify(nuevoCambio, auxTrain, mvarConfig);
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
					nuevoCambio.TimeStamp = DateTime.UtcNow;
					nuevoCambio.UserId = userId;
					almacen.StatusChanges.Add(nuevoCambio);
					auxTrain.lastChange = nuevoCambio.Guid;
					//await TelegramNotify(nuevoCambio,auxTrain,config);
					return (await almacen.SaveChangesAsync() > 0);
				}
				else
				{
					return false; //No se ha encontrado el tren.
				}
			}
		}

		[HttpPost("telegrambroadcast")]
		public async Task<bool> TelegramBroadcast(TelegramBroadcastRequestModel request)
		{
			if(null!=request.Message && null!=request.Roles)
			{
				try
				{
					await mvarHubContext.Clients.All.SendAsync("ReceiveBroadcastRequest2", request.Message, request.Priority, request.Roles.ToArray());
					return true;
				}
				catch(Exception e)
				{
					mvarLogger.LogError($"TelegramBroadcast: {e.ToString()}");
				}				
			}
			return false;
		}


		private async Task<bool> SendTelegramBroadcast(string message, bool priority =false, string filters = "")
		{
			try
			{
				await mvarHubContext.Clients.All.SendAsync("ReceiveBroadcastRequest1",message,priority,filters);
				return true;
			}
			catch (Exception ex)
			{
				return false;
			}
		}
		private async Task<bool> SendTelegramBroadcast(string message, bool priority = false, params Common.UserRole[] roles)
		{
			try
			{
				await mvarHubContext.Clients.All.SendAsync("ReceiveBroadcastRequest2", message, priority, roles);
				return true;
			}
			catch (Exception ex)
			{
				mvarLogger.LogError($"TelegramBroadcast: {ex.ToString()}");
				return false;
			}
		}

		/// <summary>
		/// Notificación a todos los usuarios registrados en Telegram del cambio de estado
		/// en uno de los trenes (Sólo si le afecta).
		/// </summary>
		/// <param name="statusChange"></param>
		/// <returns></returns>
		private async Task TelegramNotify(StatusChange statusChange, Train train, IConfiguration config)
		{
			User? usuario = await retrieveUserStatic(statusChange.UserId,config);
			string nombreUsuario = "un usuario desconocido";
			string ultimoParte = string.Empty;
			if (null!=usuario && null!=usuario.UserName) nombreUsuario = usuario.UserName;
			switch(statusChange.Operation)
			{
				case Common.OperationType.EndMaintenance:
					await SendTelegramBroadcast(string.Format("La UT {0} acaba de reincorporarse a la circulación tras terminar {1} los trabajos planificados.", train.Name, nombreUsuario), false, new Common.UserRole[] { Common.UserRole.Inspector });
					break;
				case Common.OperationType.EndCorrective:					
					await SendTelegramBroadcast(string.Format("La UT {0} acaba de reincorporarse a la circulación tras dar {1} por terminada la reparación.", train.Name, nombreUsuario), false, new Common.UserRole[] { Common.UserRole.Inspector }); 
					break;
				case Common.OperationType.CorrectiveRequest:
					ultimoParte = await lastNoteStatic(train.Guid, config);
					await SendTelegramBroadcast(string.Format("{1} abrió incidencia a la UT {0}: \"{2}\"", train.Name, nombreUsuario, ultimoParte), false, new Common.UserRole[] { Common.UserRole.Inspector, Common.UserRole.Expert, Common.UserRole.Oficial, Common.UserRole.Mechanic }); break;
				case Common.OperationType.DiagnoseToFault:
					ultimoParte = await lastNoteStatic(train.Guid, config);
					await SendTelegramBroadcast(string.Format("{1} pide retirar el tren {0} de la circulación. \"{2}\"", train.Name, nombreUsuario,ultimoParte), false, new Common.UserRole[] { Common.UserRole.Inspector, Common.UserRole.Station }); break;
				case Common.OperationType.DiagnoseToAvailable:
					ultimoParte = await lastNoteStatic(train.Guid, config);
					await SendTelegramBroadcast(string.Format("{1} considera que la UT {0} puede seguir en servicio. La última nota es:\"{2}\"", train.Name, nombreUsuario,ultimoParte), false, new Common.UserRole[] { Common.UserRole.Inspector }); break;
				case Common.OperationType.BeginCorrective:
					await SendTelegramBroadcast(string.Format("{1} ha dado entrada en taller a la UT {0} para correctivo.", train.Name, nombreUsuario), false, new Common.UserRole[] { Common.UserRole.Oficial, Common.UserRole.Engineer, Common.UserRole.Inspector, Common.UserRole.Station }); break;
				case Common.OperationType.DepotRequest:
					await SendTelegramBroadcast(string.Format("{1} solicita apartar la UT {0} para mantenimiento planificado.", train.Name, nombreUsuario), false, new Common.UserRole[] { Common.UserRole.Inspector, Common.UserRole.Station }); break;
				case Common.OperationType.DepotRequestAccept:
					await SendTelegramBroadcast(string.Format("{1} acaba de apartar la UT {0} para mantenimiento planificado.", train.Name, nombreUsuario), false, new Common.UserRole[] { Common.UserRole.Oficial, Common.UserRole.Mechanic, Common.UserRole.Inspector, Common.UserRole.Station }); break;
				case Common.OperationType.MaintenanceRescue:
				case Common.OperationType.DiferMaintenance:
					await SendTelegramBroadcast(string.Format("{1} devuelve a la circulación la UT {0} que había solicitado taller para mantenimiento planificado.", train.Name, nombreUsuario), false, new Common.UserRole[] { Common.UserRole.Inspector, Common.UserRole.Station }); break;
				case Common.OperationType.SendToStandStill:
					await SendTelegramBroadcast(string.Format("{1} envía la UT {0} al estado \"Stand-Still\" .", train.Name, nombreUsuario), false, new Common.UserRole[] { Common.UserRole.Engineer, Common.UserRole.Oficial }); break;
				case Common.OperationType.Activate:
					await SendTelegramBroadcast(string.Format("{1} acaba de activar la UT {0} en el sistema.", train.Name, nombreUsuario), false, new Common.UserRole[] { Common.UserRole.Engineer, Common.UserRole.Oficial }); break;
				case Common.OperationType.RescueFromStandStill:
					await SendTelegramBroadcast(string.Format("{1} ha reactivado la UT {0} desde el estado de Stand-Still. Ahora está asignada a taller para revisión.", train.Name, nombreUsuario), false, new Common.UserRole[] { Common.UserRole.Engineer, Common.UserRole.Oficial, Common.UserRole.Mechanic });break;
			}	
		}


		[HttpPost("changeplatform")]
		public async Task<bool> ChangePlatform(TrainModel train)
		{
			using(DataStorage almacen = new DataStorage(mvarConfig))
			{
				Train? auxTren = await almacen.Trains.Where(x => x.Guid == train.id).FirstOrDefaultAsync();
				if (null!=auxTren)
				{
					auxTren.PlatformId = train.PlatformId;
					auxTren.LastPlatformAssign = DateTime.UtcNow; //Se tiene en cuenta la fecha de asignación.
					await almacen.SaveChangesAsync();
					return true;
				}
			}
			return false;
		}

		[HttpPost("updatewash")]
		public async Task<bool> UpdateWash(TrainModel train)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				Train? auxTren = await almacen.Trains.Where(x => x.Guid == train.id).FirstOrDefaultAsync();
				if (null != auxTren)
				{
					auxTren.LastWash = DateTime.UtcNow; //Actualizo la última fecha de lavado.
					int changes = await almacen.SaveChangesAsync();
					return changes>0;
				}
			}
			return false;
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

		internal static async Task<TrainModel> trainFromTrain(Train train, IConfiguration config)
		{
			TrainModel salida = new TrainModel();
			salida.id = train.Guid;
			salida.name = train.Name;
			salida.nameCloud = train.NameCloud;
			salida.PlatformId = train.PlatformId;
			salida.LastWash = train.LastWash;
			salida.Locked = await isTrainLocked(train.Guid, config);
			salida.lastNote = await lastNoteStatic(train.Guid, config);			
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
		private static async Task<bool> isTrainLocked(Guid trainId, IConfiguration config)
		{
			using (DataStorage almacen = new DataStorage(config))
			{
				return await almacen.WorkOrders.AsNoTracking().AnyAsync(order =>
					order.TrainId == trainId &&
					!order.Rejected &&
					order.Atomic &&
					(order.OpenTime != null || order.CloseTime != null));
			}
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
			modelo.operation = rhs.Operation;
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
						salida = await hasBasicPermission(request, Common.UserRole.Station); //El inspector puede mandar un tren a reparar
					if(!salida)
						salida = await hasBasicPermission(request, Common.UserRole.Inspector); //El inspector puede mandar un tren a reparar
					return salida;							
				case Common.OperationType.DepotRequestAccept: //El inspector acepta enviar un tren a mantenimiento
					salida = await hasBasicPermission(request, Common.UserRole.Station); //Es importante que ningún otro pueda tomar esta decisión.
					if(!salida)
						salida = await hasBasicPermission(request, Common.UserRole.Inspector); //El inspector puede mandar un tren a reparar
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

		#region Ordenes GMao


		#endregion Ordenes GMao
	}
}

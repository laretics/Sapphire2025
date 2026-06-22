using Sapphire2025Models;
using Sapphire2025Models.Aeneas;
using Sapphire2025Models.Authentication;
using Sapphire2025Models.GMao;
using System.Net.Http.Json;
using System.Reflection.Metadata.Ecma335;

namespace Sapphire2025.Storage
{
	public class AeneasClient:HttpClientBase
	{
		public AeneasClient(HttpClient httpClient, IntStorageService intStorage) : base(httpClient, intStorage, "sapphireaeneas") { }
		
		public async Task<bool> openFailReport(TrainModel? train)
		{

			if(null!=train)
			{
				//La apertura de un parte de averías envía el tren al estado de
				//solicitud de diagnóstico sólo si el tren está activo o bien
				//en estado de solicitud de diagnóstico.
				Common.TrainStatus currentStatus = train.lastStatus;
				if (currentStatus == Common.TrainStatus.Available ||
					currentStatus == Common.TrainStatus.RequestToDiagnose)
				{
					return await commitTrainStatus(train.id, Common.OperationType.CorrectiveRequest);
				}
				else
					return true; //En cualquier otro estado la salida es correcta y no se hace nada.
			}
			return false;
		}
		public async Task<IEnumerable<TrainModel>> trainsList()
		{
			string request = composeCommand("trains");
			try
			{
                HttpResponseMessage respuesta = await sendGetRequest(request);
                IEnumerable<TrainModel>? auxLista = await respuesta.Content.ReadFromJsonAsync<IEnumerable<TrainModel>>();
				if (null != auxLista) return auxLista;
            }
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
            return new List<TrainModel>();
        }
		public async Task<IEnumerable<PlatformModel>> platformsList()
		{
			string request = composeCommand("platforms");
			HttpResponseMessage respuesta = await sendGetRequest(request);
			IEnumerable<PlatformModel>? auxLista = await
				respuesta.Content.ReadFromJsonAsync<IEnumerable<PlatformModel>>();
			if(null==auxLista) return new List<PlatformModel>();
			return auxLista;
		}
		public async Task<TrainModel?> train(string trainId)
		{
			string request = composeCommand(
				"traininfo",
				new requestParam("trainid", trainId));
			HttpResponseMessage respuesta = await sendGetRequest(request);
			return await respuesta.Content.ReadFromJsonAsync<TrainModel?>();
		}
		public async Task<Dictionary<Guid,UserModel>?>  usersTrainList()
		{
			string request = composeCommand("userstrains");
			HttpResponseMessage respuesta = await sendGetRequest(request);
			return await respuesta.Content.ReadFromJsonAsync<Dictionary<Guid,UserModel>>();
		}
		public async Task<IEnumerable<StatusChangeModel>> recentChangeList(DateTime timeStamp)
		{
			DateTime auxUtc = timeStamp.Kind == DateTimeKind.Utc
		? timeStamp
		: DateTime.SpecifyKind(timeStamp, DateTimeKind.Utc);
			string request = composeCommand(
				"rcchngs",
				new requestParam("timestamp", auxUtc.ToString("o")));
			HttpResponseMessage respuesta = await sendGetRequest(request);
			IEnumerable<StatusChangeModel>? auxLista = await respuesta.Content.ReadFromJsonAsync<IEnumerable<StatusChangeModel>>();
			if(null==auxLista) return new List<StatusChangeModel>() ;
			return auxLista;
		}

		public async Task<Dictionary<Guid,UserModel>?> usersChangesList(string trainId)
		{
			string request = composeCommand(
				"usersstchngs",
				new requestParam("trainid",trainId));
			HttpResponseMessage respuesta = await sendGetRequest(request);
			return await respuesta.Content.ReadFromJsonAsync<Dictionary<Guid ,UserModel>>();
		}

		public async Task<IEnumerable<StatusChangeModel>> trainChangesList(Guid trainId, DateTime oldest)
		{
			IEnumerable<StatusChangeModel>? salida = null;
			StatusChangeRequestModel request = new StatusChangeRequestModel(trainId, oldest);
			string jsonData = System.Text.Json.JsonSerializer.Serialize(request);
			HttpResponseMessage respuesta = await sendPostRequest("stchngs",jsonData);
			if (respuesta.IsSuccessStatusCode)
				salida =  await respuesta.Content.ReadFromJsonAsync<IEnumerable<StatusChangeModel>>();
			
				if (null == salida) return new List<StatusChangeModel>();
			return salida;
		}

		public async Task<bool> addNote(NoteModel note)
		{
			string jsonData = System.Text.Json.JsonSerializer.Serialize(note);
			HttpResponseMessage respuesta = await sendPostRequest("addnote", jsonData);
			if (respuesta.IsSuccessStatusCode)
				return await respuesta.Content.ReadFromJsonAsync<bool>();
			return false;
		}
		//Fuerza la modificación del andén actual del tren
		public async Task<bool> changePlatform(TrainModel train)
		{
			string jsonData = System.Text.Json.JsonSerializer.Serialize(train);
			HttpResponseMessage respuesta = await sendPostRequest("changeplatform", jsonData);
			if (respuesta.IsSuccessStatusCode)
				return await respuesta.Content.ReadFromJsonAsync<bool>();
			return false;	
		}

		//Actualiza el lavado del tren
		public async Task<bool> UpdateWash(TrainModel train)
		{
			string jsonData = System.Text.Json.JsonSerializer.Serialize(train);
			HttpResponseMessage respuesta = await sendPostRequest("updatewash", jsonData);
			if (respuesta.IsSuccessStatusCode)
				return await respuesta.Content.ReadFromJsonAsync<bool>();
			return false;
		}

		//Obtiene las últimas notas de un determinado tren.
		//Si el parámetro max es cero, devuelve todas las notas.
		//Si tiene otro valor, devuelve el número de notas requerido.
		public async Task<List<NoteModel>> retrieveNotes(Guid parentId,string type, int max)
		{
			Guid auxToken = await getCurrentToken();
			NoteChatRequestModel requestModel = new NoteChatRequestModel(auxToken,parentId,type,max);
			string jsonData = System.Text.Json.JsonSerializer.Serialize(requestModel);
			HttpResponseMessage response = await sendPostRequest("getnotes", jsonData);
			if(response.IsSuccessStatusCode)
			{
				List<NoteModel>? auxLista = await response.Content.ReadFromJsonAsync<List<NoteModel>>();
				if (null != auxLista)
					return auxLista;
			}
			return new List<NoteModel>(); //No se han encontrado notas.
		}

		public async Task<bool> commitTrainStatus(Guid trainId, Common.OperationType operation)
		{
			Guid auxToken = await getCurrentToken();
			TrainStatusCommitModel commit = new TrainStatusCommitModel(auxToken,trainId,operation);
			string jsonData = System.Text.Json.JsonSerializer.Serialize(commit);
			HttpResponseMessage response = await sendPostRequest("cmtstatus", jsonData);
			if(response.IsSuccessStatusCode)
				return await response.Content.ReadFromJsonAsync<bool>();
			return false;
		}

		#region Ordenes GMao
		public async Task<Dictionary<Guid,WorkCatalogModel>> OrdersDictionary()
		{
			IEnumerable<WorkCatalogModel>? entrada = await workCatalogList();
			Dictionary<Guid, WorkCatalogModel> salida = new Dictionary<Guid, WorkCatalogModel>();
			foreach (WorkCatalogModel ent in entrada)
			{
				if (!salida.ContainsKey(ent.Id))
					salida.Add(ent.Id, ent);
			}
			return salida;
		}

		public async Task<IEnumerable<WorkCatalogModel>> workCatalogList()
		{
			string request = composeCommand("workcatalog");
			HttpResponseMessage respuesta = await sendGetRequest(request);
			IEnumerable<WorkCatalogModel>? auxLista =
				await respuesta.Content.ReadFromJsonAsync<IEnumerable<WorkCatalogModel>>();
			if (null == auxLista) return new List<WorkCatalogModel>();
			return auxLista;
		}

		public async Task<WorkOrderModel?> workOrder(Guid id)
		{
			string request = composeCommand("workorders", new requestParam("id", id.ToString()));
			HttpResponseMessage respuesta = await sendGetRequest(request);
			return await respuesta.Content.ReadFromJsonAsync<WorkOrderModel?>();
		}

		public async Task<IEnumerable<WorkOrderModel>> workOrders(
			Guid? trainId = null,
			Guid? workType = null,
			bool? open = null,
			bool? atomic = null,
			DateTime? from = null,
			DateTime? to = null)
		{
			List<requestParam> args = new List<requestParam>();

			if (trainId.HasValue && trainId.Value != Guid.Empty)
				args.Add(new requestParam("trainId", trainId.Value.ToString()));

			if (workType.HasValue && workType.Value != Guid.Empty)
				args.Add(new requestParam("workType", workType.Value.ToString()));

			if (open.HasValue)
				args.Add(new requestParam("open", open.Value.ToString()));

			if (atomic.HasValue)
				args.Add(new requestParam("atomic", atomic.Value.ToString()));

			if (from.HasValue)
			{
				DateTime auxFrom = from.Value.Kind == DateTimeKind.Utc
					? from.Value
					: DateTime.SpecifyKind(from.Value, DateTimeKind.Utc);
				args.Add(new requestParam("from", Uri.EscapeDataString(auxFrom.ToString("o"))));
			}

			if (to.HasValue)
			{
				DateTime auxTo = to.Value.Kind == DateTimeKind.Utc
					? to.Value
					: DateTime.SpecifyKind(to.Value, DateTimeKind.Utc);
				args.Add(new requestParam("to", Uri.EscapeDataString(auxTo.ToString("o"))));
			}

			string request = composeCommand("workorders", args.ToArray());
			HttpResponseMessage respuesta = await sendGetRequest(request);
			IEnumerable<WorkOrderModel>? auxLista =
				await respuesta.Content.ReadFromJsonAsync<IEnumerable<WorkOrderModel>>();
			if (null == auxLista) return new List<WorkOrderModel>();
			return auxLista;
		}

		public Task<IEnumerable<WorkOrderModel>> workOrdersByTrain(Guid trainId)
		{
			return workOrders(trainId: trainId);
		}

		public Task<IEnumerable<WorkOrderModel>> workOrdersByTrainAndType(Guid trainId, Guid workType)
		{
			return workOrders(trainId: trainId, workType: workType);
		}

		public Task<IEnumerable<WorkOrderModel>> openWorkOrders(
			Guid? trainId = null,
			Guid? workType = null,
			bool? atomic = null,
			DateTime? from = null,
			DateTime? to = null)
		{
			return workOrders(trainId: trainId, workType: workType, open: true, atomic: atomic, from: from, to: to);
		}

		public async Task<bool> HasPendantWorks(Guid trainId, bool? atomic = null)
		{
			IEnumerable<WorkOrderModel> pendants = await workOrders(trainId: trainId, open:true, atomic:atomic);
			return pendants.Any();
		}
		public async Task<bool> HasPendantWashing(Guid trainId)
		{
			IEnumerable<WorkOrderModel> pendants = await workOrders(trainId: trainId, open:true, atomic:true);
			return pendants.Where(x => x.WorkType == Common.WorkOrderTypeManualWash ||
			x.WorkType == Common.WorkOrderTypePlatformWash ||
			x.WorkType == Common.WorkOrderTypeTunnelWash).Any();
		}

		public async Task<bool> TerminateWashing(Guid trainId)
		{
			IEnumerable<WorkOrderModel> pendants = await workOrders(trainId: trainId, open: true, atomic: true);
			bool salida = false;
			foreach(WorkOrderModel order in pendants)
			{
				if(Sapphire2025Models.Utils.OrderTypeIsWash(order.WorkType))
				{
					if (await closeWorkOrder(order.Id))
						await TerminateWashing(trainId);
					salida = true;
				}
			}
			return salida;
		}

		public Task<IEnumerable<WorkOrderModel>> closedWorkOrders(
			Guid? trainId = null,
			Guid? workType = null,
			DateTime? from = null,
			DateTime? to = null)
		{
			return workOrders(trainId: trainId, workType: workType, open: false, from: from, to: to);
		}

		public async Task<WorkOrderModel?> createWorkOrder(
			Guid workType,
			Guid? destinationObjectId = null,
			Guid? trainId = null)
		{
			Guid auxToken = await getCurrentToken();
			WorkOrderCreateRequestModel requestModel = new WorkOrderCreateRequestModel
			{
				SessionToken = auxToken,
				WorkType = workType,
				DestinationObjectId = destinationObjectId,
				TrainId = trainId
			};

			string jsonData = System.Text.Json.JsonSerializer.Serialize(requestModel);
			HttpResponseMessage respuesta = await sendPostRequest("workorders", jsonData);
			return await respuesta.Content.ReadFromJsonAsync<WorkOrderModel?>();
		}

		public async Task<bool> closeWorkOrder(Guid workOrderId)
		{
			Guid auxToken = await getCurrentToken();
			WorkOrderActionRequestModel requestModel = new WorkOrderActionRequestModel
			{
				SessionToken = auxToken,
				WorkOrderId = workOrderId
			};

			string jsonData = System.Text.Json.JsonSerializer.Serialize(requestModel);
			HttpResponseMessage respuesta = await sendPostRequest("workorders/close", jsonData);
			return (null != respuesta.Content);
		}

		public async Task<bool> verifyWorkOrder(Guid workOrderId)
		{
			Guid auxToken = await getCurrentToken();
			WorkOrderActionRequestModel requestModel = new WorkOrderActionRequestModel
			{
				SessionToken = auxToken,
				WorkOrderId = workOrderId
			};

			string jsonData = System.Text.Json.JsonSerializer.Serialize(requestModel);
			HttpResponseMessage respuesta = await sendPostRequest("workorders/verify", jsonData);
			return (null != respuesta.Content);
		}

		#endregion Ordenes GMao
	}
}

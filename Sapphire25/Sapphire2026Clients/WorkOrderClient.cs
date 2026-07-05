using Sapphire2025Models;
using Sapphire2025Models.Aeneas;
using Sapphire2025Models.GMao;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using static Sapphire2025.Storage.HttpClientBase;

namespace Sapphire2025.Storage
{
/// <summary>
/// Parte de AeneasClient que contiene todo lo relacionado con los trabajos y campañas (lavado)
/// </summary>
	public partial class AeneasClient
	{
		public async Task<Dictionary<Guid, WorkCatalogModel>> OrdersDictionary()
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
			bool? closed = null,
			bool? verified = null,
			bool? atomic = null,
			DateTime? from = null,
			DateTime? to = null,
			bool? rejected = null)
		{
			List<requestParam> args = new List<requestParam>();

			if (trainId.HasValue && trainId.Value != Guid.Empty)
				args.Add(new requestParam("trainId", trainId.Value.ToString()));

			if (workType.HasValue && workType.Value != Guid.Empty)
				args.Add(new requestParam("workType", workType.Value.ToString()));

			if (open.HasValue)
				args.Add(new requestParam("open", open.Value.ToString()));

			if (closed.HasValue)
				args.Add(new requestParam("closed", closed.Value.ToString()));

			if (verified.HasValue)
				args.Add(new requestParam("verified", verified.Value.ToString()));

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

			if (rejected.HasValue)
				args.Add(new requestParam("rejected", rejected.Value.ToString()));

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
			DateTime? to = null
			)
		{
			return workOrders(trainId: trainId, workType: workType, open: true, rejected: false, atomic: atomic, from: from, to: to);
		}

		public async Task<IEnumerable<WorkOrderModel>> PendantWorks(Guid trainId, bool? atomic = null)
		{
			return await workOrders(trainId: trainId, closed: false, rejected: false, atomic: atomic);		
		}

		public Task<IEnumerable<WorkOrderModel>> closedWorkOrders(
			Guid? trainId = null,
			Guid? workType = null,
			DateTime? from = null,
			DateTime? to = null)
		{
			return workOrders(trainId: trainId, workType: workType, closed: true, rejected: false, from: from, to: to);
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
			HttpResponseMessage respuesta = await sendPostRequest("workorders/request", jsonData);
			return await respuesta.Content.ReadFromJsonAsync<WorkOrderModel?>();
		}

		private async Task<bool> paramWorkOrder(Guid workOrderId, string orderId)
		{
			Guid auxToken = await getCurrentToken();
			WorkOrderActionRequestModel requestModel = new WorkOrderActionRequestModel
			{
				SessionToken = auxToken,
				WorkOrderId = workOrderId
			};
			string jsonData = System.Text.Json.JsonSerializer.Serialize(requestModel);
			HttpResponseMessage respuesta = await sendPostRequest($"workorders/{orderId}", jsonData);
			return (null != respuesta.Content);
		}

		public async Task<bool> openWorkOrder(Guid workOrderId)
		{
			return await paramWorkOrder(workOrderId, "open");
		}

		public async Task<bool> rejectWorkOrder(Guid workOrderId)
		{
			return await paramWorkOrder(workOrderId, "reject");
		}

		public async Task<bool> closeWorkOrder(Guid workOrderId)
		{
			return await paramWorkOrder(workOrderId, "close");
		}

		public async Task<bool> verifyWorkOrder(Guid workOrderId)
		{
			return await paramWorkOrder(workOrderId, "verify");
		}
	}
}

using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Sapphire2025Models.Aeneas;
using Sapphire2025Models.GMao;
using Sapphire2026.Data;
using Sapphire2026.Data.Models;
using Sapphire2026Data.Models.GMAO;

namespace Sapphire2025Server.Controllers
{
	public partial class SapphireAeneasController
	{
		[HttpGet("workcatalog")]
		public async Task<List<WorkCatalogModel>> WorkCatalogRequest()
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				return await almacen.WorksCatalog
					.AsNoTracking()
					.Select(x => new WorkCatalogModel
					{
						Id = x.Id,
						Name = x.Name ?? string.Empty,
						Atomic = x.Atomic,
						Comment = x.Comment
					})
					.ToListAsync();
			}
		}

		[HttpGet("workorders/{id:guid}")]
		public async Task<WorkOrderModel?> WorkOrderById(Guid id)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				WorkOrder? aux = await almacen.WorkOrders
					.AsNoTracking()
					.FirstOrDefaultAsync(x => x.Id == id);

				return aux is null ? null : ToWorkOrderModel(aux);
			}
		}

		[HttpGet("workorders")]
		public async Task<List<WorkOrderModel>> WorkOrdersQuery(
			[FromQuery] Guid? trainId = null,
			[FromQuery] Guid? workType = null,
			[FromQuery] bool? open = null,
			[FromQuery] bool? closed = null,
			[FromQuery] bool? verified = null,
			[FromQuery] bool? atomic = null,
			[FromQuery] DateTime? from = null,
			[FromQuery] DateTime? to = null,
			[FromQuery] bool? rejected = null)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				IQueryable<WorkOrder> query = ApplyWorkOrderFilters(
					almacen.WorkOrders.AsNoTracking(),
					trainId,
					workType,
					open,
					closed,
					verified,
					atomic,
					from,
					to,
					rejected);

				return await query
					.OrderByDescending(x => x.OpenTime)
					.Select(x => ToWorkOrderModel(x))
					.ToListAsync();
			}
		}

		[HttpPost("workorders/request")]
		public async Task<WorkOrderModel?> RequestWorkOrder([FromBody] WorkOrderCreateRequestModel request)
		{
			if (request is null) return null;

			User? user = await retrieveSessionUser(request.SessionToken);
			if (user is null) return null;

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				WorkCatalog? auxOperation = await almacen.WorksCatalog.Where(x => x.Id == request.WorkType).FirstOrDefaultAsync();

				if (null != auxOperation)
				{
					WorkOrder nuevo = new WorkOrder
					{
						Id = Guid.NewGuid(),
						WorkType = request.WorkType,
						Atomic = auxOperation.Atomic, //La atomicidad de una operación podría cambiar en el tiempo
						DestinationObjectId = request.DestinationObjectId,
						TrainId = request.TrainId,
						RequestUserId = user.guid,
						RequestTime = DateTime.UtcNow,
						Rejected = false
					};
					almacen.WorkOrders.Add(nuevo);

					if (await almacen.SaveChangesAsync() > 0)
						return ToWorkOrderModel(nuevo);
				}
				return null;
			}
		}

		[HttpPost("workorders/open")]
		public async Task<WorkOrderModel?> OpenWorkOrder([FromBody] WorkOrderActionRequestModel request)
		{
			if (null == request) return null;

			User? user = await retrieveSessionUser(request.SessionToken);
			if (null == user) return null;

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				WorkOrder? order = await almacen.WorkOrders
				.FirstOrDefaultAsync(x => x.Id == request.WorkOrderId);

				if (null == order) return null;
				if (null != order.OpenTime) return ToWorkOrderModel(order);

				order.OpenUserId = user.guid;
				order.OpenTime = DateTime.UtcNow;

				await almacen.SaveChangesAsync();
				return ToWorkOrderModel(order);
			}
		}

		[HttpPost("workorders/reject")]
		public async Task<WorkOrderModel?> RejectWorkOrder([FromBody] WorkOrderActionRequestModel request)
		{
			if (null == request) return null;

			User? user = await retrieveSessionUser(request.SessionToken);
			if (null == user) return null;

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				WorkOrder? order = await almacen.WorkOrders
				.FirstOrDefaultAsync(x => x.Id == request.WorkOrderId);

				if (null == order) return null;
				if (null != order.OpenTime) return ToWorkOrderModel(order);

				order.Rejected = true;
				order.VerifyUserId = user.guid;
				order.VerifyTime = DateTime.UtcNow;

				await almacen.SaveChangesAsync();
				return ToWorkOrderModel(order);
			}
		}

		[HttpPost("workorders/close")]
		public async Task<WorkOrderModel?> CloseWorkOrder([FromBody] WorkOrderActionRequestModel request)
		{
			if (request is null) return null;

			User? user = await retrieveSessionUser(request.SessionToken);
			if (user is null) return null;

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				WorkOrder? order = await almacen.WorkOrders
					.FirstOrDefaultAsync(x => x.Id == request.WorkOrderId);

				if (order is null) return null;
				if (order.CloseTime is not null) return ToWorkOrderModel(order);

				order.CloseUserId = user.guid;
				order.CloseTime = DateTime.UtcNow;

				await almacen.SaveChangesAsync();
				return ToWorkOrderModel(order);
			}
		}
		[HttpPost("workorders/verify")]
		public async Task<WorkOrderModel?> VerifyWorkOrder([FromBody] WorkOrderActionRequestModel request)
		{
			if (request is null) return null;

			User? user = await retrieveSessionUser(request.SessionToken);
			if (user is null) return null;

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				WorkOrder? order = await almacen.WorkOrders
					.FirstOrDefaultAsync(x => x.Id == request.WorkOrderId);

				if (order is null) return null;
				if (order.CloseTime is null) return null;

				order.VerifyUserId = user.guid;
				order.VerifyTime = DateTime.UtcNow;

				await almacen.SaveChangesAsync();
				return ToWorkOrderModel(order);
			}
		}

		private static IQueryable<WorkOrder> ApplyWorkOrderFilters(
			IQueryable<WorkOrder> query,
			Guid? trainId,
			Guid? workType,
			bool? open,
			bool? closed,
			bool? verified,
			bool? atomic,
			DateTime? from,
			DateTime? to,
			bool? rejected)
		{
			if (trainId.HasValue && trainId.Value != Guid.Empty)
				query = query.Where(x => x.TrainId == trainId.Value);

			if (workType.HasValue && workType.Value != Guid.Empty)
				query = query.Where(x => x.WorkType == workType.Value);

			if (open.HasValue)
			{
				if (open.Value)
					query = query.Where(x => null != x.OpenTime);
				else
					query = query.Where(x => null == x.OpenTime);
			}

			if (closed.HasValue)
			{
				if (closed.Value)
					query = query.Where(x => null != x.CloseTime);
				else
					query = query.Where(x => null == x.CloseTime);
			}

			if (verified.HasValue)
			{
				if (verified.Value)
					query = query.Where(x => null != x.VerifyTime);
				else
					query = query.Where(x => null == x.VerifyTime);
			}

			if (atomic.HasValue)
				query = query.Where(x => x.Atomic == atomic.Value);

			if (from.HasValue)
				query = query.Where(x => x.OpenTime >= from.Value);

			if (to.HasValue)
				query = query.Where(x => x.OpenTime <= to.Value);

			if (rejected.HasValue)
				query = query.Where(x => x.Rejected == rejected.Value);

			return query;
		}

		private static WorkCatalogModel ToWorkCatalogModel(WorkCatalog rhs)
		{
			return new WorkCatalogModel
			{
				Id = rhs.Id,
				Atomic = rhs.Atomic,
				Name = rhs.Name ?? string.Empty,
				Comment = rhs.Comment
			};
		}

		private static WorkOrderModel ToWorkOrderModel(WorkOrder rhs)
		{
			return new WorkOrderModel
			{
				Id = rhs.Id,
				WorkType = rhs.WorkType,
				Atomic = rhs.Atomic,
				Rejected = rhs.Rejected,
				DestinationObjectId = rhs.DestinationObjectId,
				TrainId = rhs.TrainId,
				RequestUserId = rhs.RequestUserId,
				OpenUserId = rhs.OpenUserId,
				CloseUserId = rhs.CloseUserId,
				VerifyUserId = rhs.VerifyUserId,
				RequestTime = rhs.RequestTime,
				OpenTime = rhs.OpenTime,
				CloseTime = rhs.CloseTime,
				VerifyTime = rhs.VerifyTime
			};
		}


	}
}

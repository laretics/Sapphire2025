using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.DBStorage;
using TimeNet2026Data.DBStorage;
using Microsoft.EntityFrameworkCore;
using System.Xml.Schema;

namespace TimeNet2026Data.Serialization
{
	//Este elemento se encarga de recuperar y guardar estructuras de datos TimeNet en la base de datos.
	public class DBSerializer
	{
		internal ITimeNetContextStorage mvarContext;
		public DBSerializer(ITimeNetContextStorage context)
		{
			mvarContext = context;
		}
		public async Task ClearDatabase()
		{
			List<DBHeader> auxHeaders = await mvarContext.Headers.ToListAsync();
			List<DBRefPunctual> auxRefPunctuals = await mvarContext.RefPunctuals.ToListAsync();
			List<DBStation> auxStations = await mvarContext.Stations.ToListAsync();
			List<DBAxis> auxAxis = await mvarContext.Axis.ToListAsync();
			List<DBTopoStorage> auxStorages = await mvarContext.TopoStorages.ToListAsync();
			List<DBRauta> auxRautatie = await mvarContext.Rautatie.ToListAsync();
			List<DBPlan> auxPlans = await mvarContext.Plans.ToListAsync();
			List<DBCirculationBlock> auxBlocks = await mvarContext.CirculationBlocks.ToListAsync();
			List<DBCirculation> auxCirculations = await mvarContext.Circulations.ToListAsync();
			List<DBSchedule> auxSchedules = await mvarContext.Schedules.ToListAsync();
			List<DBScheduleUnit> auxScheduleUnits = await mvarContext.ScheduleUnits.ToListAsync();
			mvarContext.Headers.RemoveRange(auxHeaders);
			mvarContext.RefPunctuals.RemoveRange(auxRefPunctuals);
			mvarContext.Stations.RemoveRange(auxStations);
			mvarContext.Axis.RemoveRange(auxAxis);
			mvarContext.TopoStorages.RemoveRange(auxStorages);
			mvarContext.Rautatie.RemoveRange(auxRautatie);
			mvarContext.Plans.RemoveRange(auxPlans);
			mvarContext.CirculationBlocks.RemoveRange(auxBlocks);
			mvarContext.Circulations.RemoveRange(auxCirculations);
			mvarContext.Schedules.RemoveRange(auxSchedules);
			mvarContext.ScheduleUnits.RemoveRange(auxScheduleUnits);
			await mvarContext.SaveChangesAsync();
		}

		#region Headers
		public void Add(DBHeader header) { mvarContext.Headers.Add(header); }
		public void RemoveHeader (Guid id)
		{
			List<DBHeader> lista = mvarContext.Headers.Where(x => x.Id == id).ToList();
			if (lista.Count > 0)
				mvarContext.Headers.RemoveRange(lista);
		}
		public async Task<DBHeader?> GetHeader(Guid id)
		{
			return await mvarContext.Headers.Where(x => x.Id == id).FirstOrDefaultAsync();
		}
		#endregion Headers
		#region Rautatie
		public void Add(DBRauta rauta) {mvarContext.Rautatie.Add(rauta);}
		public async Task RemoveRauta(Guid id)
		{
			List<DBRauta> auxLista = await mvarContext.Rautatie.Where(x => x.HeaderId==id).ToListAsync();
			RemoveHeader(id);
			foreach (DBRauta auxRauta in  auxLista)
				await RemoveRauta(auxRauta);			
		}
		internal async Task RemoveRauta(DBRauta auxRauta)
		{
			//Existe... ahora hay que comprobar si existen sus elementos.
			List<DBPlan> auxPlanes = await mvarContext.Plans.Where(x => x.RautaId == auxRauta.Id).ToListAsync();
			foreach (DBPlan auxPlan in auxPlanes)
			{
				//Se eliminan circulaciones y turnos de los planes afectados..
				List<DBCirculationBlock> auxBlocks = await mvarContext.CirculationBlocks.Where(x => x.PlanId == auxPlan.Id).ToListAsync();
				foreach (DBCirculationBlock auxBlock in auxBlocks)
				{
					List<DBCirculation> auxCirculationsInBlock = await mvarContext.Circulations.Where(x => x.BlockId == auxBlock.Id).ToListAsync();
					mvarContext.Circulations.RemoveRange(auxCirculationsInBlock);
				}
				mvarContext.CirculationBlocks.RemoveRange(auxBlocks);

				List<DBSchedule> auxSchedules = await mvarContext.Schedules.Where(x => x.PlanId == auxPlan.Id).ToListAsync();
				foreach (DBSchedule auxSchedule in auxSchedules)
				{
					List<DBScheduleUnit> auxUnits = await mvarContext.ScheduleUnits.Where(x => x.ScheduleId == auxSchedule.Id).ToListAsync();
					mvarContext.ScheduleUnits.RemoveRange(auxUnits);
				}
				mvarContext.Schedules.RemoveRange(auxSchedules);
			}
			mvarContext.Plans.RemoveRange(auxPlanes);			
			mvarContext.Rautatie.Remove(auxRauta);
			await mvarContext.SaveChangesAsync();//Transacción de una sola vez.
		}
		public async Task RemoveRauta(int topoStorageId)
		{
			IEnumerable<DBRauta> lista = await mvarContext.Rautatie.Where(x => x.TopoStorageId == topoStorageId).ToListAsync();
			foreach (DBRauta b in lista)
				await RemoveRauta(b);
		}
		
		public async Task<List<DBRauta>>GetRautatie(int topoStorageId)
		{
			return await mvarContext.Rautatie.Where(x=>x.TopoStorageId==topoStorageId).ToListAsync();
		}
		#region Plan
		public void Add(DBPlan plan) { mvarContext.Plans.Add(plan); }
		public async Task<List<DBPlan>> GetPlans(int rautatieId)
		{
			return await mvarContext.Plans.Where(x=>x.RautaId==rautatieId).ToListAsync();
		}
		#region Circulation
		public void Add(DBCirculationBlock block) { mvarContext.CirculationBlocks.Add(block); }
		public void Add(DBCirculation circulation) { mvarContext.Circulations.Add(circulation); }
		public async Task<List<DBCirculationBlock>> GetCirculationBlocks(int planId)
		{
			return await mvarContext.CirculationBlocks.Where(x=>x.PlanId== planId).ToListAsync();
		}
		public async Task<List<DBCirculation>> GetCirculations(int blockId)
		{
			return await mvarContext.Circulations.Where(x=>x.BlockId== blockId).ToListAsync();
		}
		public async Task<DBCirculation?> GetCirculation(int circulationId)
		{
			return await mvarContext.Circulations.Where(x => x.Id == circulationId).FirstOrDefaultAsync();
		}

		#endregion Circulation
		#region Schedule
		public void Add(DBSchedule schedule) { mvarContext.Schedules.Add(schedule); }
		public void Add(DBScheduleUnit unit) { mvarContext.ScheduleUnits.Add(unit); }
		public async Task<List<DBSchedule>> GetSchedules(int planId)
		{
			return await mvarContext.Schedules.Where(x=>x.PlanId == planId).ToListAsync();
		}
		public async Task<List<DBScheduleUnit>> GetScheduleUnits(int scheduleId)
		{
			return await mvarContext.ScheduleUnits.Where(x=>x.ScheduleId== scheduleId).ToListAsync();
		}

		#endregion Schedule
		#endregion Plan

		#endregion Rautatie

		#region TopoStorage
		public void Add(DBTopoStorage topo) { mvarContext.TopoStorages.Add(topo); }
		public async Task<List<DBTopoStorage>> GetTopoStorages() { return await mvarContext.TopoStorages.ToListAsync(); }
		public async Task<DBTopoStorage?> GetTopoStorage(Guid headerId)
		{
			return await mvarContext.TopoStorages.Where(xx => xx.HeaderId== headerId).FirstOrDefaultAsync();
		}
		public async Task RemoveTopoStorage(Guid id)
		{
			DBTopoStorage? auxTopoStorage = await GetTopoStorage(id);
			if (null != auxTopoStorage)
			{
				//Primero elimino todos los rautatie relacionados.
				await RemoveRauta(auxTopoStorage.Id);
				//Ahora ya puedo eliminar toda la jerarquía.
				RemoveHeader(id);

				IEnumerable<DBAxis> auxAxises = await GetAxises(auxTopoStorage.Id);
				foreach (DBAxis auxAxis in auxAxises)
				{
					IEnumerable<DBStation> auxStations = await mvarContext.Stations.Where(x => x.AxisId == auxAxis.Id).ToListAsync();
					mvarContext.Stations.RemoveRange(auxStations);
					//Puntos singulares y estaciones del eje.
					IEnumerable<DBRefPunctual> auxPunctuals = await mvarContext.RefPunctuals.Where(x => x.AxisId == auxAxis.Id).ToListAsync();
					mvarContext.RefPunctuals.RemoveRange(auxPunctuals);

					// ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ###
					// ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ###
					// ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ###
					//TODO:  Limitaciones de velocidad
					// ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ###
					// ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ###
					// ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ###

					// ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ###
					// ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ###
					// ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ###
					//TODO:  Señales
					// ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ###
					// ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ###
					// ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ### ###

				}

				IEnumerable<DBAsimilation> auxAsimilations = await mvarContext.Asimilations.Where(x => x.TopoStorageId == auxTopoStorage.Id).ToListAsync();
				foreach (DBAsimilation auxAsimilation in auxAsimilations)
				{
					IEnumerable<DBAsimilationStep> auxAsimilationSteps = await mvarContext.AsimilationSteps.Where(x => x.AsimilationId == auxAsimilation.Id).ToListAsync();
					mvarContext.AsimilationSteps.RemoveRange(auxAsimilationSteps);
				}
				mvarContext.Asimilations.RemoveRange(auxAsimilations);
				mvarContext.TopoStorages.Remove(auxTopoStorage);
				await mvarContext.SaveChangesAsync();
			}
		}
		#region Axis
		public void Add(DBAxis nuevo) { mvarContext.Axis.Add(nuevo); }
		public async Task<IEnumerable<DBAxis>> GetAxises(int topoStorageId)
		{
			return await mvarContext.Axis.Where(x => x.StorageId== topoStorageId).ToListAsync();
		}
		#endregion Axis
		#region Station
		public void Add(DBStation station) { mvarContext.Stations.Add(station); }
		public async Task<IEnumerable<DBStation>> GetStations(int axisId)
		{
			return await mvarContext.Stations.Where(x => x.AxisId== axisId).ToListAsync();
		}
		#endregion Station
		#region Punctual
		public void Add(DBRefPunctual punctual) { mvarContext.RefPunctuals.Add(punctual); }
		public async Task<IEnumerable<DBRefPunctual>> GetRefPunctuals(int axisId)
		{
			return await mvarContext.RefPunctuals.Where(x => x.AxisId == axisId).ToListAsync();
		}

		#endregion Punctual
		#region Asimilation
		public void Add(DBAsimilation asimila) { mvarContext.Asimilations.Add(asimila); }
		public void Add(DBAsimilationStep step) { mvarContext.AsimilationSteps.Add(step); }
		public async Task<IEnumerable<DBAsimilation>> GetAsimilations(int topoStorageId)
		{
			return await mvarContext.Asimilations.Where(x => x.TopoStorageId== topoStorageId).ToListAsync();
		}
		public async Task<IEnumerable<DBAsimilationStep>> GetAsimilationSteps(int asimilationId)
		{
			return await mvarContext.AsimilationSteps.Where(x=>x.AsimilationId== asimilationId).ToListAsync();
		}

		#endregion Asimilation

		#endregion TopoStorage

		public Task<int> SaveChangesAsync() { return mvarContext.SaveChangesAsync();}
	}
}

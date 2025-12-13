using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite.Query.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Storage;
using TimeNet2026.Timed;
using TimeNet2026.Topo;

namespace TimeNet2026.DBStorage
{
	public class OnyxDatabase:DbContext
	{
		internal DbSet<DBHeader> Headers { get; set; }
		internal DbSet<DBRefPunctual> RefPunctuals { get; set; }
		internal DbSet<DBStation> Stations { get; set; }
		internal DbSet<DBAxis> Axis { get; set; }
		internal DbSet<DBAsimilationStep> AsimilationSteps { get; set; }
		internal DbSet<DBAsimilation> Asimilations { get; set; }
		internal DbSet<DBTopoStorage> TopoStorages { get; set; }

		public OnyxDatabase(DbContextOptions<OnyxDatabase> opciones) : base(opciones) { }
		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<DBRefPunctual>()
				.HasKey(e => new { e.AxisId, e.Pk }); // Clave primaria compuesta
		}
		private DBTopoStorage? mvarCurrentTopoStorage { get; set; }
		internal TopoStorage? CurrentTopoStorage {get; set; } //Para operaciones con ejes.
		private Dictionary<int,Axis> mcolAxisCache = new Dictionary<int, Axis>(); //Estructura interna de ejes para cargar las asimilaciones.
		private Dictionary<int, Station> mcolStationCache = new Dictionary<int, Station>(); //Estructura interna de estaciones para cargar las asimilaciones.

		internal async Task TotalRemove()
		{
			List<DBHeader> auxHeaders = Headers.ToList();
			List<DBRefPunctual> auxRefPunctuals = RefPunctuals.ToList();
			List<DBStation> auxStations = Stations.ToList();
			List<DBAxis> auxAxis = Axis.ToList();
			List<DBTopoStorage> auxStorages = TopoStorages.ToList();
			Headers.RemoveRange(auxHeaders);
			RefPunctuals.RemoveRange(auxRefPunctuals);
			Stations.RemoveRange(auxStations);
			Axis.RemoveRange(auxAxis);
			TopoStorages.RemoveRange(auxStorages);
			await SaveChangesAsync();
		}

		#region Header
		internal async Task Insert(Header rhs)
		{
			await Remove(rhs); //Elimino cualquier etiqueta anterior.
			DBHeader auxHeader = new DBHeader();
			auxHeader.Id = rhs.Id;
			auxHeader.Name = rhs.Name;
			auxHeader.Comment = rhs.Comment;
			auxHeader.License = rhs.License;
			auxHeader.Author = rhs.Author;
			auxHeader.FirstDate = rhs.FirstDate;
			auxHeader.LastDate = rhs.LastDate;
			auxHeader.Version = rhs.Version;
			auxHeader.Bitmap = rhs.Bitmap;
			Headers.Add(auxHeader);
		}
		internal async Task Remove(Header rhs, bool update = true)
		{
			List<DBHeader> lista = Headers.Where(x => x.Id == rhs.Id).ToList();			
			if(lista.Count>0)
			{
				Headers.RemoveRange(lista);
				if(update) await SaveChangesAsync();
			}
		}
		internal async Task<Header?> GetHeader(Guid id)
		{
			DBHeader? xx = await Headers.Where(x => x.Id == id).FirstOrDefaultAsync();
			if(null!=xx)
			{
				Header salida = new Header();
				salida.Id = xx.Id;
				salida.Name = xx.Name;
				salida.Comment = xx.Comment;
				salida.License = xx.License;
				salida.Author = xx.Author;
				salida.FirstDate = xx.FirstDate;
				salida.LastDate = xx.LastDate;
				salida.Version = xx.Version;
				salida.Bitmap = xx.Bitmap;
				return salida;
			}
			return null;
		}
		#endregion Header

		#region TopoStorage
		internal async Task<List<TopoStorage>> GetTopoStorages()
		{
			List<TopoStorage> salida = new List<TopoStorage>();
			List<DBTopoStorage> entrada = await TopoStorages.ToListAsync();
			foreach(DBTopoStorage auxEntrada in entrada)
			{
				TopoStorage? nuevo = await GetTopoStorage(auxEntrada.HeaderId);
				if (null != nuevo)
					salida.Add(nuevo);
			}
			return salida;
		}
		internal async Task<TopoStorage?> GetTopoStorage(Guid id)
		{
			DBTopoStorage? tablaIndice = await TopoStorages.Where(x => x.HeaderId == id).FirstOrDefaultAsync();
			if (null == tablaIndice) return null;
			TopoStorage salida = new TopoStorage();
			mcolAxisCache = new Dictionary<int, Axis>(); //Necesito este caché para cargar los ejes.
			mcolStationCache = new Dictionary<int, Station>(); //Necesito este caché para cargar las estaciones.
			Header? auxHeader = await GetHeader(tablaIndice.HeaderId);
			System.Diagnostics.Debug.Assert(null != auxHeader, "Inconsistencia en la base de datos respecto a un Header en un TopoStorage");
			salida.Header = auxHeader;
			CurrentTopoStorage = salida; //Necesitamos este ajuste para recuperar los elementos de esta colección de topologías.
			mvarCurrentTopoStorage = await TopoStorages.Where(x => x.HeaderId == id).FirstOrDefaultAsync();
			
			//Recuperamos todos los ejes.
			List<Axis> auxEjes = await getAxis();
			foreach(Axis eje in auxEjes)
				salida.mcolAxis.Add(eje.id, eje);

			return salida;
		}

		internal async Task Insert(TopoStorage rhs)
		{
			await RemoveTopoStorage(rhs.Header.Id); //Elimino versiones previas.

			await Insert(rhs.Header); //Metemos la cabecera.
			DBTopoStorage nuevo = new DBTopoStorage();
			nuevo.HeaderId = rhs.Header.Id;			
			TopoStorages.Add(nuevo);
			await SaveChangesAsync(); //Lo tengo que hacer aquí para que currentStorage tenga numero.
			mvarCurrentTopoStorage = nuevo;
			CurrentTopoStorage = rhs;

			//Ahora metemos todos los ejes
			foreach (Axis eje in rhs.ColAxis)
				await Insert(eje);

		}
		internal async Task RemoveTopoStorage(Guid rhs, bool update = true)
		{
			DBTopoStorage? auxCandidato = await TopoStorages.Where(x => x.HeaderId == rhs).FirstOrDefaultAsync();
			if (null == auxCandidato) return;
			await GetTopoStorage(rhs);
			List<DBAxis> ejes = Axis.Where(x => x.StorageId == auxCandidato.Id).ToList();
			foreach(DBAxis eje in ejes)
				await RemoveAxis(eje.AxisId, false);
			//TODO: Eliminar las asimilaciones
			Axis.RemoveRange(ejes);
			TopoStorages.Remove(auxCandidato);
			if(update) await SaveChangesAsync();
			CurrentTopoStorage = null;
			mvarCurrentTopoStorage = null;
		}
		#endregion TopoStorage

		#region AsimilationStep
		internal async Task Insert (AsimilationStep rhs, int internalAsimilationId)
		{
			DBAxis? auxEje = await Axis.Where(x => x.AxisId == rhs.axis.id).FirstOrDefaultAsync();
			if(null!=auxEje)
			{
				DBStation? auxEstacion = await Stations.Where(x => x.StationId == rhs.destination.id).FirstOrDefaultAsync();
				if(null!=auxEstacion)
				{
					DBAsimilationStep nuevo = new DBAsimilationStep();
					nuevo.AsimilationId = internalAsimilationId;
					nuevo.DestinationStationId = auxEstacion.Id;
					nuevo.AxisId = auxEje.Id;
					nuevo.stopTime = rhs.stopTime;
					nuevo.tripTime = rhs.tripTime;
					AsimilationSteps.Add(nuevo);
					await SaveChangesAsync();
				}
			}
		}
		internal async Task <List<AsimilationStep>> GetAsimilationSteps(int internalAsimilationId)
		{
			List<AsimilationStep> salida = new List<AsimilationStep>();
			List<DBAsimilationStep> entrada = await AsimilationSteps.Where(x => x.AsimilationId == internalAsimilationId).ToListAsync();
			foreach(DBAsimilationStep pasoEntrada in entrada)
			{
				Station? auxDestino = GetStation(pasoEntrada.DestinationStationId);
				Axis? auxEje = await getAxis(pasoEntrada.AxisId);
				if(null!=auxDestino && null!=auxEje)
				{
					AsimilationStep pasoSalida = new AsimilationStep(auxDestino,auxEje,pasoEntrada.tripTime,pasoEntrada.stopTime);
					salida.Add(pasoSalida);
				}			
			}
			return salida;
		}
		


		#endregion AsimilationStep

		#region Asimilation
		internal async Task Insert(Asimilation rhs)
		{
			System.Diagnostics.Debug.Assert(null != mvarCurrentTopoStorage);
			System.Diagnostics.Debug.Assert(null != rhs.origin);
			await Remove(rhs, true);
			DBAsimilation nueva = new DBAsimilation();
			nueva.TopoStorageId = mvarCurrentTopoStorage.Id;
			nueva.AsimilationId = rhs.id;
			nueva.Name = rhs.name;
			nueva.Comment = rhs.comment;
			nueva.Color0 = rhs.color[0];
			nueva.Color1 = rhs.color[1];
			nueva.MaxSpeed = rhs.maxSpeed;			
			DBStation? candidate = await GetDBStation(rhs.origin.id, rhs.origin.axis.id);
			System.Diagnostics.Debug.Assert(null != candidate);
			nueva.OriginStationId = candidate.Id;
			await SaveChangesAsync(); //Doy valor al id de la asimilación.
			foreach (AsimilationStep paso in rhs.mcolSteps)
				await Insert(paso,nueva.Id);
		}
		internal async Task Remove(Asimilation rhs, bool update = true)
		{
			System.Diagnostics.Debug.Assert(null != mvarCurrentTopoStorage);
			int topoStorage = mvarCurrentTopoStorage.Id;
			DBAsimilation? candidato = await Asimilations.Where(x => x.TopoStorageId == topoStorage && x.AsimilationId == rhs.id).FirstOrDefaultAsync();
			if(null!=candidato)
			{
				//Primero eliminamos todas las operaciones.
				List<DBAsimilationStep> auxPasos = await AsimilationSteps.Where(x => x.AsimilationId == candidato.Id).ToListAsync();
				AsimilationSteps.RemoveRange(auxPasos);
				Asimilations.Remove(candidato);
				if (update) await SaveChangesAsync();
			}
		}
		internal async Task<Asimilation?> GetAsimilation(int id)
		{
			DBAsimilation? candidato = await Asimilations.Where(x => x.Id == id).FirstOrDefaultAsync();
			if(null!=candidato)
			{
				Asimilation salida = new Asimilation();
				salida.id = candidato.AsimilationId;
				salida.name = candidato.Name;
				salida.comment = candidato.Comment;
				salida.color[0] = candidato.Color0;
				salida.color[1] = candidato.Color1;
				salida.mvarMaxSpeed = candidato.MaxSpeed;
				salida.mcolSteps = await GetAsimilationSteps(candidato.Id);
				salida.origin = GetStation(candidato.OriginStationId);
				return salida;
			}
			return null;
		}

		#endregion Asimilation

		#region Axis
		internal async Task Insert(Axis rhs)
		{
			System.Diagnostics.Debug.Assert(null != mvarCurrentTopoStorage);
			await Remove(rhs, true); //Eliminamos cualquier eje existente con este id.
			DBAxis nuevo = new DBAxis();
			nuevo.AxisId = rhs.id;
			nuevo.StorageId = mvarCurrentTopoStorage.Id;
			nuevo.Name = rhs.mvarName;
			nuevo.Comment = rhs.mvarComment;
			nuevo.Color0 = rhs.mvarColor[0];
			nuevo.Color1 = rhs.mvarColor[1];
			Axis.Add(nuevo);
			await SaveChangesAsync(); //Necesario para incrementar Id.
						   //Ahora vamos con las entidades de este eje.
			foreach (Station station in rhs.mcolStations)
				await Insert(station, nuevo.Id);
			foreach (RefPunctual refe in rhs.mcolPoints)
				await Insert(refe, nuevo.Id);
			//TODO: Añadir el resto del contenido del eje


			await SaveChangesAsync();
		}
		internal async Task Remove(Axis rhs, bool update = true) {await RemoveAxis(rhs.id, update);}
		internal async Task RemoveAxis(string AxisId, bool update = true)
		{
			if(null!=CurrentTopoStorage)
			{ //Tengo al menos un storage donde buscar.
				int StorageId = mvarCurrentTopoStorage.Id;
				DBAxis? auxEje = await Axis.Where(x => x.StorageId == StorageId && x.AxisId == AxisId).FirstOrDefaultAsync();
				if(null!=auxEje)
				{
					//Eliminamos las estaciones
					await RemoveStations(auxEje.StorageId);
					//eliminamos todos los elementos del eje.
					await RemovePunctuals(auxEje.StorageId);
					//Ahora ya podemos eliminar el eje.
					Axis.Remove(auxEje);
					if (update) await SaveChangesAsync();
				}		
			}
		}
		/// <summary>
		/// Recuperamos un solo eje.
		/// </summary>
		/// <param name="AxisId">Nombre del eje</param>
		/// <returns>Eje recuperado</returns>
		internal async Task<Axis?> getAxis(int AxisId)
		{
			if(null!=CurrentTopoStorage)
			{
				if (mcolAxisCache.ContainsKey(AxisId))
					return mcolAxisCache[AxisId];
				else
				{
					int storageId = mvarCurrentTopoStorage.Id;
					DBAxis? entrada = await Axis.Where(x => x.StorageId == storageId && x.Id == AxisId).FirstOrDefaultAsync();
					if (null != entrada)
					{
						Axis salida = new Axis();
						salida.id = entrada.AxisId;
						salida.mvarName = entrada.Name;
						salida.mvarComment = entrada.Comment;
						salida.mvarColor[0] = entrada.Color0;
						salida.mvarColor[1] = entrada.Color1;
						salida.mcolPoints = await GetRefPunctual(entrada.Id);
						salida.mcolStations = await GetStations(entrada, salida);
						salida.RecalculateLinearBounds();
						//TODO: Añadir el resto del contenido del eje.

						//Añado a la caché para agilizar operaciones futuras.
						mcolAxisCache.Add(AxisId, salida);
						return salida;
					}
				}			
			}
			return null;
		}
		//Recuperamos la colección completa de ejes
		internal async Task<List<Axis>> getAxis()
		{
			List<Axis> salida = new List<Axis>();
			if(null!=CurrentTopoStorage)
			{
				int storageId = mvarCurrentTopoStorage.Id;
				List<DBAxis> entrada = await Axis.Where(x => x.StorageId == storageId).ToListAsync();
				foreach(DBAxis eje in entrada)
				{
					Axis? elemento = await getAxis(eje.Id);
					if (null != elemento) 
						salida.Add(elemento);
				}
			}		
			return salida;
		}
		#endregion Axis

		#region Station
		internal async Task Insert(Station rhs, int axisId)
		{
			await Remove(rhs, axisId); //Elimino cualquier instancia que pudiera haber de antes.

			DBStation nueva = new DBStation();
			nueva.StationId = rhs.id;
			nueva.AxisId = axisId;
			nueva.Pk = rhs.pk;
			nueva.Name = rhs.name;
			nueva.ShortName = rhs.shortName;
			Stations.Add(nueva);
			//Insert((RefPunctual)rhs, axisId);
			await SaveChangesAsync();
		}
		internal async Task Remove(Station rhs, int axisId, bool update = true)
		{
			//Elimino la entidad puntual.
			await Remove(rhs.pk,axisId,false);
			//Elimino la entrada de la estación.
			DBStation? auxEstacion = await Stations.Where(x => x.AxisId == axisId && x.StationId == rhs.id).FirstOrDefaultAsync();
			if(null!=auxEstacion)
			{
				Stations.Remove(auxEstacion);
				if (update) await SaveChangesAsync();
			}				
		}
		internal async Task RemoveStations(int axisId, bool update = true)
		{
			List<DBStation> entrada = await Stations.Where(x => x.AxisId == axisId).ToListAsync();
			foreach(DBStation estacion in entrada)
				await Remove(estacion.Pk, axisId, false);
			RemoveRange(entrada);
			if (update) await SaveChangesAsync();
		}
		internal async Task<List<Station>> GetStations(DBAxis eje, Axis oniceAxis)
		{
			List<DBStation> entrada = await Stations.Where(x => x.AxisId == eje.Id).ToListAsync();
			List<Station> salida = new List<Station>();
			foreach(DBStation estacion in entrada)
			{
				Station? auxSalida = await GetStation(estacion.StationId, eje,oniceAxis);
				if (null != auxSalida)
					salida.Add(auxSalida);
			}
			return salida;
		}
		internal async Task<Station?> GetStation(string stationId, DBAxis eje, Axis oniceAxis)
		{
			System.Diagnostics.Debug.Assert(null != eje);
			DBStation? entrada = await Stations.Where(x => x.AxisId == eje.Id && x.StationId == stationId).FirstOrDefaultAsync();
			if (null == entrada) return null;
			return await GetStation(entrada.Id,oniceAxis, entrada.AxisId);
		}
		internal Station? GetStation(int id)
		{
			if (mcolStationCache.ContainsKey(id))
				return mcolStationCache[id];
			return null;
		}
		internal async Task<Station?> GetStation(int id, Axis oniceAxis, int dbAxisId)
		{
			if (mcolStationCache.ContainsKey(id))
				return mcolStationCache[id];
			else
			{
				DBStation? entrada = await Stations.Where(x => x.Id == id).FirstOrDefaultAsync();
				if(null!=entrada)
				{
					DBRefPunctual? auxPunto = await RefPunctuals.Where(x => x.AxisId == dbAxisId && x.Pk == entrada.Pk).FirstOrDefaultAsync();
					System.Diagnostics.Debug.Assert(null != auxPunto);
					Station salida = new Station(
						entrada.StationId,
						entrada.Name,
						entrada.ShortName,
						oniceAxis,
						auxPunto.Latitude,
						auxPunto.Longitude);
					mcolStationCache.Add(id, salida);
					return salida;
				}
			}
			return null;
		}

		internal async Task<DBStation?> GetDBStation(string id, string axisId)
		{
			if(null!=mvarCurrentTopoStorage)
			{
				int storageId = mvarCurrentTopoStorage.Id;
				DBAxis? auxEje = await Axis.Where(x => x.AxisId == axisId && x.StorageId ==storageId).FirstOrDefaultAsync();
				if(null!=auxEje)
				{
					DBStation? auxSalida = await Stations.Where(x => x.AxisId == auxEje.Id && x.StationId == id).FirstOrDefaultAsync();
					if (null != auxSalida) return auxSalida;
				}
			}
			return null;
		}
		#endregion Station

		#region RefPunctual
		internal async Task Insert(RefPunctual rhs, int axisId)
		{
			await Remove(rhs.pk, axisId,false); //Primero quito algún elemento previo en esta posición.			
			DBRefPunctual nuevo = new DBRefPunctual();
			nuevo.AxisId = axisId;
			nuevo.Latitude = rhs.point.Latitude;
			nuevo.Longitude = rhs.point.Longitude;
			nuevo.Pk = rhs.pk;
			if(!(nuevo.Pk<0))
			{
				try
				{
					RefPunctuals.Add(nuevo);
					await SaveChangesAsync();
				}
				catch (System.InvalidOperationException ex)
				{
					Console.WriteLine(string.Format("Error al añadir punto en eje {0} con Pk {1} (Coordenadas {2},{3})", nuevo.AxisId, nuevo.Pk, nuevo.Latitude, nuevo.Longitude));
				}
			}
		}
		internal async Task Remove(long pk, int axisId, bool update = true)
		{
			DBRefPunctual? elemento = await RefPunctuals.Where(x => x.AxisId == axisId && x.Pk == pk).FirstOrDefaultAsync();
			if (null != elemento)
			{
				RefPunctuals.Remove(elemento);
				if(update) await SaveChangesAsync();
			}				
		}
		/// <summary>
		/// Elimina todas las entidades puntuales de un determinado eje
		/// </summary>
		/// <param name="axisId">Id del Eje</param>
		internal async Task RemovePunctuals(int axisId)
		{
			List<DBRefPunctual> xx = await RefPunctuals.Where(x => x.AxisId == axisId).ToListAsync();
			RefPunctuals.RemoveRange(xx);
			await SaveChangesAsync();
		}
		internal async Task<RefPunctual?> GetRefPunctual(long pk, int axisId)
		{
			DBRefPunctual? xx = await RefPunctuals.Where(x => x.AxisId == axisId && x.Pk == pk).FirstOrDefaultAsync();
			if(null!=xx)
			{
				RefPunctual salida = new RefPunctual(xx.Latitude,xx.Longitude);
				salida.pk = xx.Pk;
				return salida;
			}
			return null;
		}
		internal async Task<List<RefPunctual>> GetRefPunctual(int axisId)
		{
			List<DBRefPunctual> xx = await RefPunctuals.Where(x => x.AxisId == axisId).ToListAsync();
			List<RefPunctual> salida = new List<RefPunctual>();
			foreach (DBRefPunctual ppp in xx)
			{
				RefPunctual nuevo = new RefPunctual(ppp.Latitude, ppp.Longitude);
				nuevo.pk = ppp.Pk;
				salida.Add(nuevo);
			}
			return salida;
		}
		#endregion RefPunctual





	}
}

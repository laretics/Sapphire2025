using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite.Query.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Numerics;
using System.Reflection.PortableExecutable;
using System.Runtime.Serialization;
using System.Security.AccessControl;
using System.Security.Cryptography;
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
		internal DbSet<DBRauta> Rautatie { get; set; }
		internal DbSet<DBPlan> Plans { get; set; }
		internal DbSet<DBCirculation> Circulations { get; set; }

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
		private Dictionary<int, TopoStorage> mcolTopoStorages = new Dictionary<int, TopoStorage>(); //Caché con los TopoStorages cargados

		internal async Task TotalRemove()
		{
			List<DBHeader> auxHeaders = await Headers.ToListAsync();
			List<DBRefPunctual> auxRefPunctuals = await RefPunctuals.ToListAsync();
			List<DBStation> auxStations = await Stations.ToListAsync();
			List<DBAxis> auxAxis = await Axis.ToListAsync();
			List<DBTopoStorage> auxStorages = await TopoStorages.ToListAsync();
			List<DBRauta> auxRautatie = await Rautatie.ToListAsync();
			List<DBPlan> auxPlans = await Plans.ToListAsync();
			List<DBCirculation> auxCirculations = await Circulations.ToListAsync();
			Headers.RemoveRange(auxHeaders);
			RefPunctuals.RemoveRange(auxRefPunctuals);
			Stations.RemoveRange(auxStations);
			Axis.RemoveRange(auxAxis);
			TopoStorages.RemoveRange(auxStorages);
			Rautatie.RemoveRange(auxRautatie);
			Plans.RemoveRange(auxPlans);
			Circulations.RemoveRange(auxCirculations);
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


        #region Rautatie
		internal async Task RemoveRautatie (int topoStorageId)
		{
			IEnumerable<DBRauta> lista = await Rautatie.Where(x => x.TopoStorageId == topoStorageId).ToListAsync();
			foreach (DBRauta elemento in lista)
				await RemoveRauta(elemento.HeaderId);
		}
		internal async Task RemoveRauta(Guid id)
		{
			DBRauta? auxRauta = await Rautatie.Where(x => x.HeaderId == id).FirstOrDefaultAsync();
			if(null!=auxRauta)
			{
				//Existe... ahora hay que comprobar si existen sus elementos.
				List<DBPlan> auxPlanes = await Plans.Where(x => x.RautaId == auxRauta.Id).ToListAsync();
				foreach(DBPlan auxPlan in auxPlanes)
				{
					//Se eliminan circulaciones y turnos de los planes afectados..
					List<DBCirculation> auxCirculations = await Circulations.Where(x => x.PlanId == auxPlan.Id).ToListAsync();
					Circulations.RemoveRange(auxCirculations);

					//TODO: Eliminar los turnos encadenados a estas circulaciones...
					 

				}
				Plans.RemoveRange(auxPlanes);

				Rautatie.Remove(auxRauta);
				await SaveChangesAsync();//Transacción de una sola vez.
			}
		}
		internal async Task InsertRautatie(TopoStorage topoStorage)
		{
			//Elimino rautatie existentes:
			foreach (Rauta auxRauta in topoStorage.mcolRauta.Values)
				await RemoveRauta(auxRauta.Header.Id);

			//Antes de empezar tengo que obtener el registro del TopoStorage en la base de datos.
			DBTopoStorage? auxDBTopoStorage = await TopoStorages.Where(x => x.HeaderId == topoStorage.Header.Id).FirstOrDefaultAsync();
			if(null!=auxDBTopoStorage)
			{
                foreach (Rauta auxRauta in topoStorage.mcolRauta.Values)
                {
                    DBRauta nuevoRauta = new DBRauta();
                    nuevoRauta.HeaderId = auxRauta.Header.Id;
                    nuevoRauta.TopoStorageId = auxDBTopoStorage.Id;
                    Rautatie.Add(nuevoRauta);
                    await SaveChangesAsync();
					await Insert(auxRauta.Header);
                    foreach (Plan auxPlan in auxRauta.Plans)
                    {
                        DBPlan nuevoPlan = new DBPlan();
                        nuevoPlan.RautaId = nuevoRauta.Id;
                        nuevoPlan.PlanId = auxPlan.Id;
                        nuevoPlan.Name = auxPlan.Name;
                        nuevoPlan.Comment = auxPlan.mvarComment;
                        nuevoPlan.Color0 = auxPlan.mvarColor[0] ?? "black";
                        nuevoPlan.Color1 = auxPlan.mvarColor[1] ?? "white";
                        Plans.Add(nuevoPlan);
                        await SaveChangesAsync();
                        foreach (Circulation auxCircula in auxPlan.mcolCirculations.Values)
                        {
                            if (null != auxCircula.asimilation)
                            {
                                DBCirculation nuevaCircula = new DBCirculation();
                                nuevaCircula.PlanId = nuevoPlan.Id;
                                nuevaCircula.AsimilationId = auxCircula.asimilation.id;
                                nuevaCircula.Name = auxCircula.name;
                                nuevaCircula.Departure = auxCircula.departure;
                                nuevaCircula.Comment = auxCircula.comment;
                                nuevaCircula.Color0 = auxCircula.color[0] ?? "black";
                                nuevaCircula.Color1 = auxCircula.color[1] ?? "white";
                                Circulations.Add(nuevaCircula);
                            }
                        }
                        await SaveChangesAsync();

                        //TODO: Añadir aquí los turnos.

                    }
                }
            }
		}
		internal async Task<Dictionary<Guid,Rauta>> GetRautatie(int topoId, TopoStorage topoStorage)
		{
			Dictionary<Guid, Rauta> salida = new Dictionary<Guid, Rauta>();			
			List<DBRauta> entrada = await Rautatie.Where(x => x.TopoStorageId==topoId).ToListAsync();
			foreach(DBRauta auxRauta in entrada)
			{				
				Header? auxCabecera = await GetHeader(auxRauta.HeaderId);
				if(null!=auxCabecera)
				{
                    Rauta nuevoRauta = new Rauta(topoStorage);
                    nuevoRauta.Header = auxCabecera;
					nuevoRauta.mvarParent = topoStorage;
                    //Cargamos los planes del rauta
                    List < DBPlan > planes = await Plans.Where(x => x.RautaId == auxRauta.Id).ToListAsync();
                    foreach (DBPlan auxPlan in planes)
                    {
						Plan nuevoPlan = new Plan();
						nuevoPlan.mvarId = auxPlan.PlanId;
						nuevoPlan.mvarName = auxPlan.Name;
						nuevoPlan.mvarComment = auxPlan.Comment;
						nuevoPlan.mvarColor[0] = auxPlan.Color0 ?? "black";
                        nuevoPlan.mvarColor[1] = auxPlan.Color1 ?? "white";
						nuevoPlan.TopoId = topoStorage.Header.Id;
                        //Cargamos las circulaciones del plan.
                        List<DBCirculation> circulaciones = await Circulations.Where(x => x.PlanId == auxPlan.Id).ToListAsync();
						foreach (DBCirculation auxCirculation in circulaciones)
						{
							if(topoStorage.mcolAsimilations.ContainsKey(auxCirculation.AsimilationId))
							{
								Circulation nuevaCirculation = new Circulation();
								nuevaCirculation.departure = auxCirculation.Departure;
								nuevaCirculation.color[0] = auxCirculation.Color0;
                                nuevaCirculation.color[1] = auxCirculation.Color1;
								nuevaCirculation.comment = auxCirculation.Comment;
								nuevaCirculation.name = auxCirculation.Name;
								nuevaCirculation.asimilation = topoStorage.mcolAsimilations[auxCirculation.AsimilationId];
								nuevoPlan.mcolCirculations.Add(nuevaCirculation.name, nuevaCirculation);
                            }
						}

						//Cargamos los turnos del plan


						nuevoRauta.mcolPlans.Add(nuevoPlan.Id, nuevoPlan);
                    }
					salida.Add(auxCabecera.Id, nuevoRauta);
                }
			}
			return salida;
		}
        #endregion Rautatie

        #region TopoStorage
        internal async Task<Dictionary<Guid,TopoStorage>> GetTopoStorages()
		{
			Dictionary<Guid, TopoStorage> salida = new Dictionary<Guid, TopoStorage>();
			List<DBTopoStorage> entrada = await TopoStorages.ToListAsync();
			foreach(DBTopoStorage auxEntrada in entrada)
			{
				TopoStorage? nuevo = await GetTopoStorage(auxEntrada.HeaderId);
				if (null != nuevo)
					salida.Add(auxEntrada.HeaderId, nuevo);
			}
			return salida;
		}
		internal async Task<TopoStorage?> GetTopoStorage(Guid id)
		{
			DBTopoStorage? tablaIndice = await TopoStorages.Where(x => x.HeaderId == id).FirstOrDefaultAsync();
			if (null == tablaIndice) return null;
			return await GetTopoStorage(tablaIndice.Id);
        }
		internal async Task<TopoStorage?> GetTopoStorage(int id)
		{
			if (mcolTopoStorages.ContainsKey(id))
			{
                mvarCurrentTopoStorage = await TopoStorages.Where(x => x.Id == id).FirstOrDefaultAsync();
				CurrentTopoStorage = mcolTopoStorages[id];
				return CurrentTopoStorage;
            }
							
			TopoStorage salida = new TopoStorage();
            mcolAxisCache = new Dictionary<int, Axis>(); //Necesito este caché para cargar los ejes.
            mcolStationCache = new Dictionary<int, Station>(); //Necesito este caché para cargar las estaciones.
			mvarCurrentTopoStorage = await TopoStorages.Where(x => x.Id == id).FirstOrDefaultAsync();
			if (null == mvarCurrentTopoStorage) return null; //No existe en la base de datos.
            Header? auxHeader = await GetHeader(mvarCurrentTopoStorage.HeaderId);
            System.Diagnostics.Debug.Assert(null != auxHeader, "Inconsistencia en la base de datos respecto a un Header en un TopoStorage");
            salida.Header = auxHeader;
            CurrentTopoStorage = salida; //Necesitamos este ajuste para recuperar los elementos de esta colección de topologías.

            //Recuperamos todos los ejes.
            List<Axis> auxEjes = await getAxis();
            foreach (Axis eje in auxEjes)
                salida.mcolAxis.Add(eje.id, eje);
            List<Asimilation> asimilations = await GetAsimilations();
            foreach (Asimilation asim in asimilations)
                salida.mcolAsimilations.Add(asim.id, asim);
			Dictionary<Guid,Rauta> rautatie = await GetRautatie(id, CurrentTopoStorage);			
			salida.mcolRauta = rautatie;
            mcolTopoStorages.Add(id, salida);
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
			//Metemos las asimilaciones que contiene
			foreach (Asimilation asim in rhs.ColAsimilations)
				await Insert(asim);
		}
		internal async Task RemoveTopoStorage(Guid rhs, bool update = true)
		{
			DBTopoStorage? auxCandidato = await TopoStorages.Where(x => x.HeaderId == rhs).FirstOrDefaultAsync();
			if (null == auxCandidato) return;
			await GetTopoStorage(rhs);
			IEnumerable<DBAxis> ejes = await Axis.Where(x => x.StorageId == auxCandidato.Id).ToListAsync();
			foreach(DBAxis eje in ejes)
				await RemoveAxis(eje.AxisId, false);
			await RemoveAsimilations(auxCandidato.Id);

			System.Diagnostics.Debug.Assert(null != mvarCurrentTopoStorage);
			await RemoveRautatie(mvarCurrentTopoStorage.Id); //Eliminar los rautas del topoStorage seleccionado.
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
			Asimilations.Add(nueva);
			await SaveChangesAsync(); //Doy valor al id de la asimilación.
			foreach (AsimilationStep paso in rhs.mcolSteps)
				await Insert(paso,nueva.Id);
		}		
		internal async Task RemoveAsimilations(int topoStorageId)
		{
            List<DBAsimilation> asimilations = await Asimilations.Where(x => x.TopoStorageId == topoStorageId).ToListAsync();
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
		internal async Task<List<Asimilation>> GetAsimilations()
		{
			List<Asimilation> salida = new List<Asimilation>();
			if(null!=mvarCurrentTopoStorage)
			{
				int storageId = mvarCurrentTopoStorage.Id;
				List<DBAsimilation> entrada = await Asimilations.Where(x => x.TopoStorageId == storageId).ToListAsync();
				foreach(DBAsimilation asimila in entrada)
				{
					Asimilation? elemento = await GetAsimilation(asimila.Id);
					if (null != elemento)
						salida.Add(elemento);
				}
			}
			return salida;
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
			if(null!=mvarCurrentTopoStorage)
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
			if(null!=mvarCurrentTopoStorage)
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

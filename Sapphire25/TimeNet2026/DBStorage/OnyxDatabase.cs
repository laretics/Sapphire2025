using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite.Query.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
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
		internal DbSet<DBSchedule> Schedules { get; set; }
		internal DbSet<DBScheduleUnit> ScheduleUnits { get; set; }

		public OnyxDatabase(DbContextOptions<OnyxDatabase> opciones) : base(opciones) { }
		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<DBRefPunctual>()
				.HasKey(e => new { e.AxisId, e.Pk }); // Clave primaria compuesta
		}
		private DBTopoStorage? mvarCurrentTopoStorage { get; set; }
		internal TopoStorage? CurrentTopoStorage {get; set; } //Para operaciones con ejes.
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
			List<DBSchedule> auxSchedules = await Schedules.ToListAsync();
			List<DBScheduleUnit> auxScheduleUnits = await ScheduleUnits.ToListAsync();
			Headers.RemoveRange(auxHeaders);
			RefPunctuals.RemoveRange(auxRefPunctuals);
			Stations.RemoveRange(auxStations);
			Axis.RemoveRange(auxAxis);
			TopoStorages.RemoveRange(auxStorages);
			Rautatie.RemoveRange(auxRautatie);
			Plans.RemoveRange(auxPlans);
			Circulations.RemoveRange(auxCirculations);
			Schedules.RemoveRange(auxSchedules);
			ScheduleUnits.RemoveRange(auxScheduleUnits);
			await SaveChangesAsync();
		}

		#region Header
		internal void SerializeHeader(Header rhs)
		{
			RemoveHeader(rhs.Id); //Elimino cualquier etiqueta anterior.
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
		internal void RemoveHeader(Guid id)
		{
			List<DBHeader> lista = Headers.Where(x => x.Id == id).ToList();			
			if(lista.Count>0)
                Headers.RemoveRange(lista);
		}
		internal async Task<Header?> DeserializeHeader(Guid id)
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

					List<DBSchedule> auxSchedules = await Schedules.Where(x => x.PlanId == auxPlan.Id).ToListAsync();
					foreach(DBSchedule auxSchedule in auxSchedules)
					{
						List<DBScheduleUnit> auxUnits = await ScheduleUnits.Where(x => x.ScheduleId == auxSchedule.Id).ToListAsync();
						ScheduleUnits.RemoveRange(auxUnits);
					}
					Schedules.RemoveRange(auxSchedules);
				}
				Plans.RemoveRange(auxPlanes);
				RemoveHeader(id);
				Rautatie.Remove(auxRauta);
				await SaveChangesAsync();//Transacción de una sola vez.
			}
		}
		internal async Task SerializeRautatie(TopoStorage topoStorage)
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
                    SerializeHeader(auxRauta.Header);
                    await SaveChangesAsync();                    
                    foreach (Plan auxPlan in auxRauta.Plans.Values)
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

						foreach (Schedule auxSchedule in auxPlan.Schedules)
						{
							DBSchedule nuevoSchedule = new DBSchedule();
							nuevoSchedule.PlanId = nuevoPlan.Id;
							nuevoSchedule.Name = auxSchedule.nameCloudString;
							nuevoSchedule.Comment = auxSchedule.comment;
							nuevoSchedule.WeekdayMask = auxSchedule.weekdayMask;
							nuevoSchedule.Color1 = auxSchedule.color[0] ?? "black";
							nuevoSchedule.Color2 = auxSchedule.color[1] ?? "white";
							nuevoSchedule.CoordinateX = auxSchedule.coordinates[0];
							nuevoSchedule.CoordinateY = auxSchedule.coordinates[1];
							Schedules.Add(nuevoSchedule);
							await SaveChangesAsync();
							foreach (Schedule.ScheduleItem auxItem in auxSchedule.mcolItems)
							{
								DBScheduleUnit nuevoUnit = new DBScheduleUnit();
								nuevoUnit.ScheduleId = nuevoSchedule.Id;
								nuevoUnit.Begin = auxItem.timeLapse.Begin;
								nuevoUnit.End = auxItem.timeLapse.End;
								if (null != auxItem.circulation) //Esto es un tren
								{
									DBCirculation? auxCirculation = await Circulations.Where(x => x.PlanId == nuevoPlan.Id && x.Name == auxItem.circulation.name).FirstOrDefaultAsync();
									if (null == auxCirculation)
									{
										Console.WriteLine(string.Format("Error: Incoherencia de datos en el turno {0} con la circulación {1}.", nuevoSchedule.Name, auxItem.circulation.name));
									}
									else
									{
										//La circulación existe.
										nuevoUnit.CirculationId = auxCirculation.Id;
										nuevoUnit.Active = auxItem.active;
									}
								}
								ScheduleUnits.Add(nuevoUnit);
							}
							await SaveChangesAsync();
						}
					}
                }
            }
		}
		internal async Task<Dictionary<Guid,Rauta>> DeserializeRautatie(TopoStorage topoStorage)
		{
			Dictionary<Guid, Rauta> salida = new Dictionary<Guid, Rauta>();
			DBTopoStorage? auxTopo = await TopoStorages.Where(x => x.HeaderId == topoStorage.Header.Id).FirstOrDefaultAsync();
			if (null == auxTopo) return salida;
			List<DBRauta> entrada = await Rautatie.Where(x => x.TopoStorageId==auxTopo.Id).ToListAsync();
			foreach(DBRauta auxRauta in entrada)
			{				
				Header? auxCabecera = await DeserializeHeader(auxRauta.HeaderId);
				if(null!=auxCabecera)
				{
                    Rauta nuevoRauta = new Rauta(topoStorage);
                    nuevoRauta.Header = auxCabecera;
					nuevoRauta.mvarParent = topoStorage;
                    //Cargamos los planes del rauta
                    List < DBPlan > planes = await Plans.Where(x => x.RautaId == auxRauta.Id).ToListAsync();
                    foreach (DBPlan auxPlan in planes)
                    {
						Plan nuevoPlan = new Plan(topoStorage);
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

						List<DBSchedule> schedules = await Schedules.Where(x => x.PlanId == auxPlan.Id).ToListAsync();
						foreach (DBSchedule schedule in schedules)
						{
							Schedule nuevoSchedule = new Schedule();
							nuevoSchedule.name = schedule.Name;
							nuevoSchedule.comment = schedule.Comment;
							nuevoSchedule.weekdayMask = schedule.WeekdayMask;
							nuevoSchedule.color[0] = schedule.Color1 ?? "black";
							nuevoSchedule.color[1] = schedule.Color2 ?? "white";
							nuevoSchedule.coordinates[0] = schedule.CoordinateX;
							nuevoSchedule.coordinates[1] = schedule.CoordinateY;
							//Cargamos las unidades del horario.
							List<DBScheduleUnit> unidades = await ScheduleUnits.Where(x => x.ScheduleId == schedule.Id).ToListAsync();
							foreach (DBScheduleUnit unidad in unidades)
							{
								if(unidad.CirculationId<0) //Turno de depósito
								{
									TimeLapse lapso = new TimeLapse { Begin = unidad.Begin, End = unidad.End };
									Schedule.ScheduleItem nuevoItem = new Schedule.ScheduleItem(lapso, unidad.Active);
									nuevoSchedule.mcolItems.Add(nuevoItem);
								}
								else //Tracción o acompañamiento
								{
									DBCirculation? auxCirculation = await Circulations.Where(x => x.Id == unidad.CirculationId).FirstOrDefaultAsync();
									System.Diagnostics.Debug.Assert(null != auxCirculation);
									if (null != auxCirculation)
									{
										if (nuevoPlan.mcolCirculations.ContainsKey(auxCirculation.Name))
										{
											Schedule.ScheduleItem nuevoItem = new Schedule.ScheduleItem(nuevoPlan.mcolCirculations[auxCirculation.Name], unidad.Active);
											nuevoItem.active = unidad.Active;
											nuevoSchedule.mcolItems.Add(nuevoItem);
										}
									}
								}
							}
							nuevoPlan.mcolSchedules.Add(nuevoSchedule);
						}
						nuevoRauta.mcolPlans.Add(nuevoPlan.Id, nuevoPlan);
                    }
					salida.Add(auxCabecera.Id, nuevoRauta);
                }
			}
			return salida;
		}
        #endregion Rautatie

        #region TopoStorage
        internal async Task<Dictionary<Guid,TopoStorage>> DeserializeTopoStorages()
		{
			Dictionary<Guid, TopoStorage> salida = new Dictionary<Guid, TopoStorage>();
			List<DBTopoStorage> entrada = await TopoStorages.ToListAsync();
			foreach(DBTopoStorage auxEntrada in entrada)
			{
				TopoStorage? nuevo = await DeserializeTopoStorage(auxEntrada.HeaderId);
				if (null != nuevo)
					salida.Add(auxEntrada.HeaderId, nuevo);
			}
			return salida;
		}
		internal async Task<TopoStorage?> DeserializeTopoStorage(Guid id)
		{
			DBTopoStorage? auxTopoStorage = await TopoStorages.Where(x => x.HeaderId == id).FirstOrDefaultAsync();
			if (null == auxTopoStorage) return null;
			TopoStorage salida = new TopoStorage();
			//Cargamos todos los elementos del TopoStorage afectado.
			//Carga del Header.
			Header? auxHeader = await DeserializeHeader(id);
			if (null == auxHeader) return null;
			salida.Header = auxHeader;
			//Carga de los ejes.
			Dictionary<int, Station> auxAllStationsCache = new Dictionary<int, Station>();
			Dictionary<int, Axis> auxAllAxisCache = new Dictionary<int, Axis>();
			IEnumerable<DBAxis> auxAxises = await Axis.Where(x => x.StorageId == auxTopoStorage.Id).ToListAsync();
			foreach(DBAxis auxAxis in auxAxises)
			{
				Axis nuevoAxis = new Axis();
				nuevoAxis.id = auxAxis.AxisId;
				nuevoAxis.mvarName = auxAxis.Name;
				nuevoAxis.mvarComment = auxAxis.Comment;
				nuevoAxis.mvarColor[0] = auxAxis.Color0 ?? "black";
                nuevoAxis.mvarColor[1] = auxAxis.Color1 ?? "white";
				Dictionary<long, Station> auxCacheStations = new Dictionary<long, Station>();
                //Estaciones del eje
                IEnumerable<DBStation> auxStations = await Stations.Where(x => x.AxisId == auxAxis.Id).ToListAsync();
                foreach (DBStation auxStation in auxStations)
                {
					Station nuevaStation = new Station
						(auxStation.StationId,
						auxStation.Name,
						auxStation.ShortName,
						nuevoAxis,
						0,
						0);
					nuevaStation.pk = auxStation.Pk;
					auxCacheStations.Add(nuevaStation.pk, nuevaStation);
					auxAllStationsCache.Add(auxStation.Id, nuevaStation);
                }
                //Puntos singulares y estaciones del eje.
                IEnumerable<DBRefPunctual> auxPunctuals = await RefPunctuals.Where(x => x.AxisId == auxAxis.Id).ToListAsync();
				foreach (DBRefPunctual auxPunctual in auxPunctuals)
				{
					if(auxCacheStations.ContainsKey(auxPunctual.Pk))
					{
						Station auxxStation = auxCacheStations[auxPunctual.Pk];
						auxxStation.point = new GeoLocation(auxPunctual.Latitude, auxPunctual.Longitude);
						nuevoAxis.mcolPoints.Add(auxxStation);
						nuevoAxis.mcolStations.Add(auxxStation);
					}
					else
					{
                        RefPunctual nuevoPunctual = new RefPunctual(auxPunctual.Latitude, auxPunctual.Longitude);
                        nuevoPunctual.pk = auxPunctual.Pk;
                        nuevoAxis.mcolPoints.Add(nuevoPunctual);
                    }
				}
                #region todos
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
                #endregion todos

                auxAllAxisCache.Add(auxAxis.Id, nuevoAxis);
                salida.mcolAxis.Add(nuevoAxis.id, nuevoAxis);
            }

			//Carga de las asimilaciones
			IEnumerable<DBAsimilation> auxAsimilations = await Asimilations.Where(x => x.TopoStorageId == auxTopoStorage.Id).ToListAsync();
			foreach(DBAsimilation auxAsimilation in auxAsimilations)
			{
				if(auxAllStationsCache.ContainsKey(auxAsimilation.OriginStationId))
				{
                    Asimilation nuevaAsimilation = new Asimilation(salida);
                    nuevaAsimilation.id = auxAsimilation.AsimilationId;
                    nuevaAsimilation.mvarName = auxAsimilation.Name;
                    nuevaAsimilation.mvarComment = auxAsimilation.Comment;
                    nuevaAsimilation.color[0] = auxAsimilation.Color0 ?? "white";
                    nuevaAsimilation.color[1] = auxAsimilation.Color1 ?? "black";
                    nuevaAsimilation.mvarMaxSpeed = auxAsimilation.MaxSpeed;
					nuevaAsimilation.origin = auxAllStationsCache[auxAsimilation.OriginStationId];
					//Carga de los pasos de cada asimilación
					IEnumerable<DBAsimilationStep> auxAsimilationSteps = await AsimilationSteps.Where(x => x.AsimilationId == auxAsimilation.Id).ToListAsync();
					foreach(DBAsimilationStep auxAsimilationStep in auxAsimilationSteps)
					{
						if(auxAllStationsCache.ContainsKey(auxAsimilationStep.DestinationStationId)&&
							auxAllAxisCache.ContainsKey(auxAsimilationStep.AxisId))
						{
							AsimilationStep nuevoPaso = new AsimilationStep
								(
								auxAllStationsCache[auxAsimilationStep.DestinationStationId],
								auxAllAxisCache[auxAsimilationStep.AxisId],
								auxAsimilationStep.tripTime,
								auxAsimilationStep.stopTime
								);
							nuevaAsimilation.mcolSteps.Add(nuevoPaso);
						}
					}
					salida.mcolAsimilations.Add(nuevaAsimilation.id, nuevaAsimilation);
                }
            }
			return salida;
        }
        internal async Task RemoveTopoStorage(Guid id)
        {
            DBTopoStorage? auxTopoStorage = await TopoStorages.Where(x => x.HeaderId == id).FirstOrDefaultAsync();
            if (null != auxTopoStorage)
			{
				//Primero elimino todos los rautatie relacionados.
				await RemoveRautatie(auxTopoStorage.Id);
				//Ahora ya puedo eliminar toda la jerarquía.
				RemoveHeader(id);

                IEnumerable<DBAxis> auxAxises = await Axis.Where(x => x.StorageId == auxTopoStorage.Id).ToListAsync();
                foreach (DBAxis auxAxis in auxAxises)
                {
                    IEnumerable<DBStation> auxStations = await Stations.Where(x => x.AxisId == auxAxis.Id).ToListAsync();
					Stations.RemoveRange(auxStations);
                    //Puntos singulares y estaciones del eje.
                    IEnumerable<DBRefPunctual> auxPunctuals = await RefPunctuals.Where(x => x.AxisId == auxAxis.Id).ToListAsync();
					RefPunctuals.RemoveRange(auxPunctuals);

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

                IEnumerable<DBAsimilation> auxAsimilations = await Asimilations.Where(x => x.TopoStorageId == auxTopoStorage.Id).ToListAsync();
                foreach (DBAsimilation auxAsimilation in auxAsimilations)
                {
                    IEnumerable<DBAsimilationStep> auxAsimilationSteps = await AsimilationSteps.Where(x => x.AsimilationId == auxAsimilation.Id).ToListAsync();
					AsimilationSteps.RemoveRange(auxAsimilationSteps);
                }
				Asimilations.RemoveRange(auxAsimilations);
                TopoStorages.Remove(auxTopoStorage);
				await SaveChangesAsync();
            }		
        }

        internal async Task SerializeTopoStorage(TopoStorage rhs)
		{
			await RemoveTopoStorage(rhs.Header.Id);			
			DBTopoStorage nuevo = new DBTopoStorage();
            SerializeHeader(rhs.Header);
            nuevo.HeaderId = rhs.Header.Id;			
			TopoStorages.Add(nuevo);
			await SaveChangesAsync();
			Dictionary<Station, int> auxColStations = new Dictionary<Station, int>();
			Dictionary<Axis, int> auxColAxis = new Dictionary<Axis, int>();
			foreach (Axis eje in rhs.mcolAxis.Values)
			{
				DBAxis nuevoEje = new DBAxis();
                nuevoEje.AxisId = eje.id;
                nuevoEje.StorageId = nuevo.Id;				
				nuevoEje.Name = eje.mvarName;
				nuevoEje.Comment = eje.mvarComment;
				nuevoEje.Color0 = eje.mvarColor[0];
                nuevoEje.Color1 = eje.mvarColor[1];
				Axis.Add(nuevoEje);				
				await SaveChangesAsync();
				auxColAxis.Add(eje, nuevoEje.Id);
				//Estaciones
				foreach	(Station estacion in eje.Stations)
				{
					DBStation nuevaEstacion = new DBStation();
					nuevaEstacion.StationId = estacion.id;
					nuevaEstacion.AxisId = nuevoEje.Id;
					nuevaEstacion.Pk = estacion.pk;
					nuevaEstacion.Name = estacion.name;
					nuevaEstacion.ShortName = estacion.shortName;
					Stations.Add(nuevaEstacion);
					await SaveChangesAsync();
					auxColStations.Add(estacion, nuevaEstacion.Id);
				}
				//Referencias puntuales
				foreach(RefPunctual punto in eje.mcolPoints)
				{
					DBRefPunctual nuevoPunto = new DBRefPunctual();
					nuevoPunto.AxisId = nuevoEje.Id;
					nuevoPunto.Pk = punto.pk;
					nuevoPunto.Latitude = punto.point.Latitude;
					nuevoPunto.Longitude = punto.point.Longitude;
					RefPunctuals.Add(nuevoPunto);
				}
				#region todos
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
				#endregion todos
				await SaveChangesAsync();
            }
			foreach (Asimilation asimilacion in rhs.ColAsimilations.Values)
			{
				DBAsimilation nuevaAsimilacion = new DBAsimilation();
				nuevaAsimilacion.TopoStorageId = nuevo.Id;
				nuevaAsimilacion.AsimilationId = asimilacion.id;
				nuevaAsimilacion.Name = asimilacion.name;
				nuevaAsimilacion.Comment = asimilacion.comment;
				nuevaAsimilacion.Color0 = asimilacion.mvarColor[0];
                nuevaAsimilacion.Color1 = asimilacion.mvarColor[1];
				nuevaAsimilacion.MaxSpeed = asimilacion.maxSpeed;
				System.Diagnostics.Debug.Assert(null != asimilacion.origin);
				nuevaAsimilacion.OriginStationId = auxColStations[asimilacion.origin];
				Asimilations.Add(nuevaAsimilacion);
				await SaveChangesAsync();
				foreach(AsimilationStep paso in asimilacion.mcolSteps)
				{
					DBAsimilationStep nuevoPaso = new DBAsimilationStep();
					nuevoPaso.AsimilationId = nuevaAsimilacion.Id;
					nuevoPaso.DestinationStationId = auxColStations[paso.destination];
					nuevoPaso.AxisId = auxColAxis[paso.destination.axis];
					nuevoPaso.tripTime = paso.tripTime;
					nuevoPaso.stopTime = paso.stopTime;
					AsimilationSteps.Add(nuevoPaso);
				}
				await SaveChangesAsync();
            }
        }
		#endregion TopoStorage


	}
}

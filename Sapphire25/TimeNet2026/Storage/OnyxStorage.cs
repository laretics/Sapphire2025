using System.Xml.Linq;
using TimeNet2026.DBStorage;
using TimeNet2026.Timed;
using TimeNet2026.Topo;
using TimeNet2026Data;
using TimeNet2026Data.DBStorage;
using TimeNet2026Data.Serialization;

namespace TimeNet2026.Storage
{
	public class OnyxStorage
	{
		private DBSerializer mvarSerializer;

		private Dictionary<Guid,TopoStorage> mcolTopoStorages;
		private Dictionary<int, TopoStorage> mcolCacheTopoStorages = new Dictionary<int, TopoStorage>(); //Caché con los TopoStorages cargados

		public OnyxStorage(ITimeNetContextStorage context)
		{
			mcolTopoStorages = new Dictionary<Guid, TopoStorage>();
			mvarSerializer = new DBSerializer(context);
			//mvarStorage.Database.EnsureCreated(); //Se asegura de que existe la base de datos.
		}
		public async Task EmptyDatabase() {await mvarSerializer.ClearDatabase();}
		public async Task Init()
		{
			//Primero sacamos los topos
			mcolTopoStorages = await DeserializeTopoStorages();
			//Luego cargamos los rautas.
			foreach(TopoStorage auxTopo in mcolTopoStorages.Values)
			{
                Dictionary<Guid,Rauta> rautatie = await DeserializeRautatie(auxTopo);
				auxTopo.mcolRauta = rautatie;
            }                
		}
	
		public Dictionary<Guid,TopoStorage> Storages { get => mcolTopoStorages; }

		/// <summary>
		/// Carga el nodo que viene y deserializa automáticamente lo que contenga.
		/// </summary>
		/// <param name="root"></param>
		public async Task deserializeXML(XNode root)
		{
			if(root is XElement element)
			{
				switch (element.Name.LocalName)
				{
					case "layout":
						await deserializeTopo(root);
						break;
					case "rautatie":
						await deserializeRauta(root);
						break;
					default:
						break;
				}
			}
		}

		internal async Task deserializeTopo(XNode root)
		{
			//Root es el nodo "layout"
			TopoStorage nuevo = new TopoStorage(root);
			await SerializeTopoStorage(nuevo);
		}
		internal async Task deserializeRauta(XNode root)
		{
			//Lo primero que tenemos que hacer es buscar el TopoStorage compatible
			//await Init();
			Guid auxId = Rauta.TopoStorageId(root);
			if(Guid.Empty!=auxId && mcolTopoStorages.ContainsKey(auxId))
			{
				TopoStorage auxTopoStorage = mcolTopoStorages[auxId];
				Rauta auxRauta = new Rauta(root, auxTopoStorage);
				auxTopoStorage.mcolRauta.Add(auxRauta.Header.Id, auxRauta);
				await SerializeRautatie(auxTopoStorage);
            }                            
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
			mvarSerializer.Add(auxHeader);			
		}
		internal void RemoveHeader(Guid id) {mvarSerializer.RemoveHeader(id);}
		internal async Task<Header?> DeserializeHeader(Guid id)
		{
			DBHeader? xx = await mvarSerializer.GetHeader(id);
			if (null != xx)
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
		internal async Task RemoveRautatie(int topoStorageId){await mvarSerializer.RemoveRauta(topoStorageId);}		
		internal async Task SerializeRautatie(TopoStorage topoStorage)
		{
			//Elimino rautatie existentes:
			foreach (Rauta auxRauta in topoStorage.mcolRauta.Values)
				await mvarSerializer.RemoveRauta(auxRauta.Header.Id);

			//Antes de empezar tengo que obtener el registro del TopoStorage en la base de datos.
			DBTopoStorage? auxDBTopoStorage = await mvarSerializer.GetTopoStorage(topoStorage.Header.Id);
			if (null != auxDBTopoStorage)
			{
				foreach (Rauta auxRauta in topoStorage.mcolRauta.Values)
				{
					DBRauta nuevoRauta = new DBRauta();
					nuevoRauta.HeaderId = auxRauta.Header.Id;
					nuevoRauta.TopoStorageId = auxDBTopoStorage.Id;
					mvarSerializer.Add(nuevoRauta);
					SerializeHeader(auxRauta.Header);
					await mvarSerializer.SaveChangesAsync();
					foreach (Plan auxPlan in auxRauta.Plans.Values)
					{
						DBPlan nuevoPlan = new DBPlan();
						nuevoPlan.RautaId = nuevoRauta.Id;
						nuevoPlan.PlanId = auxPlan.Id;
						nuevoPlan.Name = auxPlan.Name;
						nuevoPlan.Comment = auxPlan.mvarComment;
						nuevoPlan.Color0 = auxPlan.mvarColor[0] ?? "black";
						nuevoPlan.Color1 = auxPlan.mvarColor[1] ?? "white";
						mvarSerializer.Add(nuevoPlan);
						await mvarSerializer.SaveChangesAsync();
						foreach (CirculationBlock auxBlock in auxPlan.CirculationBlocks)
						{
							if (null != auxBlock.asimilation)
							{
								DBCirculationBlock nuevoBlock = new DBCirculationBlock();
								nuevoBlock.PlanId = nuevoPlan.Id;
								nuevoBlock.AsimilationId = auxBlock.asimilation.id;
								nuevoBlock.WeekdayMask = auxBlock.weekdayMask;
								nuevoBlock.Pattern = auxBlock.pattern;
								mvarSerializer.Add(nuevoBlock);
								await mvarSerializer.SaveChangesAsync();
								foreach (Circulation auxCircula in auxBlock.mcolCirculations)
								{
									DBCirculation nuevaCircula = new DBCirculation();
									nuevaCircula.BlockId = nuevoBlock.Id;
									nuevaCircula.Name = auxCircula.name;
									nuevaCircula.Departure = auxCircula.departure;
									nuevaCircula.Comment = auxCircula.comment;
									nuevaCircula.Color0 = auxCircula.color[0] ?? "black";
									nuevaCircula.Color1 = auxCircula.color[1] ?? "white";
									mvarSerializer.Add(nuevaCircula);
								}
							}
						}
						await mvarSerializer.SaveChangesAsync();

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
							mvarSerializer.Add(nuevoSchedule);
							await mvarSerializer.SaveChangesAsync();
							foreach (ScheduleItem auxItem in auxSchedule.mcolItems)
							{
								DBScheduleUnit nuevoUnit = new DBScheduleUnit();
								nuevoUnit.ScheduleId = nuevoSchedule.Id;
								nuevoUnit.Begin = auxItem.timeLapse.Begin;
								nuevoUnit.End = auxItem.timeLapse.End;
								nuevoUnit.Active = auxItem.active;
								if (null != auxItem.circulation) //Esto es un tren
								{
									//Tenemos que encontrar el registro correspondiente a esta circulación en la base de datos.
									IEnumerable<DBCirculationBlock> listaCirculaciones = await mvarSerializer.GetCirculationBlocks(nuevoPlan.Id);
									foreach (DBCirculationBlock bloque in listaCirculaciones)
									{
										List<DBCirculation> auxCirculacionesEnBloque = await mvarSerializer.GetCirculations(bloque.Id);
										foreach (DBCirculation circulacionEnBloque in auxCirculacionesEnBloque)
										{
											if (circulacionEnBloque.Name == auxItem.circulation.name)
											{
												nuevoUnit.CirculationId = circulacionEnBloque.Id;
												break;
											}
										}
										if (nuevoUnit.CirculationId > 0)
											break;
									}
								}
								else //Esto es un turno de depósito
									nuevoUnit.CirculationId = -1;

								mvarSerializer.Add(nuevoUnit);
							}
							await mvarSerializer.SaveChangesAsync();
						}
					}
				}
			}
		}
		internal async Task<Dictionary<Guid, Rauta>> DeserializeRautatie(TopoStorage topoStorage)
		{
			Dictionary<Guid, Rauta> salida = new Dictionary<Guid, Rauta>();
			DBTopoStorage? auxTopo = await mvarSerializer.GetTopoStorage(topoStorage.Header.Id);
			if (null == auxTopo) return salida;
			List<DBRauta> entrada = await mvarSerializer.GetRautatie(auxTopo.Id);
			foreach (DBRauta auxRauta in entrada)
			{
				Header? auxCabecera = await DeserializeHeader(auxRauta.HeaderId);
				if (null != auxCabecera)
				{
					Rauta nuevoRauta = new Rauta(topoStorage);
					nuevoRauta.Header = auxCabecera;
					nuevoRauta.mvarParent = topoStorage;
					//Cargamos los planes del rauta
					List<DBPlan> planes = await mvarSerializer.GetPlans(auxRauta.Id);
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
						List<DBCirculationBlock> blocks = await mvarSerializer.GetCirculationBlocks(auxPlan.Id);
							//mvarContext.CirculationBlocks.Where(x => x.PlanId == auxPlan.Id).ToListAsync();
						foreach (DBCirculationBlock block in blocks)
						{
							if (topoStorage.mcolAsimilations.ContainsKey(block.AsimilationId))
							{
								CirculationBlock nuevoBlock = new CirculationBlock();
								nuevoBlock.asimilation = topoStorage.mcolAsimilations[block.AsimilationId];
								nuevoBlock.weekdayMask = block.WeekdayMask;
								nuevoBlock.pattern = block.Pattern;
								nuevoPlan.CirculationBlocks.Add(nuevoBlock);
								List<DBCirculation> circulacionesEnBloque = await mvarSerializer.GetCirculations(block.Id);
								foreach (DBCirculation auxCirculation in circulacionesEnBloque)
								{
									Circulation nuevaCirculation = new Circulation(nuevoBlock);
									nuevaCirculation.departure = auxCirculation.Departure;
									nuevaCirculation.color[0] = auxCirculation.Color0;
									nuevaCirculation.color[1] = auxCirculation.Color1;
									nuevaCirculation.comment = auxCirculation.Comment;
									nuevaCirculation.name = auxCirculation.Name;
									nuevoBlock.mcolCirculations.Add(nuevaCirculation);
								}
							}
						}

						List<DBSchedule> schedules = await mvarSerializer.GetSchedules(auxPlan.Id);
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
							List<DBScheduleUnit> unidades = await mvarSerializer.GetScheduleUnits(schedule.Id);
							foreach (DBScheduleUnit unidad in unidades)
							{
								if (unidad.CirculationId < 0) //Turno de depósito
								{
									TimeLapse lapso = new TimeLapse { Begin = unidad.Begin, End = unidad.End };
									ScheduleItem nuevoItem = new ScheduleItem(lapso, unidad.Active);
									nuevoSchedule.mcolItems.Add(nuevoItem);
								}
								else //Tracción o acompañamiento
								{
									DBCirculation? auxCirculation = await mvarSerializer.GetCirculation(unidad.CirculationId);
									//System.Diagnostics.Debug.Assert(null != auxCirculation);
									if (null != auxCirculation)
									{
										Circulation? auxTNCirculation = nuevoPlan.getCirculationById(auxCirculation.Name);
										if (null != auxTNCirculation)
										{
											ScheduleItem nuevoItem = new ScheduleItem(auxTNCirculation, unidad.Active);
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
		internal async Task<Dictionary<Guid, TopoStorage>> DeserializeTopoStorages()
		{
			Dictionary<Guid, TopoStorage> salida = new Dictionary<Guid, TopoStorage>();
			List<DBTopoStorage> entrada = await mvarSerializer.GetTopoStorages();
			foreach (DBTopoStorage auxEntrada in entrada)
			{
				TopoStorage? nuevo = await DeserializeTopoStorage(auxEntrada.HeaderId);
				if (null != nuevo)
					salida.Add(auxEntrada.HeaderId, nuevo);
			}
			return salida;
		}
		internal async Task<TopoStorage?> DeserializeTopoStorage(Guid id)
		{
			DBTopoStorage? auxTopoStorage = await mvarSerializer.GetTopoStorage(id);	
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
			IEnumerable<DBAxis> auxAxises = await mvarSerializer.GetAxises(auxTopoStorage.Id);
			foreach (DBAxis auxAxis in auxAxises)
			{
				Axis nuevoAxis = new Axis();
				nuevoAxis.id = auxAxis.AxisId;
				nuevoAxis.name = auxAxis.Name;
				nuevoAxis.comment = auxAxis.Comment;
				nuevoAxis.mvarColor[0] = auxAxis.Color0 ?? "black";
				nuevoAxis.mvarColor[1] = auxAxis.Color1 ?? "white";
				Dictionary<long, Station> auxCacheStations = new Dictionary<long, Station>();
				//Estaciones del eje
				IEnumerable<DBStation> auxStations = await mvarSerializer.GetStations(auxAxis.Id);
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
				IEnumerable<DBRefPunctual> auxPunctuals = await mvarSerializer.GetRefPunctuals(auxAxis.Id);
				foreach (DBRefPunctual auxPunctual in auxPunctuals)
				{
					if (auxCacheStations.ContainsKey(auxPunctual.Pk))
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
			IEnumerable<DBAsimilation> auxAsimilations = await mvarSerializer.GetAsimilations(auxTopoStorage.Id);
			foreach (DBAsimilation auxAsimilation in auxAsimilations)
			{
				if (auxAllStationsCache.ContainsKey(auxAsimilation.OriginStationId))
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
					IEnumerable<DBAsimilationStep> auxAsimilationSteps = await mvarSerializer.GetAsimilationSteps(auxAsimilation.Id);
					foreach (DBAsimilationStep auxAsimilationStep in auxAsimilationSteps)
					{
						if (auxAllStationsCache.ContainsKey(auxAsimilationStep.DestinationStationId) &&
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
		internal async Task RemoveTopoStorage(Guid id){await mvarSerializer.RemoveTopoStorage(id);}

		internal async Task SerializeTopoStorage(TopoStorage rhs)
		{
			await RemoveTopoStorage(rhs.Header.Id);
			DBTopoStorage nuevo = new DBTopoStorage();
			SerializeHeader(rhs.Header);
			nuevo.HeaderId = rhs.Header.Id;
			mvarSerializer.Add(nuevo);
			await mvarSerializer.SaveChangesAsync();

			Dictionary<Station, int> auxColStations = new Dictionary<Station, int>();
			Dictionary<Axis, int> auxColAxis = new Dictionary<Axis, int>();
			foreach (Axis eje in rhs.mcolAxis.Values)
			{
				DBAxis nuevoEje = new DBAxis();
				nuevoEje.AxisId = eje.id;
				nuevoEje.StorageId = nuevo.Id;
				nuevoEje.Name = eje.name;
				nuevoEje.Comment = eje.comment;
				nuevoEje.Color0 = eje.mvarColor[0];
				nuevoEje.Color1 = eje.mvarColor[1];
				mvarSerializer.Add(nuevoEje);
				await mvarSerializer.SaveChangesAsync();
				auxColAxis.Add(eje, nuevoEje.Id);
				//Estaciones
				foreach (Station estacion in eje.Stations)
				{
					DBStation nuevaEstacion = new DBStation();
					nuevaEstacion.StationId = estacion.id;
					nuevaEstacion.AxisId = nuevoEje.Id;
					nuevaEstacion.Pk = estacion.pk;
					nuevaEstacion.Name = estacion.name;
					nuevaEstacion.ShortName = estacion.shortName;
					mvarSerializer.Add(nuevaEstacion);
					await mvarSerializer.SaveChangesAsync();
					auxColStations.Add(estacion, nuevaEstacion.Id);
				}
				//Referencias puntuales
				foreach (RefPunctual punto in eje.mcolPoints)
				{
					DBRefPunctual nuevoPunto = new DBRefPunctual();
					nuevoPunto.AxisId = nuevoEje.Id;
					nuevoPunto.Pk = punto.pk;
					nuevoPunto.Latitude = punto.point.Latitude;
					nuevoPunto.Longitude = punto.point.Longitude;
					mvarSerializer.Add(nuevoPunto);
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
				await mvarSerializer.SaveChangesAsync();
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
				mvarSerializer.Add(nuevaAsimilacion);
				await mvarSerializer.SaveChangesAsync();
				foreach (AsimilationStep paso in asimilacion.mcolSteps)
				{
					DBAsimilationStep nuevoPaso = new DBAsimilationStep();
					nuevoPaso.AsimilationId = nuevaAsimilacion.Id;
					nuevoPaso.DestinationStationId = auxColStations[paso.destination];
					nuevoPaso.AxisId = auxColAxis[paso.destination.axis];
					nuevoPaso.tripTime = paso.tripTime;
					nuevoPaso.stopTime = paso.stopTime;
					mvarSerializer.Add(nuevoPaso);
				}
				await mvarSerializer.SaveChangesAsync();
			}
		}
		#endregion TopoStorage


	}
}

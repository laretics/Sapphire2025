using System.Xml.Linq;
using TimeNet2026.Models;
using TimeNet2026.DBStorage;
using TimeNet2026.ScriptCompiling;
using TimeNet2026.Timed;
using TimeNet2026.Topo;
using TimeNet2026Data;
using TimeNet2026Data.DBStorage;
using TimeNet2026Data.Serialization;
using System.Diagnostics;

namespace TimeNet2026.Storage
{
	public class OnyxStorage
	{
		private Dictionary<Guid,TopoStorage> mcolTopoStorages;

		public OnyxStorage()
		{
			mcolTopoStorages = new Dictionary<Guid, TopoStorage>();
		}
		public async Task EmptyDatabase(ITimeNetContextStorage context) 
		{
			DBSerializer auxSerializer = new DBSerializer(context);
			await auxSerializer.ClearDatabase();
		}
	
		public Dictionary<Guid,TopoStorage> Storages { get => mcolTopoStorages; }

		/// <summary>
		/// Carga el nodo que viene y deserializa automáticamente lo que contenga.
		/// </summary>
		/// <param name="root"></param>
		public CompileResult ImportFromXML(XNode root)
		{
			CompileResult result = new CompileResult();
			result.Success = false;
			if (root is XElement element)
			{
				switch (element.Name.LocalName)
				{
					case "layout":
						return ImportTopoFromXML(element);
					case "rautatie":
						return ImportRautaFromXML(element);
				}								
				result.Message = string.Format("Node named {0} is not a topoStorage and not a rautatie database.", element.Name.LocalName);
				return result;
			}
			result.Message = "This XML is not a valid node containing data. Please check input file.";
			return result;
		}
		/// <summary>
		/// Vamos a usar esto para exportar un TopoStorage completo a XML.
		/// Esta función se usa para cargar datos en el tren desde el servidor Zafiro.
		/// </summary>
		/// <returns></returns>
		public string ExportToXML()
		{
			//TODO: Implementar esta función.
			return string.Empty; 
		}
		internal CompileResult ImportTopoFromXML(XElement root)
		{
			XMLCompiler compilador = new XMLCompiler();
			TopoStorage? nuevo = compilador.CompileTopoStorage(root);
			if(null!=nuevo && compilador.Result.Success)
			{
				if (mcolTopoStorages.ContainsKey(nuevo.Header.Id))
					mcolTopoStorages[nuevo.Header.Id] = nuevo;
				else
					mcolTopoStorages.Add(nuevo.Header.Id, nuevo);
			}
			return compilador.Result;
		}
		internal CompileResult ImportRautaFromXML(XElement root)
		{
			XMLCompiler compilador = new XMLCompiler();
			CompileResult result = new CompileResult();
			result.Success = false;
			Guid auxId = compilador.TopoStorageIdByRauta(root);			
			if(Guid.Empty!=auxId && mcolTopoStorages.ContainsKey(auxId))
			{
				TopoStorage auxTopoStorage = mcolTopoStorages[auxId];
				compilador.CompileRauta(auxTopoStorage, root);
				return compilador.Result;
			}
			else
			{
				result.Message = string.Format("Rauta with id {0} has no any installed compatible topoStorage on the database.", auxId);
				return result;
			}
		}
		/// <summary>
		/// Almacena el contenido de la estructura en la base de datos.
		/// </summary>
		/// <param name="context"></param>
		public async Task SerializeMemory(ITimeNetContextStorage context)
		{
			foreach (TopoStorage storage in mcolTopoStorages.Values)
				await SerializeTopoStorage(storage, context);
		}
		/// <summary>
		/// Carga toda la estructura TimeNet desde la base de datos.
		/// </summary>
		/// <param name="context"></param>
		public async Task DeserializeMemory(ITimeNetContextStorage context)
		{
			mcolTopoStorages = await DeserializeTopoStorages(context);
			foreach(TopoStorage auxTopo in mcolTopoStorages.Values)
			{
				IEnumerable<Rauta> rautatie = await DeserializeRautatie(auxTopo, context);
				foreach (Rauta ra in rautatie)
					auxTopo.InstallRauta(ra);
			}
		}

		#region Header
		internal void SerializeHeader(Header rhs, ITimeNetContextStorage context)
		{
			RemoveHeader(rhs.Id,context); //Elimino cualquier etiqueta anterior.
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
			DBSerializer auxSerializer = new DBSerializer(context);
			auxSerializer.Add(auxHeader);			
		}
		internal void RemoveHeader(Guid id, ITimeNetContextStorage context) 
		{
			DBSerializer auxSerializer = new DBSerializer(context);
			auxSerializer.RemoveHeader(id);
		}
		internal async Task<Header?> DeserializeHeader(Guid id, ITimeNetContextStorage context)
		{
			DBSerializer auxSerializer = new DBSerializer(context);
			DBHeader? xx = await auxSerializer.GetHeader(id);
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
		internal async Task RemoveRautatie(int topoStorageId, ITimeNetContextStorage context)
		{
			DBSerializer auxSerializer = new DBSerializer(context);
			await auxSerializer.RemoveRauta(topoStorageId);
		}		
		public async Task SerializeRautatie(TopoStorage topoStorage, ITimeNetContextStorage context)
		{
			//Elimino rautatie existentes:
			DBSerializer auxSerializer = new DBSerializer(context);
			foreach (Rauta auxRauta in topoStorage.Rautatie)
				await auxSerializer.RemoveRauta(auxRauta.Header.Id);

			//Antes de empezar tengo que obtener el registro del TopoStorage en la base de datos.
			DBTopoStorage? auxDBTopoStorage = await auxSerializer.GetTopoStorage(topoStorage.Header.Id);
			if (null != auxDBTopoStorage)
			{
				foreach (Rauta auxRauta in topoStorage.Rautatie)
				{
					DBRauta nuevoRauta = new DBRauta();
					nuevoRauta.HeaderId = auxRauta.Header.Id;
					nuevoRauta.TopoStorageId = auxDBTopoStorage.Id;
					auxSerializer.Add(nuevoRauta);
					SerializeHeader(auxRauta.Header, context);
					await auxSerializer.SaveChangesAsync();
					foreach (Plan auxPlan in auxRauta.Plans.Values)
					{
						DBPlan nuevoPlan = new DBPlan();
						nuevoPlan.RautaId = nuevoRauta.Id;
						nuevoPlan.PlanId = auxPlan.Id;
						nuevoPlan.Name = auxPlan.Name;
						nuevoPlan.Comment = auxPlan.Comment;
						nuevoPlan.Color0 = auxPlan.mvarColor[0] ?? "black";
						nuevoPlan.Color1 = auxPlan.mvarColor[1] ?? "white";
						auxSerializer.Add(nuevoPlan);
						await auxSerializer.SaveChangesAsync();
						foreach (CirculationBlock auxBlock in auxPlan.AllCirculationBlocks)
						{
							if (null != auxBlock.asimilation)
							{
								DBCirculationBlock nuevoBlock = new DBCirculationBlock();
								nuevoBlock.PlanId = nuevoPlan.Id;
								nuevoBlock.AsimilationId = auxBlock.asimilation.id;
								nuevoBlock.WeekdayMask = (byte)auxBlock.weekdayMask;
								nuevoBlock.Pattern = auxBlock.pattern;
								auxSerializer.Add(nuevoBlock);
								await auxSerializer.SaveChangesAsync();
								foreach (Circulation auxCircula in auxBlock.Circulations)
								{
									DBCirculation nuevaCircula = new DBCirculation();
									nuevaCircula.BlockId = nuevoBlock.Id;
									nuevaCircula.Name = auxCircula.name;
									nuevaCircula.Departure = auxCircula.departure;
									nuevaCircula.Comment = auxCircula.comment;
									nuevaCircula.Color0 = auxCircula.color[0] ?? "black";
									nuevaCircula.Color1 = auxCircula.color[1] ?? "white";
									auxSerializer.Add(nuevaCircula);
								}
							}
						}
						await auxSerializer.SaveChangesAsync();

						foreach (Schedule auxSchedule in auxPlan.AllSchedules)
						{
							DBSchedule nuevoSchedule = new DBSchedule();
							nuevoSchedule.PlanId = nuevoPlan.Id;
							nuevoSchedule.Name = auxSchedule.NameCloudString;
							nuevoSchedule.Comment = auxSchedule.Comment;
							nuevoSchedule.WeekdayMask = (byte)auxSchedule.weekdayMask;
							nuevoSchedule.Color1 = auxSchedule.Color[0] ?? "black";
							nuevoSchedule.Color2 = auxSchedule.Color[1] ?? "white";
							nuevoSchedule.CoordinateX = auxSchedule.Coordinates[0];
							nuevoSchedule.CoordinateY = auxSchedule.Coordinates[1];
							auxSerializer.Add(nuevoSchedule);
							await auxSerializer.SaveChangesAsync();
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
									IEnumerable<DBCirculationBlock> listaCirculaciones = await auxSerializer.GetCirculationBlocks(nuevoPlan.Id);
									foreach (DBCirculationBlock bloque in listaCirculaciones)
									{
										List<DBCirculation> auxCirculacionesEnBloque = await auxSerializer.GetCirculations(bloque.Id);
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

								auxSerializer.Add(nuevoUnit);
							}
							await auxSerializer.SaveChangesAsync();
						}
					}
				}
			}
		}
		internal async Task<IEnumerable<Rauta>> DeserializeRautatie(TopoStorage topoStorage, ITimeNetContextStorage context)
		{
			List<Rauta> salida = new List<Rauta>();
			DBSerializer auxSerializer = new DBSerializer(context);
			DBTopoStorage? auxTopo = await auxSerializer.GetTopoStorage(topoStorage.Header.Id);
			if (null == auxTopo) return salida;
			List<DBRauta> entrada = await auxSerializer.GetRautatie(auxTopo.Id);
			foreach (DBRauta auxRauta in entrada)
			{
				Header? auxCabecera = await DeserializeHeader(auxRauta.HeaderId,context);
				if (null != auxCabecera)
				{
					Rauta nuevoRauta = new Rauta(topoStorage);
					nuevoRauta.Header = auxCabecera;
					nuevoRauta.mvarParent = topoStorage;
					//Cargamos los planes del rauta
					List<DBPlan> planes = await auxSerializer.GetPlans(auxRauta.Id);
					foreach (DBPlan auxPlan in planes)
					{
						Plan nuevoPlan = new Plan(topoStorage);
						nuevoPlan.Id = auxPlan.PlanId;
						nuevoPlan.Name = auxPlan.Name;
						nuevoPlan.Comment = auxPlan.Comment;
						nuevoPlan.mvarColor[0] = auxPlan.Color0 ?? "black";
						nuevoPlan.mvarColor[1] = auxPlan.Color1 ?? "white";
						nuevoPlan.TopoId = topoStorage.Header.Id;
						//Cargamos las circulaciones del plan.
						List<DBCirculationBlock> blocks = await auxSerializer.GetCirculationBlocks(auxPlan.Id);
							//mvarContext.CirculationBlocks.Where(x => x.PlanId == auxPlan.Id).ToListAsync();
						foreach (DBCirculationBlock block in blocks)
						{
							if (topoStorage.mcolAsimilations.ContainsKey(block.AsimilationId))
							{
								CirculationBlock nuevoBlock = new CirculationBlock();
								nuevoBlock.asimilation = topoStorage.mcolAsimilations[block.AsimilationId];
								nuevoBlock.weekdayMask = (Weekday)block.WeekdayMask;
								nuevoBlock.pattern = block.Pattern;
								nuevoPlan.AllCirculationBlocks.Add(nuevoBlock);
								List<DBCirculation> circulacionesEnBloque = await auxSerializer.GetCirculations(block.Id);
								foreach (DBCirculation auxCirculation in circulacionesEnBloque)
								{
									Circulation nuevaCirculation = new Circulation(nuevoBlock);
									nuevaCirculation.departure = auxCirculation.Departure;
									nuevaCirculation.color[0] = auxCirculation.Color0;
									nuevaCirculation.color[1] = auxCirculation.Color1;
									nuevaCirculation.comment = auxCirculation.Comment;
									nuevaCirculation.name = auxCirculation.Name;
									nuevoBlock.Circulations.Add(nuevaCirculation);
								}
							}
						}

						List<DBSchedule> schedules = await auxSerializer.GetSchedules(auxPlan.Id);
						foreach (DBSchedule schedule in schedules)
						{
							Schedule nuevoSchedule = new Schedule();
							nuevoSchedule.Name = schedule.Name;
							nuevoSchedule.Comment = schedule.Comment;
							nuevoSchedule.weekdayMask = (Weekday)schedule.WeekdayMask;
							nuevoSchedule.Color[0] = schedule.Color1 ?? "black";
							nuevoSchedule.Color[1] = schedule.Color2 ?? "white";
							nuevoSchedule.Coordinates[0] = schedule.CoordinateX;
							nuevoSchedule.Coordinates[1] = schedule.CoordinateY;
							//Cargamos las unidades del horario.
							List<DBScheduleUnit> unidades = await auxSerializer.GetScheduleUnits(schedule.Id);
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
									DBCirculation? auxCirculation = await auxSerializer.GetCirculation(unidad.CirculationId);
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
					salida.Add(nuevoRauta);
				}
			}
			return salida;
		}
		#endregion Rautatie

		#region TopoStorage
		internal async Task<Dictionary<Guid, TopoStorage>> DeserializeTopoStorages(ITimeNetContextStorage context)
		{
			DBSerializer auxSerializer = new DBSerializer(context);
			Dictionary<Guid, TopoStorage> salida = new Dictionary<Guid, TopoStorage>();
			List<DBTopoStorage> entrada = await auxSerializer.GetTopoStorages();
			foreach (DBTopoStorage auxEntrada in entrada)
			{
				TopoStorage? nuevo = await DeserializeTopoStorage(auxEntrada.HeaderId,context);
				if (null != nuevo)
					salida.Add(auxEntrada.HeaderId, nuevo);
			}
			return salida;
		}
		/// <summary>
		/// Obtiene la lista de los headers de los TopoStorage almacenados en la base de datos.
		/// Usaremos esta lista para mostrar en el cliente Zafiro el contenido de TopoStorages
		/// </summary>
		/// <param name="context"></param>
		/// <returns></returns>
		public async Task<IEnumerable<TopoStorageHeaderModel>> DeserializeTopoStoragesHeaders(ITimeNetContextStorage context)
		{
			List<TopoStorageHeaderModel> salida = new List<TopoStorageHeaderModel>();
			DBSerializer auxSerializer = new DBSerializer(context);
			List<DBTopoStorage> auxColStorages = await auxSerializer.GetTopoStorages();
			foreach (DBTopoStorage auxStorage in  auxColStorages)
			{
				Header? auxHeader = await DeserializeHeader(auxStorage.HeaderId,context);
				if(null!=auxHeader)
				{
					TopoStorageHeaderModel cabecera = new TopoStorageHeaderModel();
					cabecera.header = auxHeader;
					List<DBRauta> auxRautatie = await auxSerializer.GetRautatie(auxStorage.Id);
					List<Header> auxColRautatieHeader = new List<Header>();
					foreach(DBRauta auxRauta in auxRautatie)
					{
						//Aquí me quedé... tengo que iterar por los rautas para obtener sus cabeceras.
						auxHeader = await DeserializeHeader(auxRauta.HeaderId,context);
						if (null != auxHeader)
							auxColRautatieHeader.Add(auxHeader);
					}
					cabecera.rautatie = auxColRautatieHeader;
					salida.Add(cabecera);
				}
			}
			return salida;
		}
		public async Task<TopoStorage?> DeserializeTopoStorage(Guid id, ITimeNetContextStorage context)
		{
			DBSerializer auxSerializer = new DBSerializer(context);
			DBTopoStorage? auxTopoStorage = await auxSerializer.GetTopoStorage(id);	
			if (null == auxTopoStorage) return null;
			TopoStorage salida = new TopoStorage();
			//Cargamos todos los elementos del TopoStorage afectado.
			//Carga del Header.
			Header? auxHeader = await DeserializeHeader(id,context);
			if (null == auxHeader) return null;
			salida.Header = auxHeader;
			//Carga de los ejes.
			Dictionary<int, Station> auxAllStationsCache = new Dictionary<int, Station>();
			Dictionary<int, Axis> auxAllAxisCache = new Dictionary<int, Axis>();
			IEnumerable<DBAxis> auxAxises = await auxSerializer.GetAxises(auxTopoStorage.Id);
			foreach (DBAxis auxAxis in auxAxises)
			{
				Axis nuevoAxis = new Axis();
				nuevoAxis.id = auxAxis.AxisId;
				nuevoAxis.Name = auxAxis.Name;
				nuevoAxis.Comment = auxAxis.Comment;
				nuevoAxis.mvarColor[0] = auxAxis.Color0 ?? "black";
				nuevoAxis.mvarColor[1] = auxAxis.Color1 ?? "white";
				Dictionary<long, Station> auxCacheStations = new Dictionary<long, Station>();
				//Estaciones del eje
				IEnumerable<DBStation> auxStations = await auxSerializer.GetStations(auxAxis.Id);
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
				IEnumerable<DBRefPunctual> auxPunctuals = await auxSerializer.GetRefPunctuals(auxAxis.Id);
				nuevoAxis.Topology = new TopoAxis();
				foreach (DBRefPunctual auxPunctual in auxPunctuals)
				{
					if (auxCacheStations.ContainsKey(auxPunctual.Pk))
					{
						Station auxxStation = auxCacheStations[auxPunctual.Pk];
						auxxStation.point = new GeoLocation(auxPunctual.Latitude, auxPunctual.Longitude);
						nuevoAxis.Topology.Points.Add(auxxStation);
						nuevoAxis.Stations.Add(auxxStation);
					}
					else
					{
						RefPunctual nuevoPunctual = new RefPunctual(auxPunctual.Latitude, auxPunctual.Longitude);
						nuevoPunctual.pk = auxPunctual.Pk;
						nuevoAxis.Topology.Points.Add(nuevoPunctual);
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
			IEnumerable<DBAsimilation> auxAsimilations = await auxSerializer.GetAsimilations(auxTopoStorage.Id);
			foreach (DBAsimilation auxAsimilation in auxAsimilations)
			{
				if (auxAllStationsCache.ContainsKey(auxAsimilation.OriginStationId))
				{
					Asimilation nuevaAsimilation = new Asimilation(salida);
					nuevaAsimilation.id = auxAsimilation.AsimilationId;
					nuevaAsimilation.Name = auxAsimilation.Name;
					nuevaAsimilation.Comment = auxAsimilation.Comment;
					nuevaAsimilation.color[0] = auxAsimilation.Color0 ?? "white";
					nuevaAsimilation.color[1] = auxAsimilation.Color1 ?? "black";
					nuevaAsimilation.MaxSpeed = auxAsimilation.MaxSpeed;
					nuevaAsimilation.Origin = auxAllStationsCache[auxAsimilation.OriginStationId];
					//Carga de los pasos de cada asimilación
					IEnumerable<DBAsimilationStep> auxAsimilationSteps = await auxSerializer.GetAsimilationSteps(auxAsimilation.Id);
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
		public async Task<CompileResult> RemoveTopoStorage(Guid id, ITimeNetContextStorage context)
		{
			DBSerializer auxSerializer = new DBSerializer(context);
			CompileResult salida = new CompileResult();
			salida.Success = true;
			try
			{
				DBTopoStorage? auxStorage = await auxSerializer.GetTopoStorage(id);
				if (null == auxStorage)
				{
					salida.Success = false;
					salida.Message = "Couldn't find this TopoStorage in Database.";
					salida.Warnings.Add(new CompileWarning(string.Format("TopoStorage id {0} not found on database.", id), -1, CompileWarning.SeverityEnum.Error));
				}
				else
				{
					DBHeader? auxHeader = await auxSerializer.GetHeader(id);
					List<DBRauta> auxRautatie = await auxSerializer.GetRautatie(auxStorage.Id);
					if (auxRautatie.Count > 0)
					{
						Debug.Assert(null != auxHeader);
						salida.Success = false;
						salida.Message = string.Format(
							"TopoStorage {0} can't be deleted. There are {1} rautatie based on it. Please, delete them prior to this deletion.",
							auxHeader.Name, auxRautatie.Count);
						foreach (DBRauta auxRauta in auxRautatie)
						{
							auxHeader = await auxSerializer.GetHeader(auxRauta.HeaderId);
							if (null != auxHeader)
								salida.Warnings.Add(new CompileWarning(string.Format("Rauta id {0} and name {1} must be deleted.", auxRauta.HeaderId, auxHeader.Name), -1, CompileWarning.SeverityEnum.Warning));
						}
					}
					else
					{
						await auxSerializer.RemoveTopoStorage(id);
					}
				}
			}
			catch (Exception ex)
			{
				salida.Success = false;
				salida.Message = string.Format("Unhandled exception trying to delete a TopoStorage: {0}", ex.Message);
			}			
			return salida;
		}

		/// <summary>
		/// Almacena un TopoStorage completo en una base de datos.
		/// </summary>
		/// <param name="rhs">El TopoStorage que queremos guardar</param>
		/// <param name="context">Entorno de serialización</param>
		internal async Task SerializeTopoStorage(TopoStorage rhs, ITimeNetContextStorage context)
		{
			await RemoveTopoStorage(rhs.Header.Id,context);
			DBTopoStorage nuevo = new DBTopoStorage();
			SerializeHeader(rhs.Header,context);
			nuevo.HeaderId = rhs.Header.Id;
			DBSerializer auxSerializer = new DBSerializer(context);
			auxSerializer.Add(nuevo);
			await auxSerializer.SaveChangesAsync();

			Dictionary<Station, int> auxColStations = new Dictionary<Station, int>();
			Dictionary<Axis, int> auxColAxis = new Dictionary<Axis, int>();
			foreach (Axis eje in rhs.mcolAxis.Values)
			{
				DBAxis nuevoEje = new DBAxis();
				nuevoEje.AxisId = eje.id;
				nuevoEje.StorageId = nuevo.Id;
				nuevoEje.Name = eje.Name;
				nuevoEje.Comment = eje.Comment;
				nuevoEje.Color0 = eje.mvarColor[0];
				nuevoEje.Color1 = eje.mvarColor[1];
				auxSerializer.Add(nuevoEje);
				await auxSerializer.SaveChangesAsync();
				auxColAxis.Add(eje, nuevoEje.Id);
				//Estaciones
				foreach (Station estacion in eje.Stations)
				{
					DBStation nuevaEstacion = new DBStation();
					nuevaEstacion.StationId = estacion.Id;
					nuevaEstacion.AxisId = nuevoEje.Id;
					nuevaEstacion.Pk = estacion.pk;
					nuevaEstacion.Name = estacion.Name;
					nuevaEstacion.ShortName = estacion.ShortName;
					auxSerializer.Add(nuevaEstacion);
					await auxSerializer.SaveChangesAsync();
					auxColStations.Add(estacion, nuevaEstacion.Id);
				}
				//Referencias puntuales
				if(null!=eje.Topology)
				{
					foreach (RefPunctual punto in eje.Topology.Points)
					{
						DBRefPunctual nuevoPunto = new DBRefPunctual();
						nuevoPunto.AxisId = nuevoEje.Id;
						nuevoPunto.Pk = punto.pk;
						nuevoPunto.Latitude = punto.point.Latitude;
						nuevoPunto.Longitude = punto.point.Longitude;
						auxSerializer.Add(nuevoPunto);
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
				await auxSerializer.SaveChangesAsync();
			}
			foreach (Asimilation asimilacion in rhs.ColAsimilations.Values)
			{
				DBAsimilation nuevaAsimilacion = new DBAsimilation();
				nuevaAsimilacion.TopoStorageId = nuevo.Id;
				nuevaAsimilacion.AsimilationId = asimilacion.id;
				nuevaAsimilacion.Name = asimilacion.Name;
				nuevaAsimilacion.Comment = asimilacion.Comment;
				nuevaAsimilacion.Color0 = asimilacion.mvarColor[0];
				nuevaAsimilacion.Color1 = asimilacion.mvarColor[1];
				nuevaAsimilacion.MaxSpeed = asimilacion.MaxSpeed;
				System.Diagnostics.Debug.Assert(null != asimilacion.Origin);
				nuevaAsimilacion.OriginStationId = auxColStations[asimilacion.Origin];
				auxSerializer.Add(nuevaAsimilacion);
				await auxSerializer.SaveChangesAsync();
				foreach (AsimilationStep paso in asimilacion.mcolSteps)
				{
					DBAsimilationStep nuevoPaso = new DBAsimilationStep();
					nuevoPaso.AsimilationId = nuevaAsimilacion.Id;
					nuevoPaso.DestinationStationId = auxColStations[paso.destination];
					nuevoPaso.AxisId = auxColAxis[paso.destination.axis];
					nuevoPaso.tripTime = paso.tripTime;
					nuevoPaso.stopTime = paso.stopTime;
					auxSerializer.Add(nuevoPaso);
				}
				await auxSerializer.SaveChangesAsync();
			}
		}
		#endregion TopoStorage


	}
}

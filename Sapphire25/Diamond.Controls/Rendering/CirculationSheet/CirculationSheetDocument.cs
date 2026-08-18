using System.Globalization;
using Diamond.Basis;
using Diamond.Motion;
using Diamond.Timed;
using Diamond.Topo;
using ProjectCirculation = Diamond.Project.Circulation;
using ProjectTimedCall = Diamond.Project.TimedCall;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Documento de ficha de marcha: cabecera + fronteras (estaciones y cambios de V) + páginas.
	/// </summary>
	public sealed class CirculationSheetDocument
	{
		private readonly Circulation mvarCirculation;
		private readonly string mvarTrainNumber;
		private readonly string mvarTrainTitle;
		private readonly string mvarRelation;
		private readonly string mvarMaterialType;
		private readonly string mvarLocationLine;
		private readonly string mvarRouteTitle;
		private readonly string mvarRouteLine;
		private readonly string mvarMarchId;
		private readonly string mvarEditionLabel;
		private readonly string mvarServiceDaysLabel;
		private readonly IReadOnlyList<CirculationSheetFrontier> mcolFrontiers;
		private readonly IReadOnlyList<CirculationSheetPage> mcolPages;
		private readonly int mvarMaxFrontiersPerPage;

		private CirculationSheetDocument(
			Circulation circulation,
			string trainNumber,
			string trainTitle,
			string relation,
			string materialType,
			string locationLine,
			string routeTitle,
			string routeLine,
			string marchId,
			string editionLabel,
			string serviceDaysLabel,
			IReadOnlyList<CirculationSheetFrontier> frontiers,
			IReadOnlyList<CirculationSheetPage> pages,
			int maxFrontiersPerPage)
		{
			mvarCirculation = circulation;
			mvarTrainNumber = trainNumber;
			mvarTrainTitle = trainTitle;
			mvarRelation = relation;
			mvarMaterialType = materialType;
			mvarLocationLine = locationLine;
			mvarRouteTitle = routeTitle ?? string.Empty;
			mvarRouteLine = routeLine ?? string.Empty;
			mvarMarchId = marchId;
			mvarEditionLabel = editionLabel;
			mvarServiceDaysLabel = serviceDaysLabel ?? string.Empty;
			mcolFrontiers = frontiers;
			mcolPages = pages;
			mvarMaxFrontiersPerPage = maxFrontiersPerPage;
		}

		public Circulation Circulation
		{
			get { return mvarCirculation; }
		}

		public string TrainNumber
		{
			get { return mvarTrainNumber; }
		}

		public string TrainTitle
		{
			get { return mvarTrainTitle; }
		}

		public string Relation
		{
			get { return mvarRelation; }
		}

		/// <summary>
		/// Etiqueta de cabecera derecha: techo de velocidad del material (p. ej. "Tipo 100").
		/// En todas las hojas de la marcha.
		/// </summary>
		public string MaterialType
		{
			get { return mvarMaterialType; }
		}

		/// <summary>
		/// Línea Loc. (locomotora / material): nombre, aceleración y deceleración.
		/// Solo 1.ª hoja.
		/// </summary>
		public string LocationLine
		{
			get { return mvarLocationLine; }
		}

		/// <summary>Recorrido en claro (p. ej. «Palma - Sa Pobla»). 1.ª hoja.</summary>
		public string RouteTitle
		{
			get { return mvarRouteTitle; }
		}

		/// <summary>PK / firma de la vista (antes al inicio de la línea única). 1.ª hoja.</summary>
		public string RouteLine
		{
			get { return mvarRouteLine; }
		}

		public string MarchId
		{
			get { return mvarMarchId; }
		}

		public string EditionLabel
		{
			get { return mvarEditionLabel; }
		}

		/// <summary>
		/// Días de circulación para la cabecera (Laborables, Fines de semana, lunes, L, M, X…).
		/// </summary>
		public string ServiceDaysLabel
		{
			get { return mvarServiceDaysLabel; }
		}

		public IReadOnlyList<CirculationSheetFrontier> Frontiers
		{
			get { return mcolFrontiers; }
		}

		public IReadOnlyList<CirculationSheetPage> Pages
		{
			get { return mcolPages; }
		}

		public int MaxFrontiersPerPage
		{
			get { return mvarMaxFrontiersPerPage; }
		}

		public static CirculationSheetDocument Build(
			Circulation circulation,
			int maxFrontiersPerPage = CirculationSheetPager.DefaultMaxFrontiersPerPage,
			string? editionLabel = null,
			ServiceDays? serviceDays = null,
			bool includeTemporaryLimits = false)
		{
			return Build(
				circulation,
				mesh: null,
				maxFrontiersPerPage,
				editionLabel,
				serviceDays,
				includeTemporaryLimits: includeTemporaryLimits);
		}

		/// <param name="mesh">
		/// Malla opcional para rellenar la columna de cruces (trenes en sentido opuesto
		/// con los que se cruza en el camino).
		/// </param>
		/// <param name="serviceDays">
		/// Días de circulación del tren (demanda o unión multi-día del plan de explotación).
		/// </param>
		/// <summary>
		/// Ficha del tren activo en cabina: circulación de proyecto + topología viva.
		/// Los horarios de parada salen del proyecto publicado; el resto se interpola.
		/// </summary>
		public static CirculationSheetDocument BuildFromProject(
			ProjectCirculation circulation,
			TopoLayout topo,
			int maxFrontiersPerPage = CirculationSheetPager.DefaultMaxFrontiersPerPage,
			string? editionLabel = null,
			ServiceDays? serviceDays = null,
			TrainSpecs? specs = null,
			bool includeTemporaryLimits = false)
		{
			if (circulation is null)
			{
				throw new ArgumentNullException(nameof(circulation));
			}

			if (topo is null)
			{
				throw new ArgumentNullException(nameof(topo));
			}

			Circulation timed = CabinCirculationHydrator.ToTimed(circulation, topo, specs);
			return Build(
				timed,
				mesh: null,
				maxFrontiersPerPage,
				editionLabel,
				serviceDays,
				scheduledTimes: circulation,
				includeTemporaryLimits: includeTemporaryLimits);
		}

		public static CirculationSheetDocument Build(
			Circulation circulation,
			Mesh? mesh,
			int maxFrontiersPerPage = CirculationSheetPager.DefaultMaxFrontiersPerPage,
			string? editionLabel = null,
			ServiceDays? serviceDays = null,
			ProjectCirculation? scheduledTimes = null,
			bool includeTemporaryLimits = false)
		{
			if (circulation is null)
			{
				throw new ArgumentNullException(nameof(circulation));
			}

			Asimilation asim = circulation.Asimilation;
			RouteView view = asim.View;
			long originPk = asim.Origin.PK;
			long destPk = asim.Destination.PK;
			bool increasing = originPk <= destPk;

			string originName = FormatStationName(asim.Origin.Station);
			string destName = FormatStationName(asim.Destination.Station);

			// Solo número de servicio de plantilla (4923…); no Id técnico ni DemandId/asim.
			string trainNumber = circulation.HasServiceNumber
				? circulation.ServiceNumber
				: string.Empty;
			string trainTitle = string.Empty;
			string relation = originName + " a " + destName;
			// Loc. = locomotora/material. Recorrido y PK de vista en dos líneas.
			// Tipo = techo de velocidad del material en todas las hojas ("Tipo 100").
			string locationLine = FormatLocomotiveLine(asim.Specs);
			string routeTitle = FormatRouteTitle(originName, destName);
			string routeLine = FormatRoutePkLine(view);
			string material = FormatMaterialTypeLabel(asim.Specs);
			// No exponer Id técnico de planificación (C12-R-T3…).
			string marchId = string.Empty;
			string edition = editionLabel
				?? ("Zafiro · " + DateTime.Now.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));

			// Días: parámetro → día de la malla filtrada (un solo día).
			string daysLabel = string.Empty;
			if (serviceDays is not null)
			{
				daysLabel = serviceDays.FormatCirculationLabel();
			}
			else if (mesh is not null && mesh.PlanningDay.HasValue)
			{
				daysLabel = ServiceDays.FromDayOfWeekMask(mesh.PlanningDay.Value).FormatCirculationLabel();
			}

			bool ascending = TrainNumbering.IsNetworkAscendingForNumbering(circulation);
			List<CirculationSheetFrontier> frontiers = BuildFrontiers(
				circulation,
				asim,
				view,
				originPk,
				destPk,
				increasing,
				scheduledTimes,
				includeTemporaryLimits,
				ascending);
			if (mesh is not null)
			{
				frontiers = AttachCrossings(frontiers, circulation, view, mesh);
			}

			IReadOnlyList<CirculationSheetPage> pages = CirculationSheetPager.Paginate(frontiers, maxFrontiersPerPage);

			return new CirculationSheetDocument(
				circulation,
				trainNumber,
				trainTitle,
				relation,
				material,
				locationLine,
				routeTitle,
				routeLine,
				marchId,
				edition,
				daysLabel,
				frontiers,
				pages,
				maxFrontiersPerPage);
		}

		/// <summary>
		/// Loc. (locomotora): nombre del material + aceleración y freno redondeados.
		/// </summary>
		public static string FormatLocomotiveLine(TrainSpecs? specs)
		{
			if (specs is null)
			{
				return "Loc. —";
			}

			string name = !string.IsNullOrWhiteSpace(specs.Name)
				? specs.Name.Trim()
				: (specs.Id ?? string.Empty).Trim();
			if (name.Length == 0)
			{
				name = "—";
			}

			string a = RoundKinematic(specs.Acceleration);
			string b = RoundKinematic(specs.ServiceBrake);
			return "Loc. " + name + "  a " + a + "  b " + b;
		}

		/// <summary>Nombre del recorrido: «Palma - Sa Pobla».</summary>
		public static string FormatRouteTitle(string originName, string destName)
		{
			string o = string.IsNullOrWhiteSpace(originName) ? "—" : originName.Trim();
			string d = string.IsNullOrWhiteSpace(destName) ? "—" : destName.Trim();
			return o + " - " + d;
		}

		/// <summary>
		/// PK / firma de la vista (el texto que antes iba al comienzo de la línea única).
		/// </summary>
		public static string FormatRoutePkLine(RouteView view)
		{
			if (view is null || string.IsNullOrWhiteSpace(view.Id))
			{
				return "—";
			}

			return view.Id.Trim();
		}

		/// <summary>Compat: «vista.- relación» en una sola línea.</summary>
		public static string FormatRouteLine(RouteView view, string relation)
		{
			string pk = FormatRoutePkLine(view);
			string rel = string.IsNullOrWhiteSpace(relation)
				? "—"
				: relation.Trim();
			return pk + ".- " + rel;
		}

		/// <summary>Cabecera derecha: "Tipo 100" con vmax del material redondeado a entero.</summary>
		public static string FormatMaterialTypeLabel(TrainSpecs? specs)
		{
			if (specs is null || specs.MaxSpeedKmh <= 0.0)
			{
				return string.Empty;
			}

			int vmax = (int)Math.Round(specs.MaxSpeedKmh, MidpointRounding.AwayFromZero);
			if (vmax < 1)
			{
				return string.Empty;
			}

			return "Tipo " + vmax.ToString(CultureInfo.InvariantCulture);
		}

		private static string RoundKinematic(double metersPerSecondSquared)
		{
			// Una cifra decimal basta para legibilidad en ficha (0.85 → 0.9, 0.8 → 0.8).
			double r = Math.Round(metersPerSecondSquared, 1, MidpointRounding.AwayFromZero);
			return r.ToString("0.0", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Asocia a cada frontera los números de tren con los que hay cruce
		/// (sentidos físicos opuestos, trayectorias que se cruzan en espacio-tiempo).
		/// </summary>
		private static List<CirculationSheetFrontier> AttachCrossings(
			List<CirculationSheetFrontier> frontiers,
			Circulation self,
			RouteView displayView,
			Mesh mesh)
		{
			if (frontiers.Count == 0)
			{
				return frontiers;
			}

			// Por índice de frontera: conjunto de números de tren.
			List<List<string>> bags = new List<List<string>>(frontiers.Count);
			int bi = 0;
			while (bi < frontiers.Count)
			{
				bags.Add(new List<string>());
				bi++;
			}

			int ci = 0;
			while (ci < mesh.Circulations.Count)
			{
				Circulation other = mesh.Circulations[ci];
				ci++;
				if (string.Equals(other.TechnicalId, self.TechnicalId, StringComparison.Ordinal))
				{
					continue;
				}

				if (!MeshCantonGeometry.ArePhysicallyOpposite(self.Asimilation, other.Asimilation))
				{
					continue;
				}

				long meetPk;
				if (!TryFindMeetingRoutePk(self, other, displayView, out meetPk))
				{
					continue;
				}

				int nearest = FindNearestFrontierIndex(frontiers, meetPk);
				if (nearest < 0)
				{
					continue;
				}

				// Solo números de servicio asignados (plantilla); no Ids técnicos.
				if (!other.HasServiceNumber)
				{
					continue;
				}

				string num = other.ServiceNumber;

				// Evitar duplicados en la misma casilla.
				List<string> bag = bags[nearest];
				bool exists = false;
				int k = 0;
				while (k < bag.Count)
				{
					if (string.Equals(bag[k], num, StringComparison.Ordinal))
					{
						exists = true;
						break;
					}

					k++;
				}

				if (!exists)
				{
					bag.Add(num);
				}
			}

			List<CirculationSheetFrontier> result = new List<CirculationSheetFrontier>(frontiers.Count);
			int i = 0;
			while (i < frontiers.Count)
			{
				string label = string.Join(" ", bags[i]);
				result.Add(frontiers[i].WithCrossingTrains(label));
				i++;
			}

			return result;
		}

		/// <summary>
		/// Busca el PK de ruta (en <paramref name="displayView"/>) donde se cruzan
		/// dos circulaciones de sentido físico opuesto, si sus ventanas temporales se solapan.
		/// </summary>
		private static bool TryFindMeetingRoutePk(
			Circulation self,
			Circulation other,
			RouteView displayView,
			out long meetPk)
		{
			meetPk = 0;
			double a0 = self.Departure.TotalSeconds;
			double a1 = self.Arrival.TotalSeconds;
			double b0 = other.Departure.TotalSeconds;
			double b1 = other.Arrival.TotalSeconds;
			double t0 = Math.Max(a0, b0);
			double t1 = Math.Min(a1, b1);
			if (t1 <= t0 + 1.0)
			{
				return false;
			}

			const int steps = 80;
			bool hasPrev = false;
			double prevDiff = 0.0;
			long prevPkS = 0;
			long prevPkO = 0;
			int s = 0;
			while (s <= steps)
			{
				double t = t0 + (t1 - t0) * ((double)s / steps);
				long pkS;
				long pkO;
				if (!TryPositionOnView(self, displayView, t, out pkS)
					|| !TryPositionOnView(other, displayView, t, out pkO))
				{
					hasPrev = false;
					s++;
					continue;
				}

				double diff = pkS - pkO;
				if (hasPrev)
				{
					bool crossed = (prevDiff < 0.0 && diff >= 0.0) || (prevDiff > 0.0 && diff <= 0.0);
					bool coincided = Math.Abs(diff) <= 50.0; // ~50 m
					if (crossed || coincided)
					{
						// Interpolar PK de encuentro.
						if (crossed && Math.Abs(prevDiff - diff) > 1e-9)
						{
							double u = prevDiff / (prevDiff - diff);
							if (u < 0.0)
							{
								u = 0.0;
							}

							if (u > 1.0)
							{
								u = 1.0;
							}

							meetPk = (long)Math.Round(prevPkS + u * (pkS - prevPkS));
						}
						else
						{
							meetPk = (pkS + pkO) / 2;
						}

						return true;
					}
				}

				prevDiff = diff;
				prevPkS = pkS;
				prevPkO = pkO;
				hasPrev = true;
				s++;
			}

			return false;
		}

		private static bool TryPositionOnView(
			Circulation c,
			RouteView displayView,
			double absoluteSeconds,
			out long routePk)
		{
			routePk = 0;
			double rel = absoluteSeconds - c.Departure.TotalSeconds;
			if (rel < -1.0 || rel > c.Asimilation.TotalTime.TotalSeconds + 1.0)
			{
				return false;
			}

			if (rel < 0.0)
			{
				rel = 0.0;
			}

			double trip = c.Asimilation.TotalTime.TotalSeconds;
			if (rel > trip)
			{
				rel = trip;
			}

			long asimPk = c.Asimilation.PKByTime(TimeSpan.FromSeconds(rel));
			return displayView.TryMapRoutePkFrom(c.Asimilation.View, asimPk, out routePk);
		}

		private static int FindNearestFrontierIndex(List<CirculationSheetFrontier> frontiers, long pk)
		{
			int best = -1;
			long bestDist = long.MaxValue;
			int i = 0;
			while (i < frontiers.Count)
			{
				long d = Math.Abs(frontiers[i].RoutePk - pk);
				if (d < bestDist)
				{
					bestDist = d;
					best = i;
				}

				i++;
			}

			return best;
		}

		private static List<CirculationSheetFrontier> BuildFrontiers(
			Circulation circulation,
			Asimilation asim,
			RouteView view,
			long originPk,
			long destPk,
			bool increasing,
			ProjectCirculation? scheduledTimes,
			bool includeTemporaryLimits,
			bool ascending)
		{
			// —— Paradas comerciales (dwell) ——
			HashSet<long> commercialPk = new HashSet<long>();
			Dictionary<long, TimeSpan> dwellByPk = new Dictionary<long, TimeSpan>();
			int si = 0;
			while (si < asim.Stops.Count)
			{
				AsimilationStop stop = asim.Stops[si];
				if (stop.Dwell > TimeSpan.Zero)
				{
					commercialPk.Add(stop.PK);
					dwellByPk[stop.PK] = stop.Dwell;
				}

				si++;
			}

			// Origen y destino siempre “paran” a efectos de ficha.
			commercialPk.Add(originPk);
			commercialPk.Add(destPk);

			// —— Estaciones por PK ——
			Dictionary<long, StationOnRoute> stationByPk = new Dictionary<long, StationOnRoute>();
			int vi = 0;
			while (vi < view.Stations.Count)
			{
				StationOnRoute st = view.Stations[vi];
				if (IsOnPath(st.PK, originPk, destPk, increasing))
				{
					stationByPk[st.PK] = st;
				}

				vi++;
			}

			// Garantizar extremos.
			EnsureStationMap(stationByPk, asim.Origin.Station, originPk, view);
			EnsureStationMap(stationByPk, asim.Destination.Station, destPk, view);

			// —— PKs singulares: estaciones + frontiers de velocidad ——
			SortedSet<long> asc = new SortedSet<long>();
			asc.Add(originPk);
			asc.Add(destPk);
			foreach (long pk in stationByPk.Keys)
			{
				asc.Add(pk);
			}

			CollectSpeedFrontiersOnPath(
				view, originPk, destPk, increasing, asc, includeTemporaryLimits, ascending);

			List<long> orderedPk = new List<long>(asc);
			if (!increasing)
			{
				orderedPk.Sort(static (a, b) => b.CompareTo(a));
			}

			// —— Construir filas con datos de tramo saliente ——
			List<CirculationSheetFrontier> rows = new List<CirculationSheetFrontier>(orderedPk.Count);
			int i = 0;
			while (i < orderedPk.Count)
			{
				long pk = orderedPk[i];
				bool isOrigin = pk == originPk;
				bool isDest = pk == destPk;

				StationOnRoute? st;
				stationByPk.TryGetValue(pk, out st);

				// Eje físico y PK de eje (columna PK, nombre en cambios de V, divisiones).
				// En vistas multi-eje y trenes descendentes el PK de ruta crece con el
				// recorrido, pero en ficha debe mostrarse el PK del eje (igual en impares
				// y pares).
				string axisId = string.Empty;
				long displayPk = pk;
				if (view.TryMapRouteToAxis(pk, out Axis? axis, out long axisPk) && axis is not null)
				{
					axisId = axis.Id ?? string.Empty;
					displayPk = axisPk;
				}
				else if (st is not null)
				{
					displayPk = st.AxisPk;
					if (st.Leg is not null && st.Leg.Axis is not null)
					{
						axisId = st.Leg.Axis.Id ?? string.Empty;
					}
				}

				CirculationSheetMarkKind kind;
				string depName;
				if (st is not null)
				{
					bool principal = StationClassification.IsPrincipalStation(st.Station);
					kind = principal
						? CirculationSheetMarkKind.PrincipalStation
						: CirculationSheetMarkKind.Halt;
					depName = FormatDependencyLabel(st.Station, principal);
				}
				else
				{
					// Frontera de V en plena vía: mismo PK de eje que la columna PK.
					kind = CirculationSheetMarkKind.SpeedLimitChange;
					depName = "PK " + FormatStationKm(displayPk);
				}

				bool outgoingTemp = false;
				bool outgoingUnsignaled = false;
				string tempReason = string.Empty;
				string tempObs = string.Empty;
				TemporaryLimitTrack? tempTrack = null;
				if (includeTemporaryLimits && i < orderedPk.Count - 1)
				{
					long samplePk = MidPk(pk, orderedPk[i + 1]);
					int tracks = view.GetTrackCountAt(samplePk);
					tempTrack = TemporaryLimitSheetFilter.TrackForTrain(ascending, tracks);
					TemporarySpeedLimit? gov = view.FindGoverningTemporary(samplePk, tempTrack);
					if (gov is not null)
					{
						outgoingTemp = true;
						outgoingUnsignaled = !gov.SignaledOnTrack;
						if (kind == CirculationSheetMarkKind.SpeedLimitChange)
						{
							tempReason = TemporaryLimitReasonText.Label(gov.Reason);
							tempObs = gov.Observations ?? string.Empty;
						}
					}
				}

				bool commercial = commercialPk.Contains(pk) && st is not null;
				TimeSpan dwell = TimeSpan.Zero;
				if (commercial)
				{
					if (isOrigin || isDest)
					{
						dwell = TimeSpan.Zero; // origen/destino: Com vacío (hora de salida/llegada)
						// En destino a veces se muestra 0; el usuario pidió minutos solo si para.
						// Origen no “para” en el sentido comercial de la columna Com.
						if (isDest)
						{
							commercial = false;
						}

						if (isOrigin)
						{
							commercial = false;
						}
					}
					else if (dwellByPk.TryGetValue(pk, out TimeSpan d))
					{
						dwell = d;
					}
				}

				// Si hay dwell en stops, commercial true.
				if (!isOrigin && !isDest && dwellByPk.TryGetValue(pk, out TimeSpan d2) && d2 > TimeSpan.Zero)
				{
					commercial = true;
					dwell = d2;
				}

				ResolveTimes(circulation, asim, pk, isOrigin, isDest, dwell, scheduledTimes, out TimeSpan? arr, out TimeSpan? dep);

				// Tramo saliente hacia la siguiente frontera.
				int? outTracks = null;
				int? outVmax = null;
				TimeSpan? granted = null;
				if (i < orderedPk.Count - 1)
				{
					long nextPk = orderedPk[i + 1];
					long samplePk = MidPk(pk, nextPk);
					outTracks = view.GetTrackCountAt(samplePk);
					if (!tempTrack.HasValue && includeTemporaryLimits)
					{
						tempTrack = TemporaryLimitSheetFilter.TrackForTrain(ascending, outTracks.Value);
					}

					outVmax = view.GetSpeedLimitForSheet(samplePk, includeTemporaryLimits, tempTrack);
					if (!outVmax.HasValue && asim.Specs is not null)
					{
						outVmax = (int)Math.Round(asim.Specs.MaxSpeedKmh);
					}

					// Tiempo de reloj en este punto (salida si para) y en el siguiente (llegada si para).
					TimeSpan? tHere = commercial || isOrigin
						? (dep ?? arr)
						: (arr ?? dep);
					if (isOrigin)
					{
						tHere = dep ?? circulation.Departure;
					}

					ResolveTimes(circulation, asim, nextPk, nextPk == originPk, nextPk == destPk,
						dwellByPk.TryGetValue(nextPk, out TimeSpan nd) ? nd : TimeSpan.Zero,
						scheduledTimes,
						out TimeSpan? nextArr, out TimeSpan? nextDep);

					bool nextCommercial = dwellByPk.ContainsKey(nextPk) || nextPk == destPk;
					TimeSpan? tNext = nextCommercial || nextPk == destPk
						? (nextArr ?? nextDep)
						: (nextDep ?? nextArr);

					if (tHere.HasValue && tNext.HasValue && tNext.Value >= tHere.Value)
					{
						granted = tNext.Value - tHere.Value;
					}
				}

				rows.Add(new CirculationSheetFrontier(
					routePk: pk,
					stationKm: FormatStationKm(displayPk),
					dependencyName: depName,
					markKind: kind,
					isOrigin: isOrigin,
					isDestination: isDest,
					isCommercialStop: commercial && dwell > TimeSpan.Zero,
					dwell: dwell,
					arrival: arr,
					departure: dep,
					outgoingTrackCount: outTracks,
					outgoingVmaxKmh: outVmax,
					grantedToNext: granted,
					axisId: axisId,
					outgoingIsTemporary: outgoingTemp,
					temporaryReasonLabel: tempReason,
					temporaryObservations: tempObs,
					outgoingTemporaryUnsignaled: outgoingUnsignaled));

				i++;
			}

			return rows;
		}

		private static void ResolveTimes(
			Circulation circulation,
			Asimilation asim,
			long pk,
			bool isOrigin,
			bool isDest,
			TimeSpan dwell,
			ProjectCirculation? scheduledTimes,
			out TimeSpan? arrAbs,
			out TimeSpan? depAbs)
		{
			arrAbs = null;
			depAbs = null;
			if (TryResolveScheduledTimes(scheduledTimes, pk, isOrigin, isDest, circulation.Departure, out arrAbs, out depAbs))
			{
				return;
			}

			if (isOrigin)
			{
				depAbs = circulation.Departure;
				arrAbs = circulation.Departure;
				return;
			}

			TimeSpan? relDep = asim.TimeDepartByPK(pk);
			TimeSpan? relArr = asim.TimeArriveByPK(pk);
			if (relDep.HasValue)
			{
				depAbs = circulation.Departure + relDep.Value;
			}

			if (relArr.HasValue)
			{
				arrAbs = circulation.Departure + relArr.Value;
			}
			else if (depAbs.HasValue && dwell > TimeSpan.Zero)
			{
				arrAbs = depAbs.Value - dwell;
				if (arrAbs < circulation.Departure)
				{
					arrAbs = circulation.Departure;
				}
			}

			if (isDest && arrAbs is null && depAbs is not null)
			{
				arrAbs = depAbs;
			}
		}

		/// <summary>
		/// Horarios oficiales del proyecto publicado: parada exacta o interpolación entre llamadas.
		/// </summary>
		private static bool TryResolveScheduledTimes(
			ProjectCirculation? scheduledTimes,
			long pk,
			bool isOrigin,
			bool isDest,
			TimeSpan trainDeparture,
			out TimeSpan? arrAbs,
			out TimeSpan? depAbs)
		{
			arrAbs = null;
			depAbs = null;
			if (scheduledTimes is null || scheduledTimes.Calls.Count == 0)
			{
				return false;
			}

			if (isOrigin)
			{
				arrAbs = trainDeparture;
				depAbs = trainDeparture;
				return true;
			}

			int i = 0;
			while (i < scheduledTimes.Calls.Count)
			{
				ProjectTimedCall call = scheduledTimes.Calls[i];
				if (call.Pk == pk)
				{
					arrAbs = call.Arrival;
					depAbs = call.Departure;
					return true;
				}

				i++;
			}

			// Interpolación entre llamadas colindantes (fronteras de V en plena vía).
			ProjectTimedCall first = scheduledTimes.Calls[0];
			ProjectTimedCall last = scheduledTimes.Calls[scheduledTimes.Calls.Count - 1];
			bool increasing = first.Pk <= last.Pk;
			int prev = -1;
			int next = -1;
			int k = 0;
			while (k < scheduledTimes.Calls.Count)
			{
				long cpk = scheduledTimes.Calls[k].Pk;
				if (increasing)
				{
					if (cpk <= pk)
					{
						prev = k;
					}

					if (cpk >= pk && next < 0)
					{
						next = k;
					}
				}
				else
				{
					if (cpk >= pk)
					{
						prev = k;
					}

					if (cpk <= pk && next < 0)
					{
						next = k;
					}
				}

				k++;
			}

			if (prev < 0 || next < 0 || prev == next)
			{
				if (isDest)
				{
					arrAbs = last.Arrival;
					depAbs = last.Departure;
					return true;
				}

				return false;
			}

			long pk0 = scheduledTimes.Calls[prev].Pk;
			long pk1 = scheduledTimes.Calls[next].Pk;
			long span = pk1 - pk0;
			if (span == 0)
			{
				return false;
			}

			double u = (pk - pk0) / (double)span;
			if (u < 0.0)
			{
				u = 0.0;
			}

			if (u > 1.0)
			{
				u = 1.0;
			}

			TimeSpan t0 = scheduledTimes.Calls[prev].Departure;
			TimeSpan t1 = scheduledTimes.Calls[next].Arrival;
			long ticks = t0.Ticks + (long)Math.Round((t1.Ticks - t0.Ticks) * u);
			if (ticks < 0)
			{
				ticks = 0;
			}

			TimeSpan t = TimeSpan.FromTicks(ticks);
			arrAbs = t;
			depAbs = t;
			return true;
		}

		private static long MidPk(long a, long b)
		{
			// Punto interior del tramo (evita caer exactamente en el extremo).
			if (a == b)
			{
				return a;
			}

			long mid = a + ((b - a) / 2);
			if (mid == a)
			{
				mid = a < b ? a + 1 : a - 1;
			}

			return mid;
		}

		private static void CollectSpeedFrontiersOnPath(
			RouteView view,
			long originPk,
			long destPk,
			bool increasing,
			SortedSet<long> set,
			bool includeTemporary,
			bool ascending)
		{
			long lo = Math.Min(originPk, destPk);
			long hi = Math.Max(originPk, destPk);
			int li = 0;
			while (li < view.Legs.Count)
			{
				RouteLeg leg = view.Legs[li];
				CollectSpeedMap(view, leg.Axis, leg.Axis.FixedLimits, lo, hi, set);
				if (includeTemporary)
				{
					CollectApplicableTemporaryFrontiers(view, leg.Axis, lo, hi, set, ascending);
					CollectTrackCountChangeFrontiers(view, leg.Axis, lo, hi, set);
				}

				CollectSpeedMap(view, leg.Axis, leg.Axis.SessionLimits, lo, hi, set);
				li++;
			}
		}

		private static void CollectApplicableTemporaryFrontiers(
			RouteView view,
			Axis axis,
			long pkMin,
			long pkMax,
			SortedSet<long> set,
			bool ascending)
		{
			int i = 0;
			while (i < axis.TemporaryLimitRecords.Count)
			{
				TemporarySpeedLimit limit = axis.TemporaryLimitRecords[i];
				i++;
				if (!TemporaryAppliesOnPath(limit, view, axis, pkMin, pkMax, ascending))
				{
					continue;
				}

				AddMappedAxisPk(view, axis, limit.PK, pkMin, pkMax, set);
				AddMappedAxisPk(view, axis, limit.PKEnd, pkMin, pkMax, set);
			}
		}

		private static bool TemporaryAppliesOnPath(
			TemporarySpeedLimit limit,
			RouteView view,
			Axis axis,
			long routeLo,
			long routeHi,
			bool ascending)
		{
			long a0 = limit.PK < limit.PKEnd ? limit.PK : limit.PKEnd;
			long a1 = limit.PK > limit.PKEnd ? limit.PK : limit.PKEnd;
			if (a1 <= a0)
			{
				return false;
			}

			List<long> samples = new List<long>();
			samples.Add(a0);
			samples.Add(a0 + ((a1 - a0) / 2));
			samples.Add(a1 - 1);
			AddSpanCuts(axis.TrackSpans, a0, a1, samples);
			AddSpanCuts(axis.SessionTrackSpans, a0, a1, samples);

			int i = 0;
			while (i < samples.Count)
			{
				long axisPk = samples[i];
				i++;
				long routePk;
				if (!view.TryMapAxisToRoute(axis, axisPk, out routePk))
				{
					continue;
				}

				if (routePk < routeLo || routePk > routeHi)
				{
					continue;
				}

				int tracks = axis.GetTrackCountAt(axisPk);
				if (TemporaryLimitSheetFilter.Applies(limit, ascending, tracks))
				{
					return true;
				}
			}

			return false;
		}

		private static void CollectTrackCountChangeFrontiers(
			RouteView view,
			Axis axis,
			long pkMin,
			long pkMax,
			SortedSet<long> set)
		{
			AddSpanCutsMapped(view, axis, axis.TrackSpans, pkMin, pkMax, set);
			AddSpanCutsMapped(view, axis, axis.SessionTrackSpans, pkMin, pkMax, set);
		}

		private static void AddSpanCuts(
			IReadOnlyList<TrackSpan> spans,
			long axisLo,
			long axisHi,
			List<long> samples)
		{
			int i = 0;
			while (i < spans.Count)
			{
				TrackSpan span = spans[i];
				i++;
				if (span.Pk0 >= axisLo && span.Pk0 < axisHi)
				{
					samples.Add(span.Pk0);
				}

				if (span.Pkf > axisLo && span.Pkf <= axisHi)
				{
					samples.Add(span.Pkf);
					if (span.Pkf - 1 >= axisLo)
					{
						samples.Add(span.Pkf - 1);
					}
				}
			}
		}

		private static void AddSpanCutsMapped(
			RouteView view,
			Axis axis,
			IReadOnlyList<TrackSpan> spans,
			long pkMin,
			long pkMax,
			SortedSet<long> set)
		{
			int i = 0;
			while (i < spans.Count)
			{
				TrackSpan span = spans[i];
				i++;
				AddMappedAxisPk(view, axis, span.Pk0, pkMin, pkMax, set);
				AddMappedAxisPk(view, axis, span.Pkf, pkMin, pkMax, set);
			}
		}

		private static void AddMappedAxisPk(
			RouteView view,
			Axis axis,
			long axisPk,
			long pkMin,
			long pkMax,
			SortedSet<long> set)
		{
			long routePk;
			if (view.TryMapAxisToRoute(axis, axisPk, out routePk)
				&& routePk >= pkMin
				&& routePk <= pkMax)
			{
				set.Add(routePk);
			}
		}

		private static void CollectSpeedMap(
			RouteView view,
			Axis axis,
			SpeedLimitMap map,
			long pkMin,
			long pkMax,
			SortedSet<long> set)
		{
			if (map is null || map.SpeedCount == 0)
			{
				return;
			}

			foreach (KeyValuePair<int, AxisVectorFlex> pair in map.BySpeed)
			{
				IReadOnlyList<Punctual<long, LongAxis>> frontiers = pair.Value.Frontiers();
				int fi = 0;
				while (fi < frontiers.Count)
				{
					long axisPk = frontiers[fi].PK;
					long routePk;
					if (view.TryMapAxisToRoute(axis, axisPk, out routePk)
						&& routePk >= pkMin
						&& routePk <= pkMax)
					{
						set.Add(routePk);
					}

					fi++;
				}
			}
		}

		private static void EnsureStationMap(
			Dictionary<long, StationOnRoute> map,
			Station station,
			long pk,
			RouteView view)
		{
			if (map.ContainsKey(pk))
			{
				return;
			}

			RouteLeg? leg = null;
			long axisPk = pk;
			int li = 0;
			while (li < view.Legs.Count)
			{
				RouteLeg candidate = view.Legs[li];
				long a = Math.Min(candidate.RoutePk0, candidate.RoutePkEnd);
				long b = Math.Max(candidate.RoutePk0, candidate.RoutePkEnd);
				if (pk >= a && pk <= b)
				{
					leg = candidate;
					if (!view.TryMapRouteToAxis(pk, out Axis? axis, out axisPk) || axis is null)
					{
						axisPk = pk;
					}

					break;
				}

				li++;
			}

			if (leg is null && view.Legs.Count > 0)
			{
				leg = view.Legs[0];
			}

			if (leg is null)
			{
				return;
			}

			map[pk] = new StationOnRoute(station, pk, leg, axisPk);
		}

		private static bool IsOnPath(long pk, long originPk, long destPk, bool increasing)
		{
			if (increasing)
			{
				return pk >= originPk && pk <= destPk;
			}

			return pk <= originPk && pk >= destPk;
		}

		private static string FormatStationName(Station station)
		{
			if (!string.IsNullOrWhiteSpace(station.Name))
			{
				return station.Name.Trim();
			}

			if (!string.IsNullOrWhiteSpace(station.Avr))
			{
				return station.Avr.Trim();
			}

			return station.Id;
		}

		private static string FormatDependencyLabel(Station station, bool principal)
		{
			string name = FormatStationName(station).ToUpperInvariant();
			if (!principal)
			{
				if (name.IndexOf("(APD)", StringComparison.OrdinalIgnoreCase) < 0
					&& name.IndexOf("APD", StringComparison.OrdinalIgnoreCase) < 0)
				{
					name = name + " (APD)";
				}
			}

			return name;
		}

		public static string FormatStationKm(long pkMeters)
		{
			double km = pkMeters / 1000.0;
			return km.ToString("0.0", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Hora de ficha: HH.mm (hora a dos dígitos) y, si hay media, sufijo ½.
		/// En el dibujo de la columna Hora el ½ se pinta aparte y más pequeño.
		/// </summary>
		public static string FormatSheetTime(TimeSpan? ts)
		{
			string main = FormatSheetTime(ts, out string half);
			if (half.Length == 0)
			{
				return main;
			}

			return main + half;
		}

		/// <summary>
		/// Hora de ficha partida: cuerpo HH.mm (justificable a la izquierda) y
		/// <paramref name="halfSuffix"/> = "½" si 15–44 s de segundo, vacío si no.
		/// </summary>
		public static string FormatSheetTime(TimeSpan? ts, out string halfSuffix)
		{
			halfSuffix = string.Empty;
			if (!ts.HasValue)
			{
				return string.Empty;
			}

			TimeSpan t = ts.Value;
			if (t < TimeSpan.Zero)
			{
				t = TimeSpan.Zero;
			}

			int h = (int)t.TotalHours;
			int m = t.Minutes;
			int sec = t.Seconds;
			if (sec >= 45)
			{
				m++;
				if (m >= 60)
				{
					m = 0;
					h++;
				}

				return FormatHourMinute(h, m);
			}

			if (sec >= 15)
			{
				halfSuffix = "½";
				return FormatHourMinute(h, m);
			}

			return FormatHourMinute(h, m);
		}

		private static string FormatHourMinute(int h, int m)
		{
			return h.ToString("D2", CultureInfo.InvariantCulture)
				+ "." + m.ToString("D2", CultureInfo.InvariantCulture);
		}

		/// <summary>Tiempo concedido en minutos y medios minutos (½).</summary>
		public static string FormatGrantedMinutes(TimeSpan? ts)
		{
			if (!ts.HasValue || ts.Value <= TimeSpan.Zero)
			{
				return string.Empty;
			}

			double mins = ts.Value.TotalMinutes;
			double halfUnits = Math.Round(mins * 2.0, MidpointRounding.AwayFromZero);
			if (halfUnits < 1.0 && mins > 1e-6)
			{
				halfUnits = 1.0; // mínimo ½ min si hay algo de tiempo
			}

			int whole = (int)(halfUnits / 2.0);
			bool hasHalf = (halfUnits % 2.0) >= 0.5;
			if (whole == 0 && hasHalf)
			{
				return "½";
			}

			if (hasHalf)
			{
				return whole.ToString(CultureInfo.InvariantCulture) + "½";
			}

			return whole.ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Columna Com: vacío si no para; minutos si ≥ 1 min; círculo (señal) si &lt; 1 min.
		/// </summary>
		public static string FormatCommercialDwell(TimeSpan dwell, out bool drawCircle)
		{
			drawCircle = false;
			if (dwell <= TimeSpan.Zero)
			{
				return string.Empty;
			}

			if (dwell.TotalSeconds < 60.0)
			{
				drawCircle = true;
				return string.Empty;
			}

			// Minutos enteros (redondeo al medio hacia arriba solo si ≥ 30 s de resto).
			int mins = (int)Math.Floor(dwell.TotalMinutes);
			int sec = dwell.Seconds + (dwell.Minutes == 0 && dwell.TotalMinutes >= 1 ? 0 : 0);
			sec = (int)(dwell.TotalSeconds - mins * 60);
			if (sec >= 30)
			{
				mins++;
			}

			if (mins < 1)
			{
				drawCircle = true;
				return string.Empty;
			}

			return mins.ToString(CultureInfo.InvariantCulture);
		}
	}
}

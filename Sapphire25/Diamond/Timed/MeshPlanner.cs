using System;
using System.Collections.Generic;
using Diamond.Motion;
using Diamond.Topo;

namespace Diamond.Timed
{
	/// <summary>
	/// Planificador de malla determinista.
	/// La cadencia del script es un deseo; la real la acotan cantones y vías.
	/// Warnings: desvíos de timing/cobertura. Errors: acantonamiento o cruces ilegales.
	/// </summary>
	public sealed class MeshPlanner
	{
		private static readonly TimeSpan DefaultWindowStart = new TimeSpan(6, 0, 0);
		private static readonly TimeSpan DefaultWindowEnd = new TimeSpan(22, 0, 0);
		private static readonly TimeSpan SearchStep = TimeSpan.FromSeconds(30);
		private static readonly TimeSpan Epsilon = TimeSpan.FromMilliseconds(1);

		private readonly Plan mvarPlan;

		public MeshPlanner(Plan plan)
		{
			if (plan is null)
			{
				throw new ArgumentNullException(nameof(plan));
			}

			mvarPlan = plan;
		}

		/// <summary>
		/// Genera la malla a partir de la demanda y topología del plan.
		/// </summary>
		public Mesh Solve()
		{
			Mesh mesh = new Mesh();
			TopoLayout? topo = mvarPlan.Topo;
			if (topo is null)
			{
				mesh.AddError("El plan no tiene topología (Topo).");
				return mesh;
			}

			if (mvarPlan.Demand.Count == 0)
			{
				mesh.AddWarning("No hay requisitos de demanda que planificar.");
				return mesh;
			}

			Dictionary<AsimilationKey, Asimilation> asimCache = new Dictionary<AsimilationKey, Asimilation>();
			List<ScheduledTrip> scheduled = new List<ScheduledTrip>();
			int circulationSeq = 0;

			int demandIndex = 0;
			while (demandIndex < mvarPlan.Demand.Count)
			{
				DemandRequirement demand = mvarPlan.Demand[demandIndex];
				PlanOneDemand(demand, topo, mesh, asimCache, scheduled, ref circulationSeq);
				demandIndex++;
			}

			// Verificación final dura (por si el retraso no bastó o hay interacciones cruzadas).
			ValidateHardConstraints(scheduled, mesh);

			return mesh;
		}

		private void PlanOneDemand(
			DemandRequirement demand,
			TopoLayout topo,
			Mesh mesh,
			Dictionary<AsimilationKey, Asimilation> asimCache,
			List<ScheduledTrip> scheduled,
			ref int circulationSeq)
		{
			if (!demand.IsResolved || demand.FromStation is null || demand.ToStation is null)
			{
				mesh.AddError("Requisito " + demand.Id + ": estaciones no resueltas.");
				return;
			}

			TrainSpecs specs = ResolveSpecs(demand, mesh);
			List<DirectionJob> jobs = ExpandDirections(demand);

			// Preconstruir asimilaciones por sentido (necesario para fase de cruce).
			List<PreparedDirection> prepared = new List<PreparedDirection>();
			int jobIndex = 0;
			while (jobIndex < jobs.Count)
			{
				DirectionJob job = jobs[jobIndex];
				Axis? axis;
				StationOnAxis? origin;
				StationOnAxis? destination;
				if (!TryFindAxisPath(topo, job.From, job.To, out axis, out origin, out destination))
				{
					mesh.AddError(
						"Requisito " + demand.Id + ": no hay eje común entre '"
						+ job.From.Name + "' y '" + job.To.Name + "'.");
					jobIndex++;
					continue;
				}

				List<AsimilationStop> intermediate = BuildIntermediateStops(axis!, origin!, destination!, demand.Stops);
				AsimilationKey key = new AsimilationKey(specs.Id, axis!.Id, origin!.PK, destination!.PK, intermediate);
				Asimilation asim;
				if (!asimCache.TryGetValue(key, out asim!))
				{
					asim = new Asimilation(axis, specs, origin, destination, intermediate);
					asimCache[key] = asim;
					mesh.AddAsimilation(asim);
				}

				prepared.Add(new PreparedDirection(job, axis, asim));
				jobIndex++;
			}

			TimeSpan desiredHeadway = TimeSpan.FromHours(1.0 / demand.Frequency.TrainsPerHourValue);
			TimeSpan[] phaseOffsets = ComputePhaseOffsets(demand, prepared, desiredHeadway, mesh);

			int p = 0;
			while (p < prepared.Count)
			{
				PreparedDirection prep = prepared[p];
				TimeSpan phase = p < phaseOffsets.Length ? phaseOffsets[p] : TimeSpan.Zero;
				ScheduleDemandOnPath(
					demand,
					prep.Job,
					prep.Axis,
					prep.Asimilation,
					specs,
					mesh,
					scheduled,
					ref circulationSeq,
					phase);
				p++;
			}
		}

		/// <summary>
		/// Desfase entre sentidos para cruzar en el punto pedido (p. ej. Enllaç).
		/// Índice 0 (ida) = 0; índice 1 (vuelta) = fase calculada.
		/// </summary>
		private static TimeSpan[] ComputePhaseOffsets(
			DemandRequirement demand,
			List<PreparedDirection> prepared,
			TimeSpan headway,
			Mesh mesh)
		{
			TimeSpan[] offsets = new TimeSpan[prepared.Count];
			int i = 0;
			while (i < offsets.Length)
			{
				offsets[i] = TimeSpan.Zero;
				i++;
			}

			if (prepared.Count < 2 || demand.Stops.CrossAt is null || headway <= TimeSpan.Zero)
			{
				return offsets;
			}

			StationRef crossRef = demand.Stops.CrossAt;
			PreparedDirection forward = prepared[0];
			PreparedDirection ret = prepared[1];

			StationOnAxis? crossOnAxis = FindPlacementByRef(forward.Axis, crossRef);
			if (crossOnAxis is null)
			{
				mesh.AddWarning(
					"Requisito " + demand.Id + ": no se encontró el punto de cruce '"
					+ crossRef.Text + "' en el eje; se ignora 'cross at'.");
				return offsets;
			}

			TimeSpan? tForward = forward.Asimilation.TimeByPK(crossOnAxis.PK);
			TimeSpan? tReturn = ret.Asimilation.TimeByPK(crossOnAxis.PK);
			if (!tForward.HasValue || !tReturn.HasValue)
			{
				mesh.AddWarning(
					"Requisito " + demand.Id + ": no se pudo calcular el cruce en '"
					+ crossRef.Text + "'.");
				return offsets;
			}

			// depReturn = depForward + tForward - tReturn  (mismo instante en el cruce)
			double phaseSec = (tForward.Value - tReturn.Value).TotalSeconds;
			double headSec = headway.TotalSeconds;
			phaseSec %= headSec;
			if (phaseSec < 0)
			{
				phaseSec += headSec;
			}

			offsets[1] = TimeSpan.FromSeconds(phaseSec);
			return offsets;
		}

		private static StationOnAxis? FindPlacementByRef(Axis axis, StationRef reference)
		{
			int index = 0;
			while (index < axis.Stations.Count)
			{
				StationOnAxis p = axis.Stations[index];
				if (StopPattern.Matches(reference, p.Station.Id, p.Station.Avr, p.Station.Name))
				{
					return p;
				}

				index++;
			}

			return null;
		}

		private void ScheduleDemandOnPath(
			DemandRequirement demand,
			DirectionJob job,
			Axis axis,
			Asimilation asim,
			TrainSpecs specs,
			Mesh mesh,
			List<ScheduledTrip> scheduled,
			ref int circulationSeq,
			TimeSpan phaseOffset)
		{
			TimeSpan windowStart = demand.WindowStart.HasValue
				? demand.WindowStart.Value.ToTimeSpan()
				: DefaultWindowStart;
			TimeSpan windowEnd = demand.WindowEnd.HasValue
				? demand.WindowEnd.Value.ToTimeSpan()
				: DefaultWindowEnd;

			if (windowEnd <= windowStart)
			{
				mesh.AddError("Requisito " + demand.Id + ": ventana horaria inválida.");
				return;
			}

			double desiredTph = demand.Frequency.TrainsPerHourValue;
			TimeSpan desiredHeadway = TimeSpan.FromHours(1.0 / desiredTph);
			TimeSpan minStructuralHeadway = EstimateMinHeadway(axis, asim);

			// Cadencia real no puede ser más apretada que la estructural.
			TimeSpan workingHeadway = desiredHeadway;
			if (minStructuralHeadway > workingHeadway)
			{
				workingHeadway = minStructuralHeadway;
				mesh.AddWarning(
					"Requisito " + demand.Id + " (" + job.Label + "): la cadencia deseada "
					+ FormatHeadway(desiredHeadway) + " no es viable por cantones/vías; "
					+ "se usa al menos " + FormatHeadway(workingHeadway) + ".");
			}

			int desiredCount = CountDesiredTrips(windowStart, windowEnd, desiredHeadway);
			List<TimeSpan> placedDeps = new List<TimeSpan>();
			TimeSpan cursor = windowStart + phaseOffset;
			// Si la fase saca la primera salida del inicio de ventana, ok; si se pasa del final, normalizar.
			if (cursor > windowEnd)
			{
				cursor = windowStart + TimeSpan.FromSeconds(phaseOffset.TotalSeconds % workingHeadway.TotalSeconds);
			}
			int safety = 0;
			const int maxAttempts = 10000;

			while (cursor + asim.TotalTime <= windowEnd + Epsilon && safety < maxAttempts)
			{
				safety++;
				TimeSpan dep = cursor;
				TimeSpan adjusted;
				string? hardBlock;
				if (!TryFindFeasibleDeparture(dep, windowEnd, asim, axis, scheduled, out adjusted, out hardBlock))
				{
					if (hardBlock is not null)
					{
						mesh.AddError("Requisito " + demand.Id + ": " + hardBlock);
					}

					break;
				}

				if (adjusted > windowEnd)
				{
					break;
				}

				// Si el hueco respecto al anterior supera mucho el deseado, avisar (timing blando).
				if (placedDeps.Count > 0)
				{
					TimeSpan gap = adjusted - placedDeps[placedDeps.Count - 1];
					if (gap > desiredHeadway + TimeSpan.FromMinutes(1))
					{
						mesh.AddWarning(
							"Requisito " + demand.Id + ": hueco " + FormatHeadway(gap)
							+ " mayor que el deseado " + FormatHeadway(desiredHeadway)
							+ " (salida " + FormatTime(adjusted) + ").");
					}
				}
				else if (adjusted > windowStart + TimeSpan.FromMinutes(1))
				{
					mesh.AddWarning(
						"Requisito " + demand.Id + ": primera salida retrasada a "
						+ FormatTime(adjusted) + " (ventana desde " + FormatTime(windowStart) + ").");
				}

				circulationSeq++;
				string id = "C" + circulationSeq.ToString(System.Globalization.CultureInfo.InvariantCulture)
					+ "-" + demand.Id + (job.IsReturn ? "-R" : string.Empty);

				Circulation circulation = new Circulation(id, demand.Id, asim, specs, adjusted);
				mesh.AddCirculation(circulation);
				scheduled.Add(new ScheduledTrip(circulation, axis, asim));
				placedDeps.Add(adjusted);

				cursor = adjusted + workingHeadway;
			}

			if (placedDeps.Count < desiredCount)
			{
				mesh.AddWarning(
					"Requisito " + demand.Id + " (" + job.Label + "): programados "
					+ placedDeps.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
					+ " trenes de " + desiredCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
					+ " deseados en la ventana.");
			}

			if (placedDeps.Count == 0)
			{
				mesh.AddError(
					"Requisito " + demand.Id + " (" + job.Label + "): no se pudo programar ninguna circulación factible.");
			}
		}

		private bool TryFindFeasibleDeparture(
			TimeSpan preferred,
			TimeSpan windowEnd,
			Asimilation asim,
			Axis axis,
			List<ScheduledTrip> scheduled,
			out TimeSpan feasible,
			out string? hardBlockReason)
		{
			feasible = preferred;
			hardBlockReason = null;
			TimeSpan candidate = preferred;
			int steps = 0;
			const int maxSteps = 5000;

			while (candidate <= windowEnd && steps < maxSteps)
			{
				steps++;
				string? reason;
				if (IsFeasible(candidate, asim, axis, scheduled, out reason))
				{
					feasible = candidate;
					return true;
				}

				// Solo retrasamos por conflictos duros; si reason es null, no debería ocurrir.
				candidate = candidate + SearchStep;
			}

			hardBlockReason = "no hay hueco factible en la ventana sin violar acantonamiento/cruces.";
			return false;
		}

		private static bool IsFeasible(
			TimeSpan departure,
			Asimilation candidateAsim,
			Axis axis,
			List<ScheduledTrip> scheduled,
			out string? reason)
		{
			reason = null;
			int index = 0;
			while (index < scheduled.Count)
			{
				ScheduledTrip other = scheduled[index];
				if (!string.Equals(other.Axis.Id, axis.Id, StringComparison.Ordinal))
				{
					index++;
					continue;
				}

				if (HasHardConflict(departure, candidateAsim, axis, other, out reason))
				{
					return false;
				}

				index++;
			}

			return true;
		}

		private static bool HasHardConflict(
			TimeSpan depA,
			Asimilation asimA,
			Axis axis,
			ScheduledTrip b,
			out string? reason)
		{
			reason = null;
			Asimilation asimB = b.Asimilation;
			TimeSpan depB = b.Circulation.Departure;
			bool opposite = IsOppositeSense(asimA, asimB);

			IReadOnlyList<long> frontiers = axis.CantonFrontiers;
			if (frontiers.Count < 2)
			{
				// Sin fronteras: tratar todo el solape de rutas como un solo cantón.
				long pathMin = Math.Max(
					Math.Min(asimA.Origin.PK, asimA.Destination.PK),
					Math.Min(asimB.Origin.PK, asimB.Destination.PK));
				long pathMax = Math.Min(
					Math.Max(asimA.Origin.PK, asimA.Destination.PK),
					Math.Max(asimB.Origin.PK, asimB.Destination.PK));
				if (pathMax > pathMin)
				{
					return CantonPairConflicts(
						depA, asimA, depB, asimB, axis, pathMin, pathMax, opposite, b.Circulation.Id, out reason);
				}

				return false;
			}

			int f = 0;
			while (f < frontiers.Count - 1)
			{
				long pk0 = frontiers[f];
				long pkf = frontiers[f + 1];
				if (CantonPairConflicts(
					depA, asimA, depB, asimB, axis, pk0, pkf, opposite, b.Circulation.Id, out reason))
				{
					return true;
				}

				f++;
			}

			return false;
		}

		/// <summary>
		/// Conflicto duro en un cantón [pk0,pkf): solape temporal de ocupaciones no permitido.
		/// Permitido solo si sentidos opuestos y ≥2 vías en el tramo.
		/// </summary>
		private static bool CantonPairConflicts(
			TimeSpan depA,
			Asimilation asimA,
			TimeSpan depB,
			Asimilation asimB,
			Axis axis,
			long pk0,
			long pkf,
			bool opposite,
			string otherId,
			out string? reason)
		{
			reason = null;
			if (!IntervalOverlapsPath(pk0, pkf, asimA) || !IntervalOverlapsPath(pk0, pkf, asimB))
			{
				return false;
			}

			TimeSpan? aEnter = AbsoluteEnter(depA, asimA, pk0, pkf);
			TimeSpan? aExit = AbsoluteExit(depA, asimA, pk0, pkf);
			TimeSpan? bEnter = AbsoluteEnter(depB, asimB, pk0, pkf);
			TimeSpan? bExit = AbsoluteExit(depB, asimB, pk0, pkf);
			if (!aEnter.HasValue || !aExit.HasValue || !bEnter.HasValue || !bExit.HasValue)
			{
				return false;
			}

			if (aExit.Value <= aEnter.Value || bExit.Value <= bEnter.Value)
			{
				return false;
			}

			if (!OpenIntervalsOverlap(aEnter.Value, aExit.Value, bEnter.Value, bExit.Value))
			{
				return false;
			}

			int tracks = axis.GetTrackCountAt(pk0);
			if (opposite && tracks >= 2)
			{
				// Cruce en doble vía permitido.
				return false;
			}

			string kind = opposite && tracks < 2 ? "cruce en vía única" : "acantonamiento";
			reason = "conflicto de " + kind + " en ["
				+ pk0.ToString(System.Globalization.CultureInfo.InvariantCulture)
				+ ","
				+ pkf.ToString(System.Globalization.CultureInfo.InvariantCulture)
				+ ") con " + otherId + " (salida " + FormatTime(depA) + ").";
			return true;
		}

		private static bool IsOppositeSense(Asimilation a, Asimilation b)
		{
			return a.Sense != b.Sense;
		}

		private static bool IntervalOverlapsPath(long pk0, long pkf, Asimilation asim)
		{
			long a0 = Math.Min(asim.Origin.PK, asim.Destination.PK);
			long a1 = Math.Max(asim.Origin.PK, asim.Destination.PK);
			return pk0 < a1 && pkf > a0;
		}

		private static TimeSpan? AbsoluteEnter(TimeSpan dep, Asimilation asim, long pk0, long pkf)
		{
			long pathMin = Math.Min(asim.Origin.PK, asim.Destination.PK);
			long pathMax = Math.Max(asim.Origin.PK, asim.Destination.PK);
			long c0 = Math.Max(pk0, pathMin);
			long c1 = Math.Min(pkf, pathMax);
			if (c1 <= c0)
			{
				return null;
			}

			// Increasing: entra por c0. Decreasing: entra por c1 (extremo alto del cantón).
			long enterPk = asim.Sense == CirculationSense.IncreasingPk ? c0 : c1;
			TimeSpan? rel = asim.TimeByPK(enterPk);
			if (!rel.HasValue)
			{
				return null;
			}

			return dep + rel.Value;
		}

		private static TimeSpan? AbsoluteExit(TimeSpan dep, Asimilation asim, long pk0, long pkf)
		{
			long pathMin = Math.Min(asim.Origin.PK, asim.Destination.PK);
			long pathMax = Math.Max(asim.Origin.PK, asim.Destination.PK);
			long c0 = Math.Max(pk0, pathMin);
			long c1 = Math.Min(pkf, pathMax);
			if (c1 <= c0)
			{
				return null;
			}

			// Increasing: sale por c1. Decreasing: sale por c0.
			long exitPk = asim.Sense == CirculationSense.IncreasingPk ? c1 : c0;
			TimeSpan? rel = asim.TimeByPK(exitPk);
			if (!rel.HasValue)
			{
				return null;
			}

			return dep + rel.Value;
		}

		private static bool OpenIntervalsOverlap(TimeSpan a0, TimeSpan a1, TimeSpan b0, TimeSpan b1)
		{
			// [a0,a1) ∩ [b0,b1) no vacío
			return a0 < b1 && b0 < a1;
		}

		/// <summary>
		/// Cota inferior de intervalo entre trenes del mismo patrón: mayor ocupación de un cantón en la ruta.
		/// </summary>
		private static TimeSpan EstimateMinHeadway(Axis axis, Asimilation asim)
		{
			TimeSpan maxOcc = TimeSpan.FromMinutes(2);
			IReadOnlyList<long> frontiers = axis.CantonFrontiers;
			if (frontiers.Count < 2)
			{
				// Sin cantones definidos: usar una fracción del tiempo de viaje.
				double sec = asim.TotalTime.TotalSeconds * 0.25;
				if (sec < 120.0)
				{
					sec = 120.0;
				}

				return TimeSpan.FromSeconds(sec);
			}

			int f = 0;
			while (f < frontiers.Count - 1)
			{
				long pk0 = frontiers[f];
				long pkf = frontiers[f + 1];
				if (!IntervalOverlapsPath(pk0, pkf, asim))
				{
					f++;
					continue;
				}

				TimeSpan? enter = AbsoluteEnter(TimeSpan.Zero, asim, pk0, pkf);
				TimeSpan? exit = AbsoluteExit(TimeSpan.Zero, asim, pk0, pkf);
				if (enter.HasValue && exit.HasValue && exit.Value > enter.Value)
				{
					TimeSpan occ = exit.Value - enter.Value;
					if (occ > maxOcc)
					{
						maxOcc = occ;
					}
				}

				f++;
			}

			return maxOcc;
		}

		private void ValidateHardConstraints(List<ScheduledTrip> scheduled, Mesh mesh)
		{
			int i = 0;
			while (i < scheduled.Count)
			{
				int j = i + 1;
				while (j < scheduled.Count)
				{
					ScheduledTrip a = scheduled[i];
					ScheduledTrip b = scheduled[j];
					if (string.Equals(a.Axis.Id, b.Axis.Id, StringComparison.Ordinal))
					{
						string? reason;
						if (HasHardConflict(a.Circulation.Departure, a.Asimilation, a.Axis, b, out reason)
							|| HasHardConflict(b.Circulation.Departure, b.Asimilation, b.Axis, a, out reason))
						{
							mesh.AddError(
								"Violación dura entre " + a.Circulation.Id + " y " + b.Circulation.Id
								+ (reason is null ? "." : (": " + reason)));
						}
					}

					j++;
				}

				i++;
			}
		}

		private TrainSpecs ResolveSpecs(DemandRequirement demand, Mesh mesh)
		{
			if (demand.FleetId.Length > 0)
			{
				TrainSpecs? found = mvarPlan.FindTrainSpecsById(demand.FleetId);
				if (found is not null)
				{
					return found;
				}

				mesh.AddWarning(
					"Requisito " + demand.Id + ": fleet '" + demand.FleetId
					+ "' no está en el catálogo; se usa el modelo por defecto.");
			}

			return mvarPlan.EnsureDefaultTrainSpecs();
		}

		private static List<DirectionJob> ExpandDirections(DemandRequirement demand)
		{
			List<DirectionJob> jobs = new List<DirectionJob>();
			jobs.Add(new DirectionJob(demand.FromStation!, demand.ToStation!, false, demand.From.Text + "->" + demand.To.Text));
			if (demand.Direction == DemandDirection.BothWays)
			{
				jobs.Add(new DirectionJob(demand.ToStation!, demand.FromStation!, true, demand.To.Text + "->" + demand.From.Text));
			}

			return jobs;
		}

		private static bool TryFindAxisPath(
			TopoLayout topo,
			Station from,
			Station to,
			out Axis? axis,
			out StationOnAxis? origin,
			out StationOnAxis? destination)
		{
			axis = null;
			origin = null;
			destination = null;

			// Determinista: primer eje (orden del layout) que contenga ambas estaciones.
			int axisIndex = 0;
			while (axisIndex < topo.Axes.Count)
			{
				Axis candidate = topo.Axes[axisIndex];
				StationOnAxis? o = FindPlacement(candidate, from);
				StationOnAxis? d = FindPlacement(candidate, to);
				if (o is not null && d is not null && o.PK != d.PK)
				{
					axis = candidate;
					origin = o;
					destination = d;
					return true;
				}

				axisIndex++;
			}

			return false;
		}

		private static StationOnAxis? FindPlacement(Axis axis, Station station)
		{
			int index = 0;
			while (index < axis.Stations.Count)
			{
				StationOnAxis placement = axis.Stations[index];
				if (ReferenceEquals(placement.Station, station)
					|| string.Equals(placement.Station.Id, station.Id, StringComparison.Ordinal)
					|| (placement.Station.Avr.Length > 0
						&& string.Equals(placement.Station.Avr, station.Avr, StringComparison.Ordinal)))
				{
					return placement;
				}

				index++;
			}

			return null;
		}

		/// <summary>
		/// Paradas intermedias según <see cref="StopPattern"/> del requisito.
		/// Con <c>stops Ns</c>: todas las estaciones/apeaderos del trayecto salvo skip, con dwell.
		/// Sin patrón: legacy (solo principales, dwell 0).
		/// </summary>
		private static List<AsimilationStop> BuildIntermediateStops(
			Axis axis,
			StationOnAxis origin,
			StationOnAxis destination,
			StopPattern pattern)
		{
			List<AsimilationStop> stops = new List<AsimilationStop>();
			long min = Math.Min(origin.PK, destination.PK);
			long max = Math.Max(origin.PK, destination.PK);
			bool increasing = origin.PK < destination.PK;

			List<StationOnAxis> onPath = new List<StationOnAxis>();
			int index = 0;
			while (index < axis.Stations.Count)
			{
				StationOnAxis s = axis.Stations[index];
				if (s.PK <= min || s.PK >= max)
				{
					index++;
					continue;
				}

				if (pattern.DefaultDwell.HasValue || pattern.HasExplicitPattern)
				{
					// Modo explícito: considerar todas las paradas del eje en el tramo.
					onPath.Add(s);
				}
				else if (StationClassification.IsPrincipalStation(s.Station))
				{
					onPath.Add(s);
				}

				index++;
			}

			onPath.Sort(static (a, b) => a.PK.CompareTo(b.PK));
			if (!increasing)
			{
				onPath.Reverse();
			}

			index = 0;
			while (index < onPath.Count)
			{
				StationOnAxis s = onPath[index];
				Station st = s.Station;

				if (pattern.DefaultDwell.HasValue || pattern.Overrides.Count > 0 || pattern.Skip.Count > 0)
				{
					TimeSpan dwell;
					if (pattern.TryGetDwell(st.Id, st.Avr, st.Name, out dwell))
					{
						stops.Add(new AsimilationStop(s, dwell));
					}
				}
				else
				{
					// Legacy
					stops.Add(new AsimilationStop(s, TimeSpan.Zero));
				}

				index++;
			}

			return stops;
		}

		private static int CountDesiredTrips(TimeSpan windowStart, TimeSpan windowEnd, TimeSpan headway)
		{
			if (headway <= TimeSpan.Zero)
			{
				return 0;
			}

			int count = 0;
			TimeSpan t = windowStart;
			while (t < windowEnd)
			{
				count++;
				t = t + headway;
			}

			return count;
		}

		private static string FormatHeadway(TimeSpan ts)
		{
			return ((int)Math.Round(ts.TotalMinutes)).ToString(System.Globalization.CultureInfo.InvariantCulture) + " min";
		}

		private static string FormatTime(TimeSpan ts)
		{
			int hours = (int)ts.TotalHours;
			int minutes = ts.Minutes;
			return hours.ToString("00", System.Globalization.CultureInfo.InvariantCulture)
				+ ":"
				+ minutes.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
		}

		private readonly struct DirectionJob
		{
			public DirectionJob(Station from, Station to, bool isReturn, string label)
			{
				From = from;
				To = to;
				IsReturn = isReturn;
				Label = label;
			}

			public Station From { get; }
			public Station To { get; }
			public bool IsReturn { get; }
			public string Label { get; }
		}

		private sealed class PreparedDirection
		{
			public PreparedDirection(DirectionJob job, Axis axis, Asimilation asimilation)
			{
				Job = job;
				Axis = axis;
				Asimilation = asimilation;
			}

			public DirectionJob Job { get; }
			public Axis Axis { get; }
			public Asimilation Asimilation { get; }
		}

		private sealed class ScheduledTrip
		{
			public ScheduledTrip(Circulation circulation, Axis axis, Asimilation asimilation)
			{
				Circulation = circulation;
				Axis = axis;
				Asimilation = asimilation;
			}

			public Circulation Circulation { get; }
			public Axis Axis { get; }
			public Asimilation Asimilation { get; }
		}
	}
}

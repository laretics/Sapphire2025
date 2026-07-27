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
		/// Genera la malla para un <see cref="DayOfWeek.Monday"/> por defecto.
		/// Preferir <see cref="Solve(DayOfWeek)"/>.
		/// </summary>
		public Mesh Solve()
		{
			return Solve(DayOfWeek.Monday);
		}

		/// <summary>
		/// Genera la malla para un día concreto: solo se procesan requisitos cuyo
		/// <see cref="DemandRequirement.ServiceDays"/> incluye ese día.
		/// Errores y warnings se etiquetan con el día.
		/// </summary>
		public Mesh Solve(DayOfWeek dayOfWeek)
		{
			Mesh mesh = new Mesh();
			mesh.PlanningDay = dayOfWeek;
			string dayTag = ServiceDays.FormatDayOfWeek(dayOfWeek);

			TopoLayout? topo = mvarPlan.Topo;
			if (topo is null)
			{
				mesh.AddError("[" + dayTag + "] El plan no tiene topología (Topo).");
				return mesh;
			}

			if (mvarPlan.Demand.Count == 0 && mvarPlan.Deletes.Count == 0)
			{
				mesh.AddWarning("[" + dayTag + "] No hay requisitos de demanda que planificar.");
				return mesh;
			}

			Dictionary<AsimilationKey, Asimilation> asimCache = new Dictionary<AsimilationKey, Asimilation>();
			List<ScheduledTrip> scheduled = new List<ScheduledTrip>();
			int circulationSeq = 0;
			int applicable = 0;

			// Ejecutar require y delete en el orden del script (permite huecos + trenes especiales).
			List<ScriptStep> steps = BuildOrderedSteps(mvarPlan);
			int stepIndex = 0;
			while (stepIndex < steps.Count)
			{
				ScriptStep step = steps[stepIndex];
				if (step.Requirement is not null)
				{
					DemandRequirement demand = step.Requirement;
					if (demand.AppliesOn(dayOfWeek))
					{
						applicable++;
						PlanOneDemand(demand, topo, mesh, asimCache, scheduled, ref circulationSeq, dayTag);
					}
				}
				else if (step.Delete is not null)
				{
					DemandDeleteOp del = step.Delete;
					if (del.AppliesOn(dayOfWeek))
					{
						ApplyDelete(del, mesh, scheduled, dayTag);
					}
				}

				stepIndex++;
			}

			if (applicable == 0 && mesh.Circulations.Count == 0)
			{
				mesh.AddWarning(
					"[" + dayTag + "] Ningún requisito de demanda aplica este día ("
					+ dayTag + ").");
			}

			// Numeración SFM (defs asim del script + tablas conocidas) y colores de asimilación.
			TrainNumbering.Assign(mesh, mvarPlan.AsimilationDefs, dayOfWeek);
			TrainNumbering.ApplyAsimilationColors(mesh, mvarPlan.AsimilationDefs, dayOfWeek);

			// Verificación final dura (por si el retraso no bastó o hay interacciones cruzadas).
			ValidateHardConstraints(scheduled, mesh, dayTag);

			// Sustituir ids técnicos (C12-R-…) en mensajes generados durante la planificación.
			TrainNumbering.RewriteMessageIds(mesh);

			return mesh;
		}

		/// <summary>
		/// Mezcla require y delete por <c>ScriptOrder</c> (orden del script).
		/// </summary>
		private static List<ScriptStep> BuildOrderedSteps(Plan plan)
		{
			List<ScriptStep> steps = new List<ScriptStep>(plan.Demand.Count + plan.Deletes.Count);
			int ri = 0;
			while (ri < plan.Demand.Count)
			{
				steps.Add(ScriptStep.ForRequire(plan.Demand[ri]));
				ri++;
			}

			int di = 0;
			while (di < plan.Deletes.Count)
			{
				steps.Add(ScriptStep.ForDelete(plan.Deletes[di]));
				di++;
			}

			steps.Sort(static (a, b) => a.Order.CompareTo(b.Order));
			return steps;
		}

		/// <summary>
		/// Elimina de la malla (y de la lista de conflictos) las circulaciones que cumplen la franja.
		/// </summary>
		private static void ApplyDelete(
			DemandDeleteOp op,
			Mesh mesh,
			List<ScheduledTrip> scheduled,
			string dayTag)
		{
			List<Circulation> doomed = new List<Circulation>();
			int ci = 0;
			while (ci < mesh.Circulations.Count)
			{
				Circulation c = mesh.Circulations[ci];
				if (op.Matches(c))
				{
					doomed.Add(c);
				}

				ci++;
			}

			int removed = 0;
			int d = 0;
			while (d < doomed.Count)
			{
				Circulation c = doomed[d];
				if (mesh.RemoveCirculation(c))
				{
					removed++;
				}

				d++;
			}

			int si = scheduled.Count - 1;
			while (si >= 0)
			{
				if (op.Matches(scheduled[si].Circulation))
				{
					scheduled.RemoveAt(si);
				}

				si--;
			}

			mesh.AddWarning(
				Tag(
					dayTag,
					op.ToString() + ": eliminadas "
					+ removed.ToString(System.Globalization.CultureInfo.InvariantCulture)
					+ " circulación(es)."));
		}

		private void PlanOneDemand(
			DemandRequirement demand,
			TopoLayout topo,
			Mesh mesh,
			Dictionary<AsimilationKey, Asimilation> asimCache,
			List<ScheduledTrip> scheduled,
			ref int circulationSeq,
			string dayTag)
		{
			if (!demand.IsResolved || demand.FromStation is null || demand.ToStation is null)
			{
				mesh.AddError("[" + dayTag + "] Requisito " + demand.Id + ": estaciones no resueltas.");
				return;
			}

			TrainSpecs specs = ResolveSpecs(demand, mesh, dayTag);
			List<DirectionJob> jobs = ExpandDirections(demand);

			// Preconstruir asimilaciones por sentido (necesario para fase de cruce).
			List<PreparedDirection> prepared = new List<PreparedDirection>();
			int jobIndex = 0;
			while (jobIndex < jobs.Count)
			{
				DirectionJob job = jobs[jobIndex];
				RouteView? view;
				StationOnRoute? originOnRoute;
				StationOnRoute? destinationOnRoute;
				if (!RouteView.TryFindPath(topo, job.From, job.To, out view, out originOnRoute, out destinationOnRoute)
					|| view is null
					|| originOnRoute is null
					|| destinationOnRoute is null)
				{
					mesh.AddError(Tag(
						dayTag,
						"Requisito " + demand.Id + ": no hay camino (eje/vista) entre '"
						+ job.From.Name + "' y '" + job.To.Name + "'."));
					jobIndex++;
					continue;
				}

				// PK de asimilación = PK de ruta.
				StationOnAxis origin = new StationOnAxis(originOnRoute.Station, originOnRoute.PK);
				StationOnAxis destination = new StationOnAxis(destinationOnRoute.Station, destinationOnRoute.PK);
				List<AsimilationStop> intermediate = BuildIntermediateStops(view, origin, destination, demand.Stops);
				AsimilationKey key = new AsimilationKey(
					specs.Id,
					view.PathSignature(),
					origin.PK,
					destination.PK,
					intermediate);
				Asimilation asim;
				if (!asimCache.TryGetValue(key, out asim!))
				{
					asim = new Asimilation(view, specs, origin, destination, intermediate);
					asimCache[key] = asim;
					mesh.AddAsimilation(asim);
				}

				prepared.Add(new PreparedDirection(job, view, asim));
				jobIndex++;
			}

			TimeSpan desiredHeadway = TimeSpan.FromHours(1.0 / demand.Frequency.TrainsPerHourValue);
			// Impares (↑PK) primero; después pares (↓PK). Cada sentido arranca al inicio de la ventana.
			SortPreparedAscendingFirst(prepared);
			TimeSpan[] phaseOffsets = ComputePhaseOffsets(demand, prepared, desiredHeadway, mesh, dayTag);

			int p = 0;
			while (p < prepared.Count)
			{
				PreparedDirection prep = prepared[p];
				// Ambos sentidos empiezan al inicio del día salvo fase explícita (cross at).
				TimeSpan phase = p < phaseOffsets.Length ? phaseOffsets[p] : TimeSpan.Zero;
				ScheduleDemandOnPath(
					demand,
					prep.Job,
					prep.View,
					prep.Asimilation,
					specs,
					mesh,
					scheduled,
					ref circulationSeq,
					phase,
					dayTag);
				p++;
			}
		}

		/// <summary>
		/// Ordena sentidos de forma que IncreasingPk (impares) quede en el índice 0.
		/// </summary>
		private static void SortPreparedAscendingFirst(List<PreparedDirection> prepared)
		{
			if (prepared.Count < 2)
			{
				return;
			}

			if (prepared[0].Asimilation.Sense == CirculationSense.IncreasingPk)
			{
				return;
			}

			int i = 1;
			while (i < prepared.Count)
			{
				if (prepared[i].Asimilation.Sense == CirculationSense.IncreasingPk)
				{
					PreparedDirection tmp = prepared[0];
					prepared[0] = prepared[i];
					prepared[i] = tmp;
					return;
				}

				i++;
			}
		}

		private static string Tag(string dayTag, string message)
		{
			return "[" + dayTag + "] " + message;
		}

		/// <summary>
		/// Desfase entre sentidos para cruzar en el punto pedido (p. ej. Enllaç).
		/// Índice 0 (ida) = 0; índice 1 (vuelta) = fase calculada.
		/// </summary>
		private static TimeSpan[] ComputePhaseOffsets(
			DemandRequirement demand,
			List<PreparedDirection> prepared,
			TimeSpan headway,
			Mesh mesh,
			string dayTag)
		{
			TimeSpan[] offsets = new TimeSpan[prepared.Count];
			int i = 0;
			while (i < offsets.Length)
			{
				offsets[i] = TimeSpan.Zero;
				i++;
			}

			// Sin cross at: ambos sentidos arrancan al inicio de la ventana (fase 0).
			// Primero se planifican todos los impares (↑PK) y después todos los pares (↓PK).
			if (prepared.Count < 2 || headway <= TimeSpan.Zero || demand.Stops.CrossAt is null)
			{
				return offsets;
			}

			StationRef crossRef = demand.Stops.CrossAt;
			PreparedDirection forward = prepared[0];
			PreparedDirection ret = prepared[1];

			StationOnRoute? crossOnRoute = FindPlacementByRef(forward.View, crossRef);
			if (crossOnRoute is null)
			{
				mesh.AddWarning(Tag(
					dayTag,
					"Requisito " + demand.Id + ": no se encontró el punto de cruce '"
					+ crossRef.Text + "' en la vista; se ignora 'cross at' (fase 0)."));
				return offsets;
			}

			TimeSpan? tForward = forward.Asimilation.TimeByPK(crossOnRoute.PK);
			TimeSpan? tReturn = ret.Asimilation.TimeByPK(crossOnRoute.PK);
			if (!tForward.HasValue || !tReturn.HasValue)
			{
				mesh.AddWarning(Tag(
					dayTag,
					"Requisito " + demand.Id + ": no se pudo calcular el cruce en '"
					+ crossRef.Text + "'."));
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

		private static StationOnRoute? FindPlacementByRef(RouteView view, StationRef reference)
		{
			int index = 0;
			while (index < view.Stations.Count)
			{
				StationOnRoute p = view.Stations[index];
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
			RouteView view,
			Asimilation asim,
			TrainSpecs specs,
			Mesh mesh,
			List<ScheduledTrip> scheduled,
			ref int circulationSeq,
			TimeSpan phaseOffset,
			string dayTag)
		{
			TimeSpan windowStart = demand.WindowStart.HasValue
				? demand.WindowStart.Value.ToTimeSpan()
				: DefaultWindowStart;
			TimeSpan windowEnd = demand.WindowEnd.HasValue
				? demand.WindowEnd.Value.ToTimeSpan()
				: DefaultWindowEnd;

			if (windowEnd <= windowStart)
			{
				mesh.AddError(Tag(dayTag, "Requisito " + demand.Id + ": ventana horaria inválida."));
				return;
			}

			double desiredTph = demand.Frequency.TrainsPerHourValue;
			TimeSpan desiredHeadway = TimeSpan.FromHours(1.0 / desiredTph);
			TimeSpan minStructuralHeadway = EstimateMinHeadway(view, asim);

			// Cadencia real no puede ser más apretada que la estructural.
			TimeSpan workingHeadway = desiredHeadway;
			if (minStructuralHeadway > workingHeadway)
			{
				workingHeadway = minStructuralHeadway;
				mesh.AddWarning(Tag(
					dayTag,
					"Requisito " + demand.Id + " (" + job.Label + "): la cadencia deseada "
					+ FormatHeadway(desiredHeadway) + " no es viable por cantones/vías; "
					+ "se usa al menos " + FormatHeadway(workingHeadway) + "."));
			}

			int desiredCount = CountDesiredTrips(windowStart, windowEnd, desiredHeadway);
			List<TimeSpan> placedDeps = new List<TimeSpan>();
			// Cadencia fija desde el inicio de ventana (+ fase si hay cross at). No se retrasan
			// salidas para "buscar hueco": si hay conflicto duro se planifica igual y se error.
			TimeSpan cursor = windowStart + phaseOffset;
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

				// Comprobar conflictos con lo ya planificado (otros sentidos/requisitos),
				// pero NO desplazar la salida: se registra error y se programa igualmente.
				string? conflictReason;
				if (!IsFeasible(dep, asim, view, scheduled, out conflictReason) && conflictReason is not null)
				{
					mesh.AddError(Tag(
						dayTag,
						"Requisito " + demand.Id + " (" + job.Label + "): "
						+ conflictReason + " — se planifica igualmente a " + FormatTime(dep) + "."));
				}

				circulationSeq++;
				string id = "C" + circulationSeq.ToString(System.Globalization.CultureInfo.InvariantCulture)
					+ "-" + demand.Id + (job.IsReturn ? "-R" : string.Empty);

				Circulation circulation = new Circulation(
					id,
					demand.Id,
					asim,
					specs,
					dep,
					demand.HasColor ? demand.Color : null);
				mesh.AddCirculation(circulation);
				scheduled.Add(new ScheduledTrip(circulation, view, asim));
				placedDeps.Add(dep);

				cursor = dep + workingHeadway;
			}

			if (placedDeps.Count < desiredCount)
			{
				mesh.AddWarning(Tag(
					dayTag,
					"Requisito " + demand.Id + " (" + job.Label + "): programados "
					+ placedDeps.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
					+ " trenes de " + desiredCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
					+ " deseados en la ventana."));
			}

			if (placedDeps.Count == 0)
			{
				mesh.AddError(Tag(
					dayTag,
					"Requisito " + demand.Id + " (" + job.Label + "): no se pudo programar ninguna circulación factible."));
			}
		}

		private static bool IsFeasible(
			TimeSpan departure,
			Asimilation candidateAsim,
			RouteView view,
			List<ScheduledTrip> scheduled,
			out string? reason)
		{
			reason = null;
			int index = 0;
			while (index < scheduled.Count)
			{
				ScheduledTrip other = scheduled[index];
				// Mismo corredor (ida/vuelta) o solape físico parcial (T3 vs T3+T2).
				if (!other.View.IsSameOrReversePath(view) && !other.View.OverlapsPhysically(view))
				{
					index++;
					continue;
				}

				if (HasHardConflict(departure, candidateAsim, view, other, out reason))
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
			RouteView view,
			ScheduledTrip b,
			out string? reason)
		{
			reason = null;
			Asimilation asimB = b.Asimilation;
			TimeSpan depB = b.Circulation.Departure;
			// Sentido físico en el terreno (multi-eje: la vuelta no usa Sense local de la vista).
			bool opposite = MeshCantonGeometry.ArePhysicallyOpposite(asimA, asimB);

			IReadOnlyList<long> frontiers = view.CantonFrontiers;
			if (frontiers.Count < 2)
			{
				// Sin fronteras: un cantón = bounding box de la vista de referencia.
				long pathMin = view.PK;
				long pathMax = view.PKEnd;
				if (pathMax > pathMin)
				{
					return CantonPairConflicts(
						depA, asimA, depB, asimB, view, pathMin, pathMax, opposite, b.Circulation, out reason);
				}

				return false;
			}

			int f = 0;
			while (f < frontiers.Count - 1)
			{
				long pk0 = frontiers[f];
				long pkf = frontiers[f + 1];
				if (CantonPairConflicts(
					depA, asimA, depB, asimB, view, pk0, pkf, opposite, b.Circulation, out reason))
				{
					return true;
				}

				f++;
			}

			return false;
		}

		/// <summary>
		/// Conflicto duro en un cantón [pk0,pkf) en PK de la vista de referencia.
		/// Permitido si sentidos físicos opuestos y ≥2 vías en el tramo.
		/// </summary>
		private static bool CantonPairConflicts(
			TimeSpan depA,
			Asimilation asimA,
			TimeSpan depB,
			Asimilation asimB,
			RouteView view,
			long pk0,
			long pkf,
			bool opposite,
			Circulation other,
			out string? reason)
		{
			reason = null;

			// Fronteras en coords de `view` → coords de cada asimilación
			// (mismo corredor, inverso o proyección parcial multi-eje).
			long a0;
			long a1;
			long b0;
			long b1;
			if (!asimA.View.TryMapCantonIntervalFrom(view, pk0, pkf, out a0, out a1)
				|| !asimB.View.TryMapCantonIntervalFrom(view, pk0, pkf, out b0, out b1))
			{
				return false;
			}

			// Ocupaciones de vía (sin dwell de estación principal).
			IReadOnlyList<MeshCantonGeometry.TrackOccupationInterval> occA =
				MeshCantonGeometry.GetTrackOccupationsInCanton(depA, asimA, a0, a1);
			IReadOnlyList<MeshCantonGeometry.TrackOccupationInterval> occB =
				MeshCantonGeometry.GetTrackOccupationsInCanton(depB, asimB, b0, b1);
			if (occA.Count == 0 || occB.Count == 0)
			{
				return false;
			}

			bool anyOverlap = false;
			int ia = 0;
			while (ia < occA.Count && !anyOverlap)
			{
				MeshCantonGeometry.TrackOccupationInterval a = occA[ia];
				int ib = 0;
				while (ib < occB.Count)
				{
					MeshCantonGeometry.TrackOccupationInterval b = occB[ib];
					if (OpenIntervalsOverlap(a.Enter, a.Exit, b.Enter, b.Exit))
					{
						anyOverlap = true;
						break;
					}

					ib++;
				}

				ia++;
			}

			if (!anyOverlap)
			{
				return false;
			}

			int tracks = MeshCantonGeometry.MaxTrackCountInCanton(view, pk0, pkf);
			if (opposite && tracks >= 2)
			{
				// Cruce en doble vía permitido.
				return false;
			}

			string kind = opposite && tracks < 2 ? "cruce en vía única" : "acantonamiento";
			// depB = salida del otro tren; depA = candidato que choca.
			reason = "conflicto de " + kind + " en ["
				+ pk0.ToString(System.Globalization.CultureInfo.InvariantCulture)
				+ ","
				+ pkf.ToString(System.Globalization.CultureInfo.InvariantCulture)
				+ ") entre el candidato (salida " + FormatTime(depA) + ") y "
				+ FormatTrainRef(other) + " (salida " + FormatTime(depB) + ").";
			return true;
		}

		/// <summary>
		/// Referencia legible a un tren: "tren 4901" si ya está numerado, si no el id técnico.
		/// </summary>
		private static string FormatTrainRef(string circulationId)
		{
			if (string.IsNullOrEmpty(circulationId))
			{
				return "tren ?";
			}

			return "tren " + circulationId;
		}

		private static string FormatTrainRef(Circulation circulation)
		{
			if (circulation is null)
			{
				return "tren ?";
			}

			if (circulation.HasServiceNumber)
			{
				return "tren " + circulation.ServiceNumber;
			}

			return "tren " + circulation.Id;
		}

		private static bool IntervalOverlapsPath(long pk0, long pkf, Asimilation asim)
		{
			long a0 = Math.Min(asim.Origin.PK, asim.Destination.PK);
			long a1 = Math.Max(asim.Origin.PK, asim.Destination.PK);
			return pk0 < a1 && pkf > a0;
		}

		private static bool OpenIntervalsOverlap(TimeSpan a0, TimeSpan a1, TimeSpan b0, TimeSpan b1)
		{
			// [a0,a1) ∩ [b0,b1) no vacío
			return a0 < b1 && b0 < a1;
		}

		/// <summary>
		/// Cota inferior de intervalo entre trenes del mismo patrón: mayor ocupación de un cantón en la ruta.
		/// </summary>
		private static TimeSpan EstimateMinHeadway(RouteView view, Asimilation asim)
		{
			TimeSpan maxOcc = TimeSpan.FromMinutes(2);
			IReadOnlyList<long> frontiers = view.CantonFrontiers;
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

				long local0;
				long local1;
				if (!asim.View.TryMapCantonIntervalFrom(view, pk0, pkf, out local0, out local1))
				{
					f++;
					continue;
				}

				// Solo tiempo en vía (sin dwell de estación principal).
				IReadOnlyList<MeshCantonGeometry.TrackOccupationInterval> occs =
					MeshCantonGeometry.GetTrackOccupationsInCanton(TimeSpan.Zero, asim, local0, local1);
				int oi = 0;
				while (oi < occs.Count)
				{
					MeshCantonGeometry.TrackOccupationInterval iv = occs[oi];
					if (iv.Exit > iv.Enter)
					{
						TimeSpan occ = iv.Exit - iv.Enter;
						if (occ > maxOcc)
						{
							maxOcc = occ;
						}
					}

					oi++;
				}

				f++;
			}

			return maxOcc;
		}

		private void ValidateHardConstraints(List<ScheduledTrip> scheduled, Mesh mesh, string dayTag)
		{
			int i = 0;
			while (i < scheduled.Count)
			{
				int j = i + 1;
				while (j < scheduled.Count)
				{
					ScheduledTrip a = scheduled[i];
					ScheduledTrip b = scheduled[j];
					if (a.View.IsSameOrReversePath(b.View) || a.View.OverlapsPhysically(b.View))
					{
						string? reason;
						if (HasHardConflict(a.Circulation.Departure, a.Asimilation, a.View, b, out reason)
							|| HasHardConflict(b.Circulation.Departure, b.Asimilation, b.View, a, out reason))
						{
							mesh.AddError(Tag(
								dayTag,
								"Violación dura entre "
								+ FormatTrainRef(a.Circulation) + " y " + FormatTrainRef(b.Circulation)
								+ (reason is null ? "." : (": " + reason))));
						}
					}

					j++;
				}

				i++;
			}
		}

		private TrainSpecs ResolveSpecs(DemandRequirement demand, Mesh mesh, string dayTag)
		{
			if (demand.FleetId.Length > 0)
			{
				TrainSpecs? found = mvarPlan.FindTrainSpecsById(demand.FleetId);
				if (found is not null)
				{
					return found;
				}

				mesh.AddWarning(Tag(
					dayTag,
					"Requisito " + demand.Id + ": fleet '" + demand.FleetId
					+ "' no está en el catálogo; se usa el modelo por defecto."));
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

		/// <summary>
		/// Paradas intermedias según <see cref="StopPattern"/> del requisito, sobre PK de ruta.
		/// Con <c>stops Ns</c>: todas las estaciones/apeaderos del trayecto salvo skip, con dwell.
		/// Sin patrón: legacy (solo principales, dwell 0).
		/// </summary>
		private static List<AsimilationStop> BuildIntermediateStops(
			RouteView view,
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
			while (index < view.Stations.Count)
			{
				StationOnRoute s = view.Stations[index];
				if (s.PK <= min || s.PK >= max)
				{
					index++;
					continue;
				}

				if (pattern.DefaultDwell.HasValue || pattern.HasExplicitPattern)
				{
					// Modo explícito: todas las paradas de la vista en el tramo (PK de ruta).
					onPath.Add(new StationOnAxis(s.Station, s.PK));
				}
				else if (StationClassification.IsPrincipalStation(s.Station))
				{
					onPath.Add(new StationOnAxis(s.Station, s.PK));
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

		private readonly struct ScriptStep
		{
			private readonly int mvarOrder;
			private readonly DemandRequirement? mvarRequirement;
			private readonly DemandDeleteOp? mvarDelete;

			private ScriptStep(int order, DemandRequirement? requirement, DemandDeleteOp? delete)
			{
				mvarOrder = order;
				mvarRequirement = requirement;
				mvarDelete = delete;
			}

			public static ScriptStep ForRequire(DemandRequirement requirement)
			{
				return new ScriptStep(requirement.ScriptOrder, requirement, null);
			}

			public static ScriptStep ForDelete(DemandDeleteOp delete)
			{
				return new ScriptStep(delete.ScriptOrder, null, delete);
			}

			public int Order
			{
				get { return mvarOrder; }
			}

			public DemandRequirement? Requirement
			{
				get { return mvarRequirement; }
			}

			public DemandDeleteOp? Delete
			{
				get { return mvarDelete; }
			}
		}

		private readonly struct DirectionJob
		{
			private readonly Station mvarFrom;
			private readonly Station mvarTo;
			private readonly bool mvarIsReturn;
			private readonly string mvarLabel;

			public DirectionJob(Station from, Station to, bool isReturn, string label)
			{
				mvarFrom = from;
				mvarTo = to;
				mvarIsReturn = isReturn;
				mvarLabel = label;
			}

			public Station From
			{
				get { return mvarFrom; }
			}

			public Station To
			{
				get { return mvarTo; }
			}

			public bool IsReturn
			{
				get { return mvarIsReturn; }
			}

			public string Label
			{
				get { return mvarLabel; }
			}
		}

		private sealed class PreparedDirection
		{
			private readonly DirectionJob mvarJob;
			private readonly RouteView mvarView;
			private readonly Asimilation mvarAsimilation;

			public PreparedDirection(DirectionJob job, RouteView view, Asimilation asimilation)
			{
				mvarJob = job;
				mvarView = view;
				mvarAsimilation = asimilation;
			}

			public DirectionJob Job
			{
				get { return mvarJob; }
			}

			public RouteView View
			{
				get { return mvarView; }
			}

			public Asimilation Asimilation
			{
				get { return mvarAsimilation; }
			}
		}

		private sealed class ScheduledTrip
		{
			private readonly Circulation mvarCirculation;
			private readonly RouteView mvarView;
			private readonly Asimilation mvarAsimilation;

			public ScheduledTrip(Circulation circulation, RouteView view, Asimilation asimilation)
			{
				mvarCirculation = circulation;
				mvarView = view;
				mvarAsimilation = asimilation;
			}

			public Circulation Circulation
			{
				get { return mvarCirculation; }
			}

			public RouteView View
			{
				get { return mvarView; }
			}

			public Asimilation Asimilation
			{
				get { return mvarAsimilation; }
			}
		}
	}
}

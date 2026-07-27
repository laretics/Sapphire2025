using System;
using System.Collections.Generic;
using System.Globalization;
using Diamond.Motion;
using Diamond.Timed;
using Diamond.Topo;
using MotionAsimilation = Diamond.Motion.Asimilation;
using TimedCirculation = Diamond.Timed.Circulation;
using TimedMesh = Diamond.Timed.Mesh;

namespace Diamond.Project
{
	/// <summary>
	/// Construye un <see cref="Project"/> a partir de una malla planificada y verificada.
	/// Factoriza asimilaciones y materializa horarios absolutos por circulación.
	/// </summary>
	public static class ProjectCompiler
	{
		/// <summary>
		/// Compila el proyecto desde la malla actual del planificador.
		/// </summary>
		public static Project Compile(TimedMesh mesh, CompileOptions? options = null)
		{
			if (mesh is null)
			{
				throw new ArgumentNullException(nameof(mesh));
			}

			options ??= new CompileOptions();
			Project project = new Project();
			project.CompiledUtc = DateTime.UtcNow;
			project.PlanningDay = mesh.PlanningDay ?? options.PlanningDay;
			project.Name = options.Name ?? string.Empty;
			project.Id = options.Id ?? string.Empty;
			project.SourceScript = options.SourceScript ?? string.Empty;

			if (project.Name.Length == 0)
			{
				string day = project.PlanningDay.HasValue
					? ServiceDays.FormatDayOfWeek(project.PlanningDay.Value)
					: "dia";
				project.Name = "Proyecto " + day;
			}

			if (project.Id.Length == 0)
			{
				project.Id = "P-" + project.CompiledUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
			}

			// Solo asimilaciones referenciadas por alguna circulación (tras delete puede sobrar basura).
			Dictionary<MotionAsimilation, Asimilation> map =
				new Dictionary<MotionAsimilation, Asimilation>(ReferenceEqualityComparer<MotionAsimilation>.Instance);
			int asimSeq = 0;

			// Orden estable: por número de servicio, luego salida, luego technical id.
			List<TimedCirculation> ordered = new List<TimedCirculation>(mesh.Circulations);
			ordered.Sort(CompareCirculations);

			int ci = 0;
			while (ci < ordered.Count)
			{
				TimedCirculation src = ordered[ci];
				MotionAsimilation motionAsim = src.Asimilation;
				Asimilation? projectAsim;
				if (!map.TryGetValue(motionAsim, out projectAsim))
				{
					asimSeq++;
					projectAsim = BuildAsimilation(motionAsim, "A" + asimSeq.ToString(CultureInfo.InvariantCulture));
					map[motionAsim] = projectAsim;
					project.AddAsimilation(projectAsim);
				}

				string id = src.HasServiceNumber
					? src.ServiceNumber
					: src.Id;
				Circulation circ = new Circulation(
					id,
					src.TechnicalId,
					src.DemandId,
					src.ServiceNumber,
					src.Departure,
					src.Color,
					projectAsim);
				project.AddCirculation(circ);
				ci++;
			}

			project.AddNote(
				"Compilado: "
				+ project.Circulations.Count.ToString(CultureInfo.InvariantCulture)
				+ " circulaciones, "
				+ project.Asimilations.Count.ToString(CultureInfo.InvariantCulture)
				+ " asimilaciones.");

			if (!mesh.Success)
			{
				project.AddNote(
					"La malla origen tenía "
					+ mesh.Errors.Count.ToString(CultureInfo.InvariantCulture)
					+ " error(es) duros; el proyecto se generó igualmente.");
			}

			int wi = 0;
			while (wi < mesh.Warnings.Count && wi < 20)
			{
				project.AddNote("malla: " + mesh.Warnings[wi]);
				wi++;
			}

			return project;
		}

		/// <summary>
		/// Compila usando metadatos del <see cref="Timed.Plan"/> (nombre, script).
		/// </summary>
		public static Project Compile(Timed.Plan plan, TimedMesh mesh)
		{
			if (plan is null)
			{
				throw new ArgumentNullException(nameof(plan));
			}

			CompileOptions options = new CompileOptions
			{
				Name = plan.Name.Length > 0 ? plan.Name : null,
				Id = plan.Id.Length > 0 ? plan.Id : null,
				SourceScript = plan.DemandScript,
				PlanningDay = mesh?.PlanningDay
			};
			return Compile(mesh!, options);
		}

		private static Asimilation BuildAsimilation(MotionAsimilation src, string id)
		{
			StationInfo origin = FromStation(src.Origin.Station);
			StationInfo destination = FromStation(src.Destination.Station);
			List<Call> calls = new List<Call>();

			// Origen
			calls.Add(new Call(
				origin,
				src.Origin.PK,
				TimeSpan.Zero,
				TimeSpan.Zero,
				TimeSpan.Zero,
				isOrigin: true,
				isDestination: false,
				commercialStop: true));

			// Intermedias
			int si = 0;
			while (si < src.Stops.Count)
			{
				AsimilationStop stop = src.Stops[si];
				TimeSpan? tArr = src.TimeByPK(stop.PK);
				TimeSpan arrival = tArr ?? TimeSpan.Zero;
				// TimeByPK en parada suele ser el instante de reanudación o de llegada;
				// modelamos llegada = t, salida = t + dwell si dwell > 0 y t parece fin de dwell.
				// Convención del motor: el tiempo en el PK de parada es tras el dwell (salida).
				// Por eso: DepartureOffset = t, ArrivalOffset = t - dwell (clamp ≥ 0).
				TimeSpan dwell = stop.Dwell;
				TimeSpan departure = arrival;
				TimeSpan arrOffset = departure - dwell;
				if (arrOffset < TimeSpan.Zero)
				{
					arrOffset = TimeSpan.Zero;
				}

				calls.Add(new Call(
					FromStation(stop.Placement.Station),
					stop.PK,
					arrOffset,
					departure,
					dwell,
					isOrigin: false,
					isDestination: false,
					commercialStop: dwell > TimeSpan.Zero));
				si++;
			}

			// Destino
			TimeSpan total = src.TotalTime;
			calls.Add(new Call(
				destination,
				src.Destination.PK,
				total,
				total,
				TimeSpan.Zero,
				isOrigin: false,
				isDestination: true,
				commercialStop: true));

			string viewId = src.View is not null ? src.View.Id : string.Empty;
			string pathSig = src.View is not null ? src.View.PathSignature() : string.Empty;
			string fleetId = src.Specs is not null ? src.Specs.Id : string.Empty;

			return new Asimilation(
				id,
				origin,
				destination,
				src.Sense,
				viewId,
				pathSig,
				fleetId,
				total,
				calls);
		}

		private static StationInfo FromStation(Station station)
		{
			if (station is null)
			{
				return new StationInfo(string.Empty, string.Empty, string.Empty);
			}

			return new StationInfo(station.Id, station.Name, station.Avr);
		}

		private static int CompareCirculations(TimedCirculation a, TimedCirculation b)
		{
			// Sin número al final; si ambos tienen, orden lexicográfico (funciona para 4901 y P1MTX).
			if (a.HasServiceNumber != b.HasServiceNumber)
			{
				return a.HasServiceNumber ? -1 : 1;
			}

			if (a.HasServiceNumber && b.HasServiceNumber)
			{
				int byNumber = string.CompareOrdinal(a.ServiceNumber, b.ServiceNumber);
				if (byNumber != 0)
				{
					return byNumber;
				}
			}

			int byDep = a.Departure.CompareTo(b.Departure);
			if (byDep != 0)
			{
				return byDep;
			}

			return string.CompareOrdinal(a.TechnicalId, b.TechnicalId);
		}

		/// <summary>
		/// Comparador por identidad de referencia (misma instancia de asimilación de malla).
		/// </summary>
		private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
			where T : class
		{
			public static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();

			public bool Equals(T? x, T? y)
			{
				return ReferenceEquals(x, y);
			}

			public int GetHashCode(T obj)
			{
				return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
			}
		}
	}

	/// <summary>Opciones al compilar un <see cref="Project"/>.</summary>
	public sealed class CompileOptions
	{
		private string? mvarName;
		private string? mvarId;
		private string? mvarSourceScript;
		private DayOfWeek? mvarPlanningDay;

		public string? Name
		{
			get { return mvarName; }
			set { mvarName = value; }
		}

		public string? Id
		{
			get { return mvarId; }
			set { mvarId = value; }
		}

		public string? SourceScript
		{
			get { return mvarSourceScript; }
			set { mvarSourceScript = value; }
		}

		public DayOfWeek? PlanningDay
		{
			get { return mvarPlanningDay; }
			set { mvarPlanningDay = value; }
		}
	}
}

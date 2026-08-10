using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Diamond.Motion;
using Diamond.Timed;

namespace Diamond.Project
{
	/// <summary>
	/// Paquete JSON de un plan publicado para clientes (Tourmaline):
	/// un <see cref="Project"/> por día de la semana.
	/// Formato: <c>diamond-published-v1</c>.
	/// </summary>
	public sealed class PublishedProjectPackage
	{
		public const string FormatId = "diamond-published-v1";

		private static readonly JsonSerializerOptions soptJson = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = false,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
		};

		public string Format { get; set; } = FormatId;

		public string Name { get; set; } = string.Empty;

		public string TopoContentHash { get; set; } = string.Empty;

		public string TopoStructuralHash { get; set; } = string.Empty;

		public DateTime CompiledUtc { get; set; }

		public List<string> Notes { get; set; } = new List<string>();

		public List<PublishedDayDto> Days { get; set; } = new List<PublishedDayDto>();

		public static byte[] ToUtf8Json(PublishedProjectPackage package)
		{
			if (package is null)
			{
				throw new ArgumentNullException(nameof(package));
			}

			return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(package, soptJson));
		}

		public static PublishedProjectPackage FromUtf8Json(byte[] payload)
		{
			if (payload is null || payload.Length == 0)
			{
				throw new ArgumentException("Payload vacío.", nameof(payload));
			}

			PublishedProjectPackage? package = JsonSerializer.Deserialize<PublishedProjectPackage>(
				payload,
				soptJson);
			if (package is null)
			{
				throw new InvalidOperationException("No se pudo deserializar el paquete publicado.");
			}

			return package;
		}

		/// <summary>
		/// Construye el paquete a partir de un <see cref="Plan"/> ya compilado (demanda + topo).
		/// Planifica los 7 días y materializa un Project por día.
		/// </summary>
		public static PublishedProjectPackage BuildFromPlan(Timed.Plan plan, string topoContentHash, string topoStructuralHash)
		{
			if (plan is null)
			{
				throw new ArgumentNullException(nameof(plan));
			}

			if (plan.Topo is null)
			{
				throw new InvalidOperationException("El plan no tiene topología cargada.");
			}

			PublishedProjectPackage package = new PublishedProjectPackage
			{
				Name = plan.Name.Length > 0 ? plan.Name : "Plan",
				TopoContentHash = topoContentHash ?? string.Empty,
				TopoStructuralHash = topoStructuralHash ?? string.Empty,
				CompiledUtc = DateTime.UtcNow
			};

			MeshPlanner planner = new MeshPlanner(plan);
			DayOfWeek[] days =
			{
				DayOfWeek.Monday,
				DayOfWeek.Tuesday,
				DayOfWeek.Wednesday,
				DayOfWeek.Thursday,
				DayOfWeek.Friday,
				DayOfWeek.Saturday,
				DayOfWeek.Sunday
			};

			int hardErrors = 0;
			List<string> sampleHardErrors = new List<string>();
			const int maxSampleHardErrors = 8;
			int di = 0;
			while (di < days.Length)
			{
				DayOfWeek day = days[di];
				Mesh mesh = planner.Solve(day);
				Project project = ProjectCompiler.Compile(plan, mesh);
				PublishedDayDto dayDto = FromProject(day, project);
				package.Days.Add(dayDto);

				if (!mesh.Success)
				{
					hardErrors += mesh.Errors.Count;
					package.Notes.Add(
						ServiceDays.FormatDayOfWeek(day)
						+ ": "
						+ mesh.Errors.Count.ToString(CultureInfo.InvariantCulture)
						+ " error(es) de malla.");

					// Muestras para diagnosticar (p. ej. cantones ausentes, refs, conflictos).
					int ei = 0;
					while (ei < mesh.Errors.Count && sampleHardErrors.Count < maxSampleHardErrors)
					{
						sampleHardErrors.Add(mesh.Errors[ei]);
						ei++;
					}
				}

				int wi = 0;
				while (wi < mesh.Warnings.Count && wi < 5)
				{
					package.Notes.Add(ServiceDays.FormatDayOfWeek(day) + " av: " + mesh.Warnings[wi]);
					wi++;
				}

				di++;
			}

			package.Notes.Insert(
				0,
				"Compilado: "
				+ package.Days.Count.ToString(CultureInfo.InvariantCulture)
				+ " días"
				+ (hardErrors > 0
					? ", con "
						+ hardErrors.ToString(CultureInfo.InvariantCulture)
						+ " error(es) duros de malla (publicado igualmente)."
					: "."));

			int si = 0;
			while (si < sampleHardErrors.Count)
			{
				package.Notes.Add("ejemplo: " + sampleHardErrors[si]);
				si++;
			}

			return package;
		}

		public static PublishedDayDto FromProject(DayOfWeek day, Project project)
		{
			if (project is null)
			{
				throw new ArgumentNullException(nameof(project));
			}

			PublishedDayDto dto = new PublishedDayDto
			{
				Day = day,
				Name = project.Name,
				Id = project.Id,
				CompiledUtc = project.CompiledUtc
			};

			Dictionary<Asimilation, string> asimIds =
				new Dictionary<Asimilation, string>(ReferenceEqualityComparer.Instance);
			int ai = 0;
			while (ai < project.Asimilations.Count)
			{
				Asimilation a = project.Asimilations[ai];
				PublishedAsimilationDto ad = new PublishedAsimilationDto
				{
					Id = a.Id,
					OriginId = a.Origin.Id,
					OriginName = a.Origin.Name,
					OriginAvr = a.Origin.Avr,
					DestinationId = a.Destination.Id,
					DestinationName = a.Destination.Name,
					DestinationAvr = a.Destination.Avr,
					Sense = (int)a.Sense,
					ViewId = a.ViewId,
					PathSignature = a.PathSignature,
					FleetId = a.FleetId,
					TotalSeconds = a.TotalTime.TotalSeconds
				};
				int ci = 0;
				while (ci < a.Calls.Count)
				{
					Call c = a.Calls[ci];
					ad.Calls.Add(new PublishedCallDto
					{
						StationId = c.Station.Id,
						StationName = c.Station.Name,
						StationAvr = c.Station.Avr,
						Pk = c.Pk,
						ArrivalSeconds = c.ArrivalOffset.TotalSeconds,
						DepartureSeconds = c.DepartureOffset.TotalSeconds,
						DwellSeconds = c.Dwell.TotalSeconds,
						IsOrigin = c.IsOrigin,
						IsDestination = c.IsDestination,
						CommercialStop = c.CommercialStop
					});
					ci++;
				}

				asimIds[a] = a.Id;
				dto.Asimilations.Add(ad);
				ai++;
			}

			int pi = 0;
			while (pi < project.Circulations.Count)
			{
				Circulation circ = project.Circulations[pi];
				string asimId = circ.Asimilation.Id;
				PublishedCirculationDto cd = new PublishedCirculationDto
				{
					Id = circ.Id,
					TechnicalId = circ.TechnicalId,
					DemandId = circ.DemandId,
					ServiceNumber = circ.ServiceNumber,
					DepartureSeconds = circ.Departure.TotalSeconds,
					Color = circ.Color,
					AsimilationId = asimId
				};
				int ti = 0;
				while (ti < circ.Calls.Count)
				{
					TimedCall tc = circ.Calls[ti];
					cd.Calls.Add(new PublishedTimedCallDto
					{
						StationId = tc.Station.Id,
						StationName = tc.Station.Name,
						StationAvr = tc.Station.Avr,
						Pk = tc.Pk,
						ArrivalSeconds = tc.Arrival.TotalSeconds,
						DepartureSeconds = tc.Departure.TotalSeconds,
						DwellSeconds = tc.Dwell.TotalSeconds,
						IsOrigin = tc.IsOrigin,
						IsDestination = tc.IsDestination,
						CommercialStop = tc.Template.CommercialStop
					});
					ti++;
				}

				dto.Circulations.Add(cd);
				pi++;
			}

			return dto;
		}

		private sealed class ReferenceEqualityComparer : IEqualityComparer<Asimilation>
		{
			public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

			public bool Equals(Asimilation? x, Asimilation? y)
			{
				return ReferenceEquals(x, y);
			}

			public int GetHashCode(Asimilation obj)
			{
				return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
			}
		}
	}

	public sealed class PublishedDayDto
	{
		public DayOfWeek Day { get; set; }

		public string Name { get; set; } = string.Empty;

		public string Id { get; set; } = string.Empty;

		public DateTime CompiledUtc { get; set; }

		public List<PublishedAsimilationDto> Asimilations { get; set; } = new List<PublishedAsimilationDto>();

		public List<PublishedCirculationDto> Circulations { get; set; } = new List<PublishedCirculationDto>();
	}

	public sealed class PublishedAsimilationDto
	{
		public string Id { get; set; } = string.Empty;

		public string OriginId { get; set; } = string.Empty;

		public string OriginName { get; set; } = string.Empty;

		public string OriginAvr { get; set; } = string.Empty;

		public string DestinationId { get; set; } = string.Empty;

		public string DestinationName { get; set; } = string.Empty;

		public string DestinationAvr { get; set; } = string.Empty;

		public int Sense { get; set; }

		public string ViewId { get; set; } = string.Empty;

		public string PathSignature { get; set; } = string.Empty;

		public string FleetId { get; set; } = string.Empty;

		public double TotalSeconds { get; set; }

		public List<PublishedCallDto> Calls { get; set; } = new List<PublishedCallDto>();
	}

	public sealed class PublishedCallDto
	{
		public string StationId { get; set; } = string.Empty;

		public string StationName { get; set; } = string.Empty;

		public string StationAvr { get; set; } = string.Empty;

		public long Pk { get; set; }

		public double ArrivalSeconds { get; set; }

		public double DepartureSeconds { get; set; }

		public double DwellSeconds { get; set; }

		public bool IsOrigin { get; set; }

		public bool IsDestination { get; set; }

		public bool CommercialStop { get; set; }
	}

	public sealed class PublishedCirculationDto
	{
		public string Id { get; set; } = string.Empty;

		public string TechnicalId { get; set; } = string.Empty;

		public string DemandId { get; set; } = string.Empty;

		public string ServiceNumber { get; set; } = string.Empty;

		public double DepartureSeconds { get; set; }

		public string Color { get; set; } = string.Empty;

		public string AsimilationId { get; set; } = string.Empty;

		public List<PublishedTimedCallDto> Calls { get; set; } = new List<PublishedTimedCallDto>();
	}

	public sealed class PublishedTimedCallDto
	{
		public string StationId { get; set; } = string.Empty;

		public string StationName { get; set; } = string.Empty;

		public string StationAvr { get; set; } = string.Empty;

		public long Pk { get; set; }

		public double ArrivalSeconds { get; set; }

		public double DepartureSeconds { get; set; }

		public double DwellSeconds { get; set; }

		public bool IsOrigin { get; set; }

		public bool IsDestination { get; set; }

		public bool CommercialStop { get; set; }
	}
}

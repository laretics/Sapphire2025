using System;
using System.Collections.Generic;
using Diamond.Motion;

namespace Diamond.Project
{
	/// <summary>
	/// Reconstruye un <see cref="Project"/> de dominio a partir de un día del paquete publicado
	/// (<c>diamond-published-v1</c>). Pensado para clientes de cabina (Tourmaline).
	/// </summary>
	public static class PublishedProjectHydrator
	{
		public static Project? DayToProject(PublishedProjectPackage package, DayOfWeek day)
		{
			if (package is null)
			{
				throw new ArgumentNullException(nameof(package));
			}

			PublishedDayDto? dayDto = null;
			int i = 0;
			while (i < package.Days.Count)
			{
				if (package.Days[i].Day == day)
				{
					dayDto = package.Days[i];
					break;
				}

				i++;
			}

			if (dayDto is null)
			{
				return null;
			}

			return ToProject(dayDto);
		}

		/// <summary>
		/// Proyecto del día civil local (reloj del tren).
		/// </summary>
		public static Project? ProjectForLocalDate(PublishedProjectPackage package, DateTime localNow)
		{
			return DayToProject(package, localNow.DayOfWeek);
		}

		public static Project ToProject(PublishedDayDto day)
		{
			if (day is null)
			{
				throw new ArgumentNullException(nameof(day));
			}

			Project project = new Project();
			project.Id = day.Id ?? string.Empty;
			project.Name = day.Name ?? string.Empty;
			project.PlanningDay = day.Day;
			project.CompiledUtc = day.CompiledUtc;

			Dictionary<string, Asimilation> asimsById =
				new Dictionary<string, Asimilation>(StringComparer.Ordinal);

			int ai = 0;
			while (ai < day.Asimilations.Count)
			{
				PublishedAsimilationDto src = day.Asimilations[ai];
				Asimilation asim = BuildAsimilation(src);
				asimsById[asim.Id] = asim;
				project.AddAsimilation(asim);
				ai++;
			}

			int ci = 0;
			while (ci < day.Circulations.Count)
			{
				PublishedCirculationDto src = day.Circulations[ci];
				Asimilation? asim;
				if (!asimsById.TryGetValue(src.AsimilationId ?? string.Empty, out asim))
				{
					// Circulación huérfana: construir asimilación mínima desde sus calls.
					asim = BuildAsimilationFromCirculationCalls(src);
					asimsById[asim.Id] = asim;
					project.AddAsimilation(asim);
				}

				TimeSpan departure = TimeSpan.FromSeconds(src.DepartureSeconds);
				Circulation circ = new Circulation(
					src.Id ?? string.Empty,
					src.TechnicalId ?? string.Empty,
					src.DemandId ?? string.Empty,
					src.ServiceNumber ?? string.Empty,
					departure,
					src.Color ?? string.Empty,
					asim);
				project.AddCirculation(circ);
				ci++;
			}

			return project;
		}

		private static Asimilation BuildAsimilation(PublishedAsimilationDto src)
		{
			StationInfo origin = new StationInfo(
				src.OriginId ?? string.Empty,
				src.OriginName ?? string.Empty,
				src.OriginAvr ?? string.Empty);
			StationInfo destination = new StationInfo(
				src.DestinationId ?? string.Empty,
				src.DestinationName ?? string.Empty,
				src.DestinationAvr ?? string.Empty);

			List<Call> calls = new List<Call>();
			int i = 0;
			while (i < src.Calls.Count)
			{
				PublishedCallDto c = src.Calls[i];
				StationInfo st = new StationInfo(
					c.StationId ?? string.Empty,
					c.StationName ?? string.Empty,
					c.StationAvr ?? string.Empty);
				TimeSpan arr = TimeSpan.FromSeconds(c.ArrivalSeconds);
				TimeSpan dep = TimeSpan.FromSeconds(c.DepartureSeconds);
				if (dep < arr)
				{
					dep = arr;
				}

				TimeSpan dwell = TimeSpan.FromSeconds(c.DwellSeconds);
				if (dwell < TimeSpan.Zero)
				{
					dwell = TimeSpan.Zero;
				}

				calls.Add(new Call(
					st,
					c.Pk,
					arr,
					dep,
					dwell,
					c.IsOrigin,
					c.IsDestination,
					c.CommercialStop));
				i++;
			}

			CirculationSense sense = src.Sense < 0
				? CirculationSense.DecreasingPk
				: CirculationSense.IncreasingPk;

			return new Asimilation(
				src.Id ?? string.Empty,
				origin,
				destination,
				sense,
				src.ViewId ?? string.Empty,
				src.PathSignature ?? string.Empty,
				src.FleetId ?? string.Empty,
				TimeSpan.FromSeconds(src.TotalSeconds),
				calls);
		}

		private static Asimilation BuildAsimilationFromCirculationCalls(PublishedCirculationDto src)
		{
			string asimId = string.IsNullOrEmpty(src.AsimilationId)
				? "orphan-" + (src.Id ?? "x")
				: src.AsimilationId;

			StationInfo origin = new StationInfo(string.Empty, string.Empty, string.Empty);
			StationInfo destination = new StationInfo(string.Empty, string.Empty, string.Empty);
			List<Call> relativeCalls = new List<Call>();
			TimeSpan baseDep = TimeSpan.FromSeconds(src.DepartureSeconds);

			if (src.Calls.Count > 0)
			{
				PublishedTimedCallDto first = src.Calls[0];
				PublishedTimedCallDto last = src.Calls[src.Calls.Count - 1];
				origin = new StationInfo(
					first.StationId ?? string.Empty,
					first.StationName ?? string.Empty,
					first.StationAvr ?? string.Empty);
				destination = new StationInfo(
					last.StationId ?? string.Empty,
					last.StationName ?? string.Empty,
					last.StationAvr ?? string.Empty);

				int i = 0;
				while (i < src.Calls.Count)
				{
					PublishedTimedCallDto c = src.Calls[i];
					StationInfo st = new StationInfo(
						c.StationId ?? string.Empty,
						c.StationName ?? string.Empty,
						c.StationAvr ?? string.Empty);
					TimeSpan absArr = TimeSpan.FromSeconds(c.ArrivalSeconds);
					TimeSpan absDep = TimeSpan.FromSeconds(c.DepartureSeconds);
					TimeSpan relArr = absArr - baseDep;
					TimeSpan relDep = absDep - baseDep;
					if (relArr < TimeSpan.Zero)
					{
						relArr = TimeSpan.Zero;
					}

					if (relDep < relArr)
					{
						relDep = relArr;
					}

					relativeCalls.Add(new Call(
						st,
						c.Pk,
						relArr,
						relDep,
						TimeSpan.FromSeconds(c.DwellSeconds < 0 ? 0 : c.DwellSeconds),
						c.IsOrigin,
						c.IsDestination,
						c.CommercialStop));
					i++;
				}
			}

			TimeSpan total = TimeSpan.Zero;
			if (relativeCalls.Count > 0)
			{
				total = relativeCalls[relativeCalls.Count - 1].DepartureOffset;
			}

			return new Asimilation(
				asimId,
				origin,
				destination,
				CirculationSense.IncreasingPk,
				string.Empty,
				string.Empty,
				string.Empty,
				total,
				relativeCalls);
		}
	}
}

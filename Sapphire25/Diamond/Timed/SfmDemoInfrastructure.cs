using System;
using System.Collections.Generic;
using Diamond.Topo;

namespace Diamond.Timed
{
	/// <summary>
	/// Reglas de infraestructura de ejemplo para SFM (demo de planificación).
	/// Fronteras de cantón = estaciones principales (AVR en mayúsculas), no apeaderos.
	/// Doble vía entre Palma y Enllaç en T3; resto vía única.
	/// </summary>
	public static class SfmDemoInfrastructure
	{
		public const string PalmaManacorAxisId = "T3";
		public const string PalmaStationId = "40";

		/// <summary>
		/// Aplica fronteras de cantón y número de vías de demo sobre un layout ya cargado.
		/// </summary>
		public static void Apply(TopoLayout layout)
		{
			if (layout is null)
			{
				throw new ArgumentNullException(nameof(layout));
			}

			int axisIndex = 0;
			while (axisIndex < layout.Axes.Count)
			{
				Axis axis = layout.Axes[axisIndex];
				ApplyCantonFrontiersFromPrincipalStations(axis);

				if (string.Equals(axis.Id, PalmaManacorAxisId, StringComparison.Ordinal))
				{
					ApplyPalmaEnllacDoubleTrack(axis);
				}
				else
				{
					axis.DefaultTrackCount = 1;
					axis.ClearTrackSpans();
				}

				axisIndex++;
			}
		}

		/// <summary>
		/// Fronteras = PK de estaciones principales presentes en el eje (AVR en mayúsculas).
		/// </summary>
		public static void ApplyCantonFrontiersFromPrincipalStations(Axis axis)
		{
			if (axis is null)
			{
				throw new ArgumentNullException(nameof(axis));
			}

			List<long> frontiers = new List<long>();
			int index = 0;
			while (index < axis.Stations.Count)
			{
				StationOnAxis placement = axis.Stations[index];
				if (StationClassification.IsPrincipalStation(placement.Station))
				{
					frontiers.Add(placement.PK);
				}

				index++;
			}

			// Extremos del eje siempre acotan cantones si hay recorrido.
			if (axis.IsBuilt)
			{
				frontiers.Add(axis.PK);
				frontiers.Add(axis.PKEnd);
			}

			axis.SetCantonFrontiers(frontiers);
		}

		/// <summary>
		/// Doble vía Palma (PK 0) – Enllaç en T3; el resto del eje queda en vía única (default = 1).
		/// </summary>
		public static void ApplyPalmaEnllacDoubleTrack(Axis axis)
		{
			if (axis is null)
			{
				throw new ArgumentNullException(nameof(axis));
			}

			axis.DefaultTrackCount = 1;
			axis.ClearTrackSpans();

			long? enllacPk = FindEnllacPk(axis);
			if (!enllacPk.HasValue)
			{
				return;
			}

			long palmaPk = 0L;
			StationOnAxis? palma = FindPlacementByStationId(axis, PalmaStationId);
			if (palma is not null)
			{
				palmaPk = palma.PK;
			}

			// [palma, enllac] inclusivo en la práctica: el extremo Enllaç es estación de cruce
			// en zona de doble vía (el span es semiabierto, por eso +1 m).
			long endExclusive = enllacPk.Value + 1L;
			if (axis.IsBuilt && endExclusive > axis.PKEnd)
			{
				endExclusive = axis.PKEnd;
			}

			if (endExclusive > palmaPk)
			{
				axis.SetTrackCount(palmaPk, endExclusive, 2);
			}
		}

		private static long? FindEnllacPk(Axis axis)
		{
			int index = 0;
			while (index < axis.Stations.Count)
			{
				StationOnAxis placement = axis.Stations[index];
				string name = placement.Station.Name ?? string.Empty;
				string avr = placement.Station.Avr ?? string.Empty;

				// Enllaç aparece como "Enllaç" / avr con prefijo EL (p. ej. ELÁ).
				if (name.IndexOf("Enlla", StringComparison.OrdinalIgnoreCase) >= 0
					|| avr.StartsWith("EL", StringComparison.OrdinalIgnoreCase))
				{
					return placement.PK;
				}

				index++;
			}

			return null;
		}

		private static StationOnAxis? FindPlacementByStationId(Axis axis, string stationId)
		{
			int index = 0;
			while (index < axis.Stations.Count)
			{
				if (string.Equals(axis.Stations[index].Station.Id, stationId, StringComparison.Ordinal))
				{
					return axis.Stations[index];
				}

				index++;
			}

			return null;
		}
	}
}

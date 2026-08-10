using Diamond.Motion;
using ProjectAsimilation = Diamond.Project.Asimilation;
using ProjectStationInfo = Diamond.Project.StationInfo;

namespace Tourmaline26.Services.TourmalineExperience
{
	/// <summary>
	/// Mapeo hardcodeado de asimilación Diamond → path de Tourmaline Experience (SFM).
	/// </summary>
	public static class ExperienceRoutePathMap
	{
		/// <summary>
		/// Resuelve el identificador de ruta para el simulador.
		/// <list type="bullet">
		/// <item>Vista T3+T2: T21 (impar/ascendente), T22 (par/descendente).</item>
		/// <item>Destino Inca: T11.</item>
		/// <item>Inca → Palma: T12.</item>
		/// <item>Resto: T31 (impar/ascendente), T32 (par/descendente).</item>
		/// </list>
		/// </summary>
		public static string Resolve(ProjectAsimilation asimilation)
		{
			if (asimilation is null)
			{
				return "T31";
			}

			bool ascending = asimilation.Sense == CirculationSense.IncreasingPk;
			string viewId = asimilation.ViewId ?? string.Empty;
			string pathSig = asimilation.PathSignature ?? string.Empty;

			// 1) Corredor multi-eje Palma – Sa Pobla (T3 + T2).
			if (IsT3PlusT2(viewId) || IsT3PlusT2(pathSig))
			{
				return ascending ? "T21" : "T22";
			}

			// 2) Hacia Inca.
			if (IsInca(asimilation.Destination))
			{
				return "T11";
			}

			// 3) Desde Inca hacia Palma.
			if (IsInca(asimilation.Origin) && IsPalma(asimilation.Destination))
			{
				return "T12";
			}

			// 4) Resto (p. ej. Palma–Manacor y simétricos).
			return ascending ? "T31" : "T32";
		}

		private static bool IsT3PlusT2(string viewOrPath)
		{
			if (string.IsNullOrWhiteSpace(viewOrPath))
			{
				return false;
			}

			// Normalizar: "T3+T2", "T3|T2", "t3+t2", etc.
			string normalized = viewOrPath.Trim().ToUpperInvariant()
				.Replace(' ', '+')
				.Replace('|', '+')
				.Replace(',', '+')
				.Replace(';', '+');

			if (normalized.Contains("T3+T2", StringComparison.Ordinal)
				|| normalized.Contains("T2+T3", StringComparison.Ordinal))
			{
				return true;
			}

			// Conjunto de ejes T3 y T2 (y solo esos, o al menos ambos presentes).
			string[] parts = normalized.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			bool hasT3 = false;
			bool hasT2 = false;
			int i = 0;
			while (i < parts.Length)
			{
				if (parts[i] == "T3")
				{
					hasT3 = true;
				}
				else if (parts[i] == "T2")
				{
					hasT2 = true;
				}

				i++;
			}

			return hasT3 && hasT2;
		}

		private static bool IsInca(ProjectStationInfo station)
		{
			return MatchesStation(station, "INC", "INCA", "17");
		}

		private static bool IsPalma(ProjectStationInfo station)
		{
			return MatchesStation(station, "PMI", "PALMA", "01", "40");
		}

		private static bool MatchesStation(ProjectStationInfo station, params string[] tokens)
		{
			if (station is null)
			{
				return false;
			}

			string id = (station.Id ?? string.Empty).Trim();
			string avr = (station.Avr ?? string.Empty).Trim();
			string name = (station.Name ?? string.Empty).Trim();

			int i = 0;
			while (i < tokens.Length)
			{
				string t = tokens[i];
				if (id.Equals(t, StringComparison.OrdinalIgnoreCase)
					|| avr.Equals(t, StringComparison.OrdinalIgnoreCase)
					|| name.Equals(t, StringComparison.OrdinalIgnoreCase)
					|| name.Contains(t, StringComparison.OrdinalIgnoreCase))
				{
					// "Inca" no debe casar con "Pont d'Inca" solo por Contains de "INC"
					// si el token es INC y avr es pdi — arriba ya exige Equals en avr/id.
					// Contains en name solo para "Inca"/"Palma" como palabra.
					if (t.Length <= 3
						&& !id.Equals(t, StringComparison.OrdinalIgnoreCase)
						&& !avr.Equals(t, StringComparison.OrdinalIgnoreCase))
					{
						// Token corto (id/avr): no usar Contains en name.
						i++;
						continue;
					}

					if (t.Equals("INCA", StringComparison.OrdinalIgnoreCase)
						|| t.Equals("INC", StringComparison.OrdinalIgnoreCase))
					{
						// Evitar Pont d'Inca / Pont d'Inca Nou.
						if (name.Contains("Pont", StringComparison.OrdinalIgnoreCase))
						{
							i++;
							continue;
						}

						if (name.Equals("Inca", StringComparison.OrdinalIgnoreCase)
							|| avr.Equals("INC", StringComparison.OrdinalIgnoreCase)
							|| id.Equals("17", StringComparison.OrdinalIgnoreCase)
							|| id.Equals("INC", StringComparison.OrdinalIgnoreCase))
						{
							return true;
						}

						i++;
						continue;
					}

					return true;
				}

				i++;
			}

			return false;
		}
	}
}

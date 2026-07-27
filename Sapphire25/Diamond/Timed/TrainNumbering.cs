using System;
using System.Collections.Generic;
using System.Globalization;
using Diamond.Motion;
using Diamond.Topo;

namespace Diamond.Timed
{
	/// <summary>
	/// Numeración SFM de circulaciones: mismo origen–destino (corredor) comparte serie (xx##).
	/// Sentido PK creciente → impares (…01, 03, …); PK decreciente → pares (…02, 04, …).
	/// El criterio prevalece sobre la asimilación: distintas marchas del mismo OD comparten serie.
	/// Determinista: mismo conjunto de circulaciones → mismos números.
	/// </summary>
	public static class TrainNumbering
	{
		/// <summary>
		/// Asigna <see cref="Circulation.ServiceNumber"/> a todas las circulaciones de la malla.
		/// </summary>
		public static void Assign(Mesh mesh)
		{
			if (mesh is null)
			{
				throw new ArgumentNullException(nameof(mesh));
			}

			if (mesh.Circulations.Count == 0)
			{
				return;
			}

			// Corredor (OD no dirigido) → circulaciones
			Dictionary<string, List<Circulation>> byCorridor =
				new Dictionary<string, List<Circulation>>(StringComparer.Ordinal);
			int ci = 0;
			while (ci < mesh.Circulations.Count)
			{
				Circulation c = mesh.Circulations[ci];
				string key = CorridorKey(c);
				List<Circulation>? list;
				if (!byCorridor.TryGetValue(key, out list))
				{
					list = new List<Circulation>();
					byCorridor[key] = list;
				}

				list.Add(c);
				ci++;
			}

			// Orden estable de corredores para asignar series de fallback sin colisiones.
			List<string> corridorKeys = new List<string>(byCorridor.Keys);
			corridorKeys.Sort(StringComparer.Ordinal);

			HashSet<int> usedSeries = new HashSet<int>();
			// Reservar series SFM conocidas aunque no salgan en esta malla (estabilidad).
			usedSeries.Add(44);
			usedSeries.Add(45);
			usedSeries.Add(47);
			usedSeries.Add(48);
			usedSeries.Add(49);
			usedSeries.Add(50);
			usedSeries.Add(70);

			int k = 0;
			while (k < corridorKeys.Count)
			{
				string corridor = corridorKeys[k];
				List<Circulation> trains = byCorridor[corridor];
				int series = ResolveSeriesBase(corridor, usedSeries);
				usedSeries.Add(series);

				List<Circulation> ascending = new List<Circulation>();
				List<Circulation> descending = new List<Circulation>();
				int t = 0;
				while (t < trains.Count)
				{
					Circulation c = trains[t];
					if (c.Asimilation.Sense == CirculationSense.IncreasingPk)
					{
						ascending.Add(c);
					}
					else
					{
						descending.Add(c);
					}

					t++;
				}

				SortByDepartureThenTechnicalId(ascending);
				SortByDepartureThenTechnicalId(descending);

				AssignParity(ascending, series, odd: true);
				AssignParity(descending, series, odd: false);

				k++;
			}
		}

		/// <summary>
		/// Sustituye ids técnicos de planificación (p. ej. C12-R-T3) por "tren NNNN" en errores/warnings.
		/// Llamar tras <see cref="Assign"/>.
		/// </summary>
		public static void RewriteMessageIds(Mesh mesh)
		{
			if (mesh is null)
			{
				throw new ArgumentNullException(nameof(mesh));
			}

			// technicalId / Id antiguo → etiqueta "tren NNNN"
			List<KeyValuePair<string, string>> replacements = new List<KeyValuePair<string, string>>();
			int ci = 0;
			while (ci < mesh.Circulations.Count)
			{
				Circulation c = mesh.Circulations[ci];
				if (c.ServiceNumber > 0)
				{
					string label = "tren " + c.ServiceNumber.ToString(CultureInfo.InvariantCulture);
					if (!string.IsNullOrEmpty(c.TechnicalId)
						&& !string.Equals(c.TechnicalId, label, StringComparison.Ordinal))
					{
						replacements.Add(new KeyValuePair<string, string>(c.TechnicalId, label));
					}

					// Por si el mensaje ya dice solo el número sin prefijo
					string num = c.ServiceNumber.ToString(CultureInfo.InvariantCulture);
					// No reescribir números sueltos en ventanas horarias; solo ids técnicos.
				}

				ci++;
			}

			if (replacements.Count == 0)
			{
				return;
			}

			// Sustituir primero los ids más largos (evita solapes parciales).
			replacements.Sort(static (a, b) => b.Key.Length.CompareTo(a.Key.Length));

			mesh.RewriteMessages(message =>
			{
				if (string.IsNullOrEmpty(message))
				{
					return message;
				}

				string result = message;
				int r = 0;
				while (r < replacements.Count)
				{
					KeyValuePair<string, string> pair = replacements[r];
					if (result.IndexOf(pair.Key, StringComparison.Ordinal) >= 0)
					{
						// Evitar "tren tren 4901" si ya se prefijó.
						result = result.Replace("tren " + pair.Key, pair.Value, StringComparison.Ordinal);
						result = result.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
					}

					r++;
				}

				return result;
			});
		}

		/// <summary>
		/// Clave de corredor: par de estaciones no dirigido (AVR/id canónico ordenado).
		/// </summary>
		public static string CorridorKey(Circulation circulation)
		{
			if (circulation is null)
			{
				throw new ArgumentNullException(nameof(circulation));
			}

			Station origin = circulation.Asimilation.Origin.Station;
			Station destination = circulation.Asimilation.Destination.Station;
			return CorridorKey(origin, destination);
		}

		public static string CorridorKey(Station a, Station b)
		{
			if (a is null)
			{
				throw new ArgumentNullException(nameof(a));
			}

			if (b is null)
			{
				throw new ArgumentNullException(nameof(b));
			}

			string ka = StationKey(a);
			string kb = StationKey(b);
			if (string.CompareOrdinal(ka, kb) <= 0)
			{
				return ka + "\u001f" + kb;
			}

			return kb + "\u001f" + ka;
		}

		/// <summary>
		/// Prefijo de serie SFM (dos dígitos) para un corredor, o 0 si no es conocido.
		/// </summary>
		public static int TryKnownSeriesBase(string corridorKey)
		{
			if (string.IsNullOrEmpty(corridorKey))
			{
				return 0;
			}

			// Claves normalizadas avr:X / id:Y
			if (ContainsPair(corridorKey, "avr:MAN", "avr:PMI")
				|| ContainsPair(corridorKey, "avr:MAN", "id:01")
				|| ContainsPair(corridorKey, "avr:MAN", "id:24"))
			{
				// Laborables clásicos Palma–Manacor
				return 49;
			}

			if (ContainsPair(corridorKey, "avr:PMI", "avr:SPB")
				|| ContainsPair(corridorKey, "id:01", "avr:SPB")
				|| ContainsPair(corridorKey, "id:33", "avr:PMI"))
			{
				return 47;
			}

			if (ContainsPair(corridorKey, "avr:INC", "avr:PMI")
				|| ContainsPair(corridorKey, "avr:INC", "id:01")
				|| ContainsPair(corridorKey, "id:17", "avr:PMI"))
			{
				return 45;
			}

			if (ContainsPair(corridorKey, "avr:PMI", "avr:UIB")
				|| ContainsPair(corridorKey, "id:40", "avr:UIB")
				|| ContainsPair(corridorKey, "id:48", "avr:PMI"))
			{
				return 50;
			}

			if (ContainsPair(corridorKey, "avr:INC", "avr:SPB")
				|| ContainsPair(corridorKey, "id:17", "avr:SPB")
				|| ContainsPair(corridorKey, "id:33", "avr:INC"))
			{
				return 70;
			}

			return 0;
		}

		private static void AssignParity(List<Circulation> ordered, int series, bool odd)
		{
			int number = series * 100 + (odd ? 1 : 2);
			int index = 0;
			while (index < ordered.Count)
			{
				if (number > series * 100 + 99)
				{
					// Desbordamiento de ##: continuar en la siguiente centena libre no es SFM clásico;
					// se sigue +2 de todos modos para no fallar.
				}

				ordered[index].AssignServiceNumber(number);
				number += 2;
				index++;
			}
		}

		private static void SortByDepartureThenTechnicalId(List<Circulation> list)
		{
			list.Sort(static (a, b) =>
			{
				int c = a.Departure.CompareTo(b.Departure);
				if (c != 0)
				{
					return c;
				}

				return string.CompareOrdinal(a.TechnicalId, b.TechnicalId);
			});
		}

		private static int ResolveSeriesBase(string corridorKey, HashSet<int> usedSeries)
		{
			int known = TryKnownSeriesBase(corridorKey);
			if (known > 0)
			{
				return known;
			}

			// Fallback determinista: hash estable del corredor → serie 10..99 no reservada.
			int hash = StableHash(corridorKey);
			int candidate = 10 + (Math.Abs(hash) % 90);
			int guard = 0;
			while (usedSeries.Contains(candidate) && guard < 100)
			{
				candidate++;
				if (candidate > 99)
				{
					candidate = 10;
				}

				guard++;
			}

			return candidate;
		}

		private static bool ContainsPair(string corridorKey, string a, string b)
		{
			// corridorKey es "x\u001fy" con x<=y ordinal
			string ka = a;
			string kb = b;
			if (string.CompareOrdinal(ka, kb) > 0)
			{
				string swap = ka;
				ka = kb;
				kb = swap;
			}

			return string.Equals(corridorKey, ka + "\u001f" + kb, StringComparison.Ordinal);
		}

		private static string StationKey(Station station)
		{
			// Preferir AVR normalizado (MAN, PMI…); si no, id.
			if (!string.IsNullOrWhiteSpace(station.Avr))
			{
				return "avr:" + station.Avr.Trim().ToUpperInvariant();
			}

			if (!string.IsNullOrWhiteSpace(station.Id))
			{
				return "id:" + station.Id.Trim();
			}

			return "name:" + (station.Name ?? string.Empty).Trim().ToUpperInvariant();
		}

		private static int StableHash(string text)
		{
			// FNV-1a 32-bit
			unchecked
			{
				int hash = (int)2166136261;
				int i = 0;
				while (i < text.Length)
				{
					hash ^= text[i];
					hash *= 16777619;
					i++;
				}

				return hash;
			}
		}
	}
}

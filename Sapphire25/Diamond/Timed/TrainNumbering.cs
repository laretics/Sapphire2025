using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Diamond.Motion;
using Diamond.Topo;

namespace Diamond.Timed
{
	/// <summary>
	/// Numeración de circulaciones: mismo origen–destino (corredor) comparte patrón.
	/// Sentido PK creciente → secuencia impar (1, 3, 5…); decreciente → par (2, 4, 6…).
	/// El número es un <see cref="string"/>: <c>4901</c>, <c>P1MTX</c>, etc.
	/// Prioridad de patrón: defs <c>asim</c> del script → tablas SFM → hash numérico.
	/// </summary>
	public static class TrainNumbering
	{
		private static readonly Regex s_hashRun = new Regex("#+", RegexOptions.CultureInvariant | RegexOptions.Compiled);
		private static readonly Regex s_classicNumeric = new Regex(
			@"^(\d+)(#+)$",
			RegexOptions.CultureInvariant | RegexOptions.Compiled);

		public static void Assign(Mesh mesh)
		{
			Assign(mesh, asimilationDefs: null, dayOfWeek: null);
		}

		public static void Assign(
			Mesh mesh,
			IReadOnlyList<DemandAsimilationDef>? asimilationDefs,
			DayOfWeek? dayOfWeek)
		{
			if (mesh is null)
			{
				throw new ArgumentNullException(nameof(mesh));
			}

			if (mesh.Circulations.Count == 0)
			{
				return;
			}

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

			List<string> corridorKeys = new List<string>(byCorridor.Keys);
			corridorKeys.Sort(StringComparer.Ordinal);

			HashSet<int> usedSeriesBases = new HashSet<int>();
			usedSeriesBases.Add(44);
			usedSeriesBases.Add(45);
			usedSeriesBases.Add(47);
			usedSeriesBases.Add(48);
			usedSeriesBases.Add(49);
			usedSeriesBases.Add(50);
			usedSeriesBases.Add(70);

			HashSet<string> usedNumbers = new HashSet<string>(StringComparer.Ordinal);

			int k = 0;
			while (k < corridorKeys.Count)
			{
				string corridor = corridorKeys[k];
				List<Circulation> trains = byCorridor[corridor];
				string pattern = ResolvePattern(corridor, usedSeriesBases, asimilationDefs, dayOfWeek, trains);

				int classicBase;
				if (TryGetClassicSeriesBase(pattern, out classicBase))
				{
					usedSeriesBases.Add(classicBase);
				}

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

				AssignByPattern(ascending, pattern, odd: true, usedNumbers);
				AssignByPattern(descending, pattern, odd: false, usedNumbers);

				k++;
			}
		}

		public static void ApplyAsimilationColors(
			Mesh mesh,
			IReadOnlyList<DemandAsimilationDef>? asimilationDefs,
			DayOfWeek? dayOfWeek)
		{
			if (mesh is null || asimilationDefs is null || asimilationDefs.Count == 0)
			{
				return;
			}

			int ci = 0;
			while (ci < mesh.Circulations.Count)
			{
				Circulation c = mesh.Circulations[ci];
				if (!c.HasColor)
				{
					DemandAsimilationDef? def = FindBestDefForCorridor(c, asimilationDefs, dayOfWeek);
					if (def is not null && def.HasColor)
					{
						c.TryAssignColorFromAsimilationDef(def.Color);
					}
				}

				ci++;
			}
		}

		public static void RewriteMessageIds(Mesh mesh)
		{
			if (mesh is null)
			{
				throw new ArgumentNullException(nameof(mesh));
			}

			List<KeyValuePair<string, string>> replacements = new List<KeyValuePair<string, string>>();
			int ci = 0;
			while (ci < mesh.Circulations.Count)
			{
				Circulation c = mesh.Circulations[ci];
				if (c.HasServiceNumber)
				{
					string label = "tren " + c.ServiceNumber;
					if (!string.IsNullOrEmpty(c.TechnicalId)
						&& !string.Equals(c.TechnicalId, label, StringComparison.Ordinal)
						&& !string.Equals(c.TechnicalId, c.ServiceNumber, StringComparison.Ordinal))
					{
						replacements.Add(new KeyValuePair<string, string>(c.TechnicalId, label));
					}
				}

				ci++;
			}

			if (replacements.Count == 0)
			{
				return;
			}

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
						result = result.Replace("tren " + pair.Key, pair.Value, StringComparison.Ordinal);
						result = result.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
					}

					r++;
				}

				return result;
			});
		}

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

		public static int TryKnownSeriesBase(string corridorKey)
		{
			if (string.IsNullOrEmpty(corridorKey))
			{
				return 0;
			}

			if (ContainsPair(corridorKey, "avr:MAN", "avr:PMI")
				|| ContainsPair(corridorKey, "avr:MAN", "id:01")
				|| ContainsPair(corridorKey, "avr:MAN", "id:24"))
			{
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

		/// <summary>
		/// Expande un patrón con el contador de secuencia (1, 3, 5… o 2, 4, 6…).
		/// <c>49##</c> + 1 → <c>4901</c>; <c>P##MTX</c> + 1 → <c>P1MTX</c> (sin ceros a la izquierda
		/// en plantillas alfanuméricas).
		/// </summary>
		public static string ExpandPattern(string pattern, int sequence)
		{
			if (string.IsNullOrWhiteSpace(pattern))
			{
				return sequence.ToString(CultureInfo.InvariantCulture);
			}

			if (sequence < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(sequence));
			}

			string p = pattern.Trim();
			Match classic = s_classicNumeric.Match(p);
			if (classic.Success)
			{
				string prefix = classic.Groups[1].Value;
				int hashLen = classic.Groups[2].Value.Length;
				int prefixVal;
				if (int.TryParse(prefix, NumberStyles.Integer, CultureInfo.InvariantCulture, out prefixVal))
				{
					int scale = 1;
					int h = 0;
					while (h < hashLen)
					{
						scale *= 10;
						h++;
					}

					int full = prefixVal * scale + sequence;
					int totalWidth = prefix.Length + hashLen;
					return full.ToString("D" + totalWidth.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
				}
			}

			// Alfanumérico: sustituir cada racha de # por el número natural (P##MTX → P1MTX).
			Match hash = s_hashRun.Match(p);
			if (!hash.Success)
			{
				return p + sequence.ToString(CultureInfo.InvariantCulture);
			}

			string num = sequence.ToString(CultureInfo.InvariantCulture);
			return p.Substring(0, hash.Index) + num + p.Substring(hash.Index + hash.Length);
		}

		/// <summary>
		/// Valida y normaliza un patrón de numeración (<c>49##</c>, <c>P##MTX</c>, <c>45</c>…).
		/// </summary>
		public static bool TryParseNumberPattern(string text, out string pattern, out string? error)
		{
			pattern = string.Empty;
			error = null;
			if (string.IsNullOrWhiteSpace(text))
			{
				error = "falta el patrón de numeración (ej. 49##, P##MTX).";
				return false;
			}

			string t = text.Trim();
			// Comodines xx / ** → ##
			if (t.EndsWith("xx", StringComparison.OrdinalIgnoreCase)
				|| t.EndsWith("**", StringComparison.Ordinal))
			{
				t = t.Substring(0, t.Length - 2) + "##";
			}

			// Solo dígitos 10–99 → tratar como serie clásica NN##
			int pure;
			if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out pure)
				&& pure >= 10 && pure <= 99
				&& t.IndexOf('#') < 0)
			{
				pattern = pure.ToString(CultureInfo.InvariantCulture) + "##";
				return true;
			}

			// 4900 → 49##
			if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out pure)
				&& pure >= 1000 && pure <= 9999
				&& t.IndexOf('#') < 0)
			{
				pattern = (pure / 100).ToString(CultureInfo.InvariantCulture) + "##";
				return true;
			}

			// Debe tener al menos un #
			if (t.IndexOf('#') < 0)
			{
				error = "el patrón debe incluir '#' (ej. 49##, P##MTX) o ser una serie 10–99.";
				return false;
			}

			// Caracteres permitidos: letras, dígitos, #
			int i = 0;
			while (i < t.Length)
			{
				char c = t[i];
				if (!(char.IsLetterOrDigit(c) || c == '#' || c == '_' || c == '-'))
				{
					error = "carácter no válido en el patrón '" + text + "'.";
					return false;
				}

				i++;
			}

			pattern = t;
			return true;
		}

		/// <summary>Compat con API anterior basada solo en serie numérica.</summary>
		public static bool TryParseSeriesPattern(string text, out int seriesBase, out string? error)
		{
			seriesBase = 0;
			string pattern;
			if (!TryParseNumberPattern(text, out pattern, out error))
			{
				return false;
			}

			if (TryGetClassicSeriesBase(pattern, out seriesBase))
			{
				return true;
			}

			// Patrón alfanumérico: seriesBase 0 pero válido
			seriesBase = 0;
			error = null;
			return true;
		}

		private static void AssignByPattern(
			List<Circulation> ordered,
			string pattern,
			bool odd,
			HashSet<string> usedNumbers)
		{
			int sequence = odd ? 1 : 2;
			int index = 0;
			while (index < ordered.Count)
			{
				string number = ExpandPattern(pattern, sequence);
				// Evitar colisiones: avanzar secuencia si el número ya existe.
				int guard = 0;
				while (usedNumbers.Contains(number) && guard < 10000)
				{
					sequence += 2;
					number = ExpandPattern(pattern, sequence);
					guard++;
				}

				usedNumbers.Add(number);
				ordered[index].AssignServiceNumber(number);
				sequence += 2;
				index++;
			}
		}

		private static string ResolvePattern(
			string corridorKey,
			HashSet<int> usedSeriesBases,
			IReadOnlyList<DemandAsimilationDef>? asimilationDefs,
			DayOfWeek? dayOfWeek,
			List<Circulation> trainsOnCorridor)
		{
			if (asimilationDefs is not null && trainsOnCorridor.Count > 0)
			{
				DemandAsimilationDef? fromScript = FindBestDefForCorridor(
					trainsOnCorridor[0],
					asimilationDefs,
					dayOfWeek);
				if (fromScript is not null && fromScript.HasNumberPattern)
				{
					return fromScript.NumberPattern;
				}
			}

			int known = TryKnownSeriesBase(corridorKey);
			if (known > 0)
			{
				return known.ToString(CultureInfo.InvariantCulture) + "##";
			}

			int hash = StableHash(corridorKey);
			int candidate = 10 + (Math.Abs(hash) % 90);
			int guard = 0;
			while (usedSeriesBases.Contains(candidate) && guard < 100)
			{
				candidate++;
				if (candidate > 99)
				{
					candidate = 10;
				}

				guard++;
			}

			return candidate.ToString(CultureInfo.InvariantCulture) + "##";
		}

		private static bool TryGetClassicSeriesBase(string pattern, out int seriesBase)
		{
			seriesBase = 0;
			Match classic = s_classicNumeric.Match(pattern.Trim());
			if (!classic.Success)
			{
				return false;
			}

			int prefix;
			if (!int.TryParse(classic.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out prefix))
			{
				return false;
			}

			if (prefix < 1 || prefix > 99)
			{
				return false;
			}

			seriesBase = prefix;
			return true;
		}

		private static DemandAsimilationDef? FindBestDefForCorridor(
			Circulation sample,
			IReadOnlyList<DemandAsimilationDef> defs,
			DayOfWeek? dayOfWeek)
		{
			DemandAsimilationDef? best = null;
			int i = 0;
			while (i < defs.Count)
			{
				DemandAsimilationDef def = defs[i];
				if (DefAppliesOnDay(def, dayOfWeek) && CorridorMatchesDef(sample, def))
				{
					best = def;
				}

				i++;
			}

			return best;
		}

		private static bool DefAppliesOnDay(DemandAsimilationDef def, DayOfWeek? dayOfWeek)
		{
			if (!dayOfWeek.HasValue)
			{
				return true;
			}

			return def.Days.AppliesOn(dayOfWeek.Value);
		}

		private static bool CorridorMatchesDef(Circulation circulation, DemandAsimilationDef def)
		{
			Station origin = circulation.Asimilation.Origin.Station;
			Station destination = circulation.Asimilation.Destination.Station;
			bool forward = StationMatchesToken(origin, def.From.Text)
				&& StationMatchesToken(destination, def.To.Text);
			bool reverse = StationMatchesToken(origin, def.To.Text)
				&& StationMatchesToken(destination, def.From.Text);
			return forward || reverse;
		}

		private static bool StationMatchesToken(Station station, string token)
		{
			if (station is null || string.IsNullOrWhiteSpace(token))
			{
				return false;
			}

			string t = token.Trim();
			if (!string.IsNullOrWhiteSpace(station.Avr)
				&& string.Equals(station.Avr.Trim(), t, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (!string.IsNullOrWhiteSpace(station.Id)
				&& string.Equals(station.Id.Trim(), t, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (!string.IsNullOrWhiteSpace(station.Name)
				&& string.Equals(station.Name.Trim(), t, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			return false;
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

		private static bool ContainsPair(string corridorKey, string a, string b)
		{
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

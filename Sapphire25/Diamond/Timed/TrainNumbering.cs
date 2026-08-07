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
	/// Numeración de circulaciones (texto: <c>4901</c>, <c>P1MTX</c>…).
	/// Las defs <c>asim ORIGEN -&gt; DESTINO</c> son <strong>dirigidas</strong>: PMI→MAN ≠ MAN→PMI.
	/// Sin def de script: corredor no dirigido + impares (PK↑) / pares (PK↓) como SFM clásico.
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

			HashSet<int> usedSeriesBases = new HashSet<int>();
			usedSeriesBases.Add(44);
			usedSeriesBases.Add(45);
			usedSeriesBases.Add(47);
			usedSeriesBases.Add(48);
			usedSeriesBases.Add(49);
			usedSeriesBases.Add(50);
			usedSeriesBases.Add(70);

			HashSet<string> usedNumbers = new HashSet<string>(StringComparer.Ordinal);
			HashSet<Circulation> numbered = new HashSet<Circulation>();

			// —— Fase 1: defs dirigidas del script (cada sentido con su propio patrón) ——
			if (asimilationDefs is not null && asimilationDefs.Count > 0)
			{
				Dictionary<string, List<Circulation>> byDirected =
					new Dictionary<string, List<Circulation>>(StringComparer.Ordinal);
				int ci = 0;
				while (ci < mesh.Circulations.Count)
				{
					Circulation c = mesh.Circulations[ci];
					DemandAsimilationDef? def = FindBestDefForDirection(c, asimilationDefs, dayOfWeek);
					if (def is not null && def.HasNumberPattern)
					{
						string dkey = DirectedKey(c) + "\u001f" + def.NumberPattern;
						List<Circulation>? list;
						if (!byDirected.TryGetValue(dkey, out list))
						{
							list = new List<Circulation>();
							byDirected[dkey] = list;
						}

						list.Add(c);
					}

					ci++;
				}

				List<string> directedKeys = new List<string>(byDirected.Keys);
				directedKeys.Sort(StringComparer.Ordinal);
				int dk = 0;
				while (dk < directedKeys.Count)
				{
					List<Circulation> trains = byDirected[directedKeys[dk]];
					// Todas comparten el mismo patrón (incluido en la clave); se toma de la def.
					DemandAsimilationDef? def = FindBestDefForDirection(trains[0], asimilationDefs, dayOfWeek);
					string pattern = def is not null && def.HasNumberPattern
						? def.NumberPattern
						: "10##";

					int classicBase;
					if (TryGetClassicSeriesBase(pattern, out classicBase))
					{
						usedSeriesBases.Add(classicBase);
					}

					SortByDepartureThenTechnicalId(trains);
					// Un sentido, un patrón: secuencia 1,2,3… (no impares/pares compartidos).
					AssignByPattern(trains, pattern, startSequence: 1, step: 1, usedNumbers);
					int t = 0;
					while (t < trains.Count)
					{
						numbered.Add(trains[t]);
						t++;
					}

					dk++;
				}
			}

			// —— Fase 2: resto sin def dirigida → corredor no dirigido SFM (impar/par) ——
			Dictionary<string, List<Circulation>> byCorridor =
				new Dictionary<string, List<Circulation>>(StringComparer.Ordinal);
			int ri = 0;
			while (ri < mesh.Circulations.Count)
			{
				Circulation c = mesh.Circulations[ri];
				if (!numbered.Contains(c))
				{
					string key = CorridorKey(c);
					List<Circulation>? list;
					if (!byCorridor.TryGetValue(key, out list))
					{
						list = new List<Circulation>();
						byCorridor[key] = list;
					}

					list.Add(c);
				}

				ri++;
			}

			List<string> corridorKeys = new List<string>(byCorridor.Keys);
			corridorKeys.Sort(StringComparer.Ordinal);

			int k = 0;
			while (k < corridorKeys.Count)
			{
				string corridor = corridorKeys[k];
				List<Circulation> trains = byCorridor[corridor];
				string pattern = ResolveFallbackPattern(corridor, usedSeriesBases);

				int classicBase;
				if (TryGetClassicSeriesBase(pattern, out classicBase))
				{
					usedSeriesBases.Add(classicBase);
				}

				// Impares = avance de PK de red (PMI→SPB, PMI→MAN…);
				// pares = sentido opuesto (SPB→PMI…).
				// No usar Asimilation.Sense: en multi-eje cada OD tiene PK de ruta 0 en origen
				// y Sense siempre Increasing (todos saldrían impares).
				List<Circulation> ascending = new List<Circulation>();
				List<Circulation> descending = new List<Circulation>();
				int t = 0;
				while (t < trains.Count)
				{
					Circulation c = trains[t];
					if (IsNetworkAscendingForNumbering(c))
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

				AssignByPattern(ascending, pattern, startSequence: 1, step: 2, usedNumbers);
				AssignByPattern(descending, pattern, startSequence: 2, step: 2, usedNumbers);

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
					// Color: def dirigida; si no hay, no se hereda la del sentido contrario.
					DemandAsimilationDef? def = FindBestDefForDirection(c, asimilationDefs, dayOfWeek);
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

		/// <summary>
		/// Sentido de numeración SFM (impar): avanza en PK de eje de red.
		/// Mono-eje: coincide con <see cref="CirculationSense.IncreasingPk"/>.
		/// Multi-eje: usa <see cref="RouteView.IsNetworkPkAscending"/> (progreso neto de tramos).
		/// </summary>
		public static bool IsNetworkAscendingForNumbering(Circulation circulation)
		{
			if (circulation is null)
			{
				throw new ArgumentNullException(nameof(circulation));
			}

			Asimilation asim = circulation.Asimilation;
			RouteView view = asim.View;
			if (view.Legs.Count <= 1)
			{
				// Vista de un solo eje: PK de ruta ≡ PK de eje; Sense es fiable.
				return asim.Sense == CirculationSense.IncreasingPk;
			}

			long net = view.NetAxisPkProgress;
			if (net != 0L)
			{
				return net > 0L;
			}

			// Degenerado: caer al Sense local.
			return asim.Sense == CirculationSense.IncreasingPk;
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
			int startSequence,
			int step,
			HashSet<string> usedNumbers)
		{
			if (step < 1)
			{
				step = 1;
			}

			int sequence = startSequence;
			int index = 0;
			while (index < ordered.Count)
			{
				string number = ExpandPattern(pattern, sequence);
				// Evitar colisiones: avanzar secuencia si el número ya existe.
				int guard = 0;
				while (usedNumbers.Contains(number) && guard < 10000)
				{
					sequence += step;
					number = ExpandPattern(pattern, sequence);
					guard++;
				}

				usedNumbers.Add(number);
				ordered[index].AssignServiceNumber(number);
				sequence += step;
				index++;
			}
		}

		/// <summary>Patrón cuando no hay <c>asim</c> dirigida en el script.</summary>
		private static string ResolveFallbackPattern(string corridorKey, HashSet<int> usedSeriesBases)
		{
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

		/// <summary>Clave dirigida origen→destino (estaciones reales).</summary>
		public static string DirectedKey(Circulation circulation)
		{
			if (circulation is null)
			{
				throw new ArgumentNullException(nameof(circulation));
			}

			return StationKey(circulation.Asimilation.Origin.Station)
				+ "\u001e"
				+ StationKey(circulation.Asimilation.Destination.Station);
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

		/// <summary>
		/// Última def del script cuyo OD coincide en el mismo sentido (origen=From, destino=To).
		/// </summary>
		private static DemandAsimilationDef? FindBestDefForDirection(
			Circulation sample,
			IReadOnlyList<DemandAsimilationDef> defs,
			DayOfWeek? dayOfWeek)
		{
			DemandAsimilationDef? best = null;
			int i = 0;
			while (i < defs.Count)
			{
				DemandAsimilationDef def = defs[i];
				if (DefAppliesOnDay(def, dayOfWeek) && DirectionMatchesDef(sample, def))
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

		/// <summary>
		/// True solo si origen de la circulación = From de la def y destino = To (dirigido).
		/// </summary>
		private static bool DirectionMatchesDef(Circulation circulation, DemandAsimilationDef def)
		{
			Station origin = circulation.Asimilation.Origin.Station;
			Station destination = circulation.Asimilation.Destination.Station;
			return StationMatchesToken(origin, def.From.Text)
				&& StationMatchesToken(destination, def.To.Text);
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

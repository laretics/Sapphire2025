using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Diamond.Timed
{
	/// <summary>
	/// Parser determinista del mini-DSL de demanda ferroviaria.
	/// </summary>
	/// <remarks>
	/// <code>
	/// plan "nombre"
	/// require [both [ways]] &lt;freq&gt; &lt;from&gt; -&gt; &lt;to&gt; [ventana] [using id] [as id]
	///   stops 30s
	///   skip RLL Enllaç "Sant Joan" PSJ
	///   dwell INC 60s
	///   cross at Enllaç
	/// freq := N/h | N per hour | every N min
	/// </code>
	/// Las líneas indentadas (espacio/tab) continúan el último require.
	/// </remarks>
	public static class DemandScriptParser
	{
		public static DemandCompileResult Parse(string script)
		{
			DemandCompileResult result = new DemandCompileResult();
			if (script is null)
			{
				result.AddError("El script es null.");
				return result;
			}

			string[] lines = SplitLines(script);
			int autoId = 0;
			int lineNumber = 0;
			DemandRequirement? current = null;

			while (lineNumber < lines.Length)
			{
				int sourceLine = lineNumber + 1;
				string raw = lines[lineNumber];
				lineNumber++;

				bool indented = raw.Length > 0 && (raw[0] == ' ' || raw[0] == '\t');
				string line = StripComment(raw).Trim();
				if (line.Length == 0)
				{
					continue;
				}

				List<string> tokens;
				try
				{
					tokens = Tokenize(line);
				}
				catch (FormatException ex)
				{
					result.AddError(sourceLine, ex.Message);
					continue;
				}

				if (tokens.Count == 0)
				{
					continue;
				}

				if (indented)
				{
					if (current is null)
					{
						result.AddError(sourceLine, "línea indentada sin require previo.");
						continue;
					}

					ParseContinuation(tokens, sourceLine, current, result);
					continue;
				}

				string head = tokens[0].ToLowerInvariant();
				if (head == "plan")
				{
					current = null;
					ParsePlanLine(tokens, sourceLine, result);
				}
				else if (head == "require")
				{
					current = ParseRequireLine(tokens, sourceLine, result, ref autoId);
				}
				else
				{
					current = null;
					result.AddError(sourceLine, "palabra clave desconocida '" + tokens[0] + "' (se esperaba plan o require).");
				}
			}

			return result;
		}

		private static void ParsePlanLine(List<string> tokens, int sourceLine, DemandCompileResult result)
		{
			if (tokens.Count < 2)
			{
				result.AddError(sourceLine, "uso: plan \"nombre\"");
				return;
			}

			StringBuilder name = new StringBuilder();
			int index = 1;
			while (index < tokens.Count)
			{
				if (index > 1)
				{
					name.Append(' ');
				}

				name.Append(tokens[index]);
				index++;
			}

			result.PlanName = name.ToString();
		}

		private static DemandRequirement? ParseRequireLine(
			List<string> tokens,
			int sourceLine,
			DemandCompileResult result,
			ref int autoId)
		{
			int index = 1;
			if (index >= tokens.Count)
			{
				result.AddError(sourceLine, "require sin argumentos.");
				return null;
			}

			DemandDirection direction = DemandDirection.Forward;
			if (string.Equals(tokens[index], "both", StringComparison.OrdinalIgnoreCase))
			{
				direction = DemandDirection.BothWays;
				index++;
				if (index < tokens.Count && string.Equals(tokens[index], "ways", StringComparison.OrdinalIgnoreCase))
				{
					index++;
				}
			}

			FrequencySpec? frequency;
			string? freqError;
			if (!TryParseFrequency(tokens, ref index, out frequency, out freqError))
			{
				result.AddError(sourceLine, freqError ?? "frecuencia no válida.");
				return null;
			}

			if (index >= tokens.Count)
			{
				result.AddError(sourceLine, "falta estación de origen.");
				return null;
			}

			StationRef from = new StationRef(tokens[index]);
			index++;

			if (index >= tokens.Count || tokens[index] != "->")
			{
				result.AddError(sourceLine, "se esperaba '->' entre origen y destino.");
				return null;
			}

			index++;
			if (index >= tokens.Count)
			{
				result.AddError(sourceLine, "falta estación de destino.");
				return null;
			}

			StationRef to = new StationRef(tokens[index]);
			index++;

			TimeOnly? windowStart = null;
			TimeOnly? windowEnd = null;
			string fleetId = string.Empty;
			string id = string.Empty;
			StopPattern stops = new StopPattern();

			while (index < tokens.Count)
			{
				string token = tokens[index];
				string lower = token.ToLowerInvariant();

				if (lower == "from" && index + 3 < tokens.Count
					&& string.Equals(tokens[index + 2], "to", StringComparison.OrdinalIgnoreCase))
				{
					TimeOnly start;
					TimeOnly end;
					if (!TryParseTime(tokens[index + 1], out start))
					{
						result.AddError(sourceLine, "hora no válida '" + tokens[index + 1] + "'.");
						return null;
					}

					if (!TryParseTime(tokens[index + 3], out end))
					{
						result.AddError(sourceLine, "hora no válida '" + tokens[index + 3] + "'.");
						return null;
					}

					windowStart = start;
					windowEnd = end;
					index += 4;
					continue;
				}

				if (lower == "using" && index + 1 < tokens.Count)
				{
					fleetId = tokens[index + 1];
					index += 2;
					continue;
				}

				if (lower == "as" && index + 1 < tokens.Count)
				{
					id = tokens[index + 1];
					index += 2;
					continue;
				}

				// Continuaciones inline: stops / skip / dwell / cross
				if (lower == "stops" || lower == "skip" || lower == "dwell" || lower == "cross")
				{
					List<string> rest = tokens.GetRange(index, tokens.Count - index);
					if (!TryParseStopCommand(rest, sourceLine, stops, result))
					{
						return null;
					}

					break;
				}

				TimeOnly compactStart;
				TimeOnly compactEnd;
				if (TryParseTimeRange(token, out compactStart, out compactEnd))
				{
					windowStart = compactStart;
					windowEnd = compactEnd;
					index++;
					continue;
				}

				result.AddError(sourceLine, "token inesperado '" + token + "'.");
				return null;
			}

			if (windowStart.HasValue && windowEnd.HasValue && windowEnd.Value <= windowStart.Value)
			{
				result.AddError(sourceLine, "la ventana horaria debe tener fin posterior al inicio.");
				return null;
			}

			if (id.Length == 0)
			{
				autoId++;
				id = "R" + autoId.ToString(CultureInfo.InvariantCulture);
			}

			DemandRequirement requirement = new DemandRequirement(
				id,
				from,
				to,
				frequency!,
				direction,
				windowStart,
				windowEnd,
				fleetId,
				sourceLine,
				stops);

			result.AddRequirement(requirement);
			return requirement;
		}

		private static void ParseContinuation(
			List<string> tokens,
			int sourceLine,
			DemandRequirement current,
			DemandCompileResult result)
		{
			TryParseStopCommand(tokens, sourceLine, current.Stops, result);
		}

		private static bool TryParseStopCommand(
			List<string> tokens,
			int sourceLine,
			StopPattern stops,
			DemandCompileResult result)
		{
			if (tokens.Count == 0)
			{
				return true;
			}

			string head = tokens[0].ToLowerInvariant();

			// stops 30s | stops 30 sec
			if (head == "stops")
			{
				if (tokens.Count < 2)
				{
					result.AddError(sourceLine, "uso: stops 30s");
					return false;
				}

				TimeSpan dwell;
				if (!TryParseDuration(tokens[1], out dwell))
				{
					result.AddError(sourceLine, "duración no válida '" + tokens[1] + "' (ej. 30s, 1min).");
					return false;
				}

				stops.DefaultDwell = dwell;
				return true;
			}

			// skip A B C
			if (head == "skip")
			{
				int index = 1;
				if (index >= tokens.Count)
				{
					result.AddError(sourceLine, "uso: skip EST1 EST2 ...");
					return false;
				}

				while (index < tokens.Count)
				{
					stops.AddSkip(new StationRef(tokens[index]));
					index++;
				}

				return true;
			}

			// dwell INC 60s | dwell INC 1min
			if (head == "dwell")
			{
				if (tokens.Count < 3)
				{
					result.AddError(sourceLine, "uso: dwell ESTACIÓN 60s");
					return false;
				}

				TimeSpan dwell;
				if (!TryParseDuration(tokens[2], out dwell))
				{
					result.AddError(sourceLine, "duración no válida '" + tokens[2] + "'.");
					return false;
				}

				stops.AddOverride(new StationRef(tokens[1]), dwell);
				return true;
			}

			// cross at Enllaç
			if (head == "cross")
			{
				int index = 1;
				if (index < tokens.Count && string.Equals(tokens[index], "at", StringComparison.OrdinalIgnoreCase))
				{
					index++;
				}

				if (index >= tokens.Count)
				{
					result.AddError(sourceLine, "uso: cross at ESTACIÓN");
					return false;
				}

				stops.CrossAt = new StationRef(tokens[index]);
				return true;
			}

			result.AddError(sourceLine, "continuación desconocida '" + tokens[0] + "' (stops|skip|dwell|cross).");
			return false;
		}

		private static bool TryParseDuration(string text, out TimeSpan duration)
		{
			duration = TimeSpan.Zero;
			if (string.IsNullOrWhiteSpace(text))
			{
				return false;
			}

			string t = text.Trim().ToLowerInvariant();
			string numberPart;
			double factorSeconds;

			if (t.EndsWith("min", StringComparison.Ordinal))
			{
				numberPart = t.Substring(0, t.Length - 3);
				factorSeconds = 60.0;
			}
			else if (t.EndsWith("sec", StringComparison.Ordinal))
			{
				numberPart = t.Substring(0, t.Length - 3);
				factorSeconds = 1.0;
			}
			else if (t.EndsWith("ms", StringComparison.Ordinal))
			{
				numberPart = t.Substring(0, t.Length - 2);
				factorSeconds = 0.001;
			}
			else if (t.EndsWith("s", StringComparison.Ordinal))
			{
				numberPart = t.Substring(0, t.Length - 1);
				factorSeconds = 1.0;
			}
			else if (t.EndsWith("m", StringComparison.Ordinal))
			{
				numberPart = t.Substring(0, t.Length - 1);
				factorSeconds = 60.0;
			}
			else
			{
				// número solo → segundos
				numberPart = t;
				factorSeconds = 1.0;
			}

			double value;
			if (!double.TryParse(numberPart, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || value < 0)
			{
				return false;
			}

			duration = TimeSpan.FromSeconds(value * factorSeconds);
			return true;
		}

		private static bool TryParseFrequency(List<string> tokens, ref int index, out FrequencySpec? frequency, out string? error)
		{
			frequency = null;
			error = null;

			if (index >= tokens.Count)
			{
				error = "falta la frecuencia.";
				return false;
			}

			string t0 = tokens[index];

			if (t0.EndsWith("/h", StringComparison.OrdinalIgnoreCase) || t0.EndsWith("/H", StringComparison.Ordinal))
			{
				string numberPart = t0.Substring(0, t0.Length - 2);
				int n;
				if (!int.TryParse(numberPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n <= 0)
				{
					error = "frecuencia no válida '" + t0 + "'.";
					return false;
				}

				frequency = FrequencySpec.PerHour(n);
				index++;
				return true;
			}

			if (string.Equals(t0, "every", StringComparison.OrdinalIgnoreCase))
			{
				if (index + 2 >= tokens.Count)
				{
					error = "uso: every N min";
					return false;
				}

				int minutes;
				if (!int.TryParse(tokens[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes) || minutes <= 0)
				{
					error = "intervalo no válido '" + tokens[index + 1] + "'.";
					return false;
				}

				string unit = tokens[index + 2].ToLowerInvariant();
				if (unit != "min" && unit != "mins" && unit != "minutes")
				{
					error = "se esperaba 'min' tras el intervalo.";
					return false;
				}

				frequency = FrequencySpec.EveryMinutes(minutes);
				index += 3;
				return true;
			}

			if (index + 2 < tokens.Count
				&& string.Equals(tokens[index + 1], "per", StringComparison.OrdinalIgnoreCase)
				&& string.Equals(tokens[index + 2], "hour", StringComparison.OrdinalIgnoreCase))
			{
				int n;
				if (!int.TryParse(t0, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n <= 0)
				{
					error = "frecuencia no válida '" + t0 + "'.";
					return false;
				}

				frequency = FrequencySpec.PerHour(n);
				index += 3;
				return true;
			}

			error = "frecuencia no reconocida cerca de '" + t0 + "' (ej.: 2/h, every 40 min).";
			return false;
		}

		private static bool TryParseTime(string text, out TimeOnly time)
		{
			return TimeOnly.TryParseExact(
				text,
				new[] { "H:mm", "HH:mm", "H:mm:ss", "HH:mm:ss" },
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out time);
		}

		private static bool TryParseTimeRange(string text, out TimeOnly start, out TimeOnly end)
		{
			start = default;
			end = default;
			int dash = text.IndexOf('-');
			if (dash <= 0 || dash >= text.Length - 1)
			{
				return false;
			}

			string left = text.Substring(0, dash);
			string right = text.Substring(dash + 1);
			if (!TryParseTime(left, out start))
			{
				return false;
			}

			if (!TryParseTime(right, out end))
			{
				return false;
			}

			return true;
		}

		private static string StripComment(string line)
		{
			bool inQuotes = false;
			int index = 0;
			while (index < line.Length)
			{
				char c = line[index];
				if (c == '"')
				{
					inQuotes = !inQuotes;
				}
				else if (c == '#' && !inQuotes)
				{
					return line.Substring(0, index);
				}

				index++;
			}

			return line;
		}

		private static List<string> Tokenize(string line)
		{
			List<string> tokens = new List<string>();
			int index = 0;
			while (index < line.Length)
			{
				char c = line[index];
				if (char.IsWhiteSpace(c))
				{
					index++;
					continue;
				}

				if (c == '"')
				{
					index++;
					StringBuilder quoted = new StringBuilder();
					while (index < line.Length && line[index] != '"')
					{
						quoted.Append(line[index]);
						index++;
					}

					if (index >= line.Length)
					{
						throw new FormatException("comillas sin cerrar.");
					}

					index++;
					tokens.Add(quoted.ToString());
					continue;
				}

				if (c == '-' && index + 1 < line.Length && line[index + 1] == '>')
				{
					tokens.Add("->");
					index += 2;
					continue;
				}

				StringBuilder token = new StringBuilder();
				while (index < line.Length)
				{
					char ch = line[index];
					if (char.IsWhiteSpace(ch))
					{
						break;
					}

					if (ch == '"' || (ch == '-' && index + 1 < line.Length && line[index + 1] == '>'))
					{
						break;
					}

					token.Append(ch);
					index++;
				}

				if (token.Length > 0)
				{
					tokens.Add(token.ToString());
				}
			}

			return tokens;
		}

		private static string[] SplitLines(string script)
		{
			List<string> lines = new List<string>();
			using (System.IO.StringReader reader = new System.IO.StringReader(script))
			{
				string? line;
				while ((line = reader.ReadLine()) is not null)
				{
					lines.Add(line);
				}
			}

			return lines.ToArray();
		}
	}
}

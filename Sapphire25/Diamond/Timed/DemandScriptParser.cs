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
	/// days lab
	///   color #38bdf8
	///     require|req [both [ways]] [&lt;freq&gt;] &lt;from&gt; -&gt; &lt;to&gt; [ventana] [using id] [as id]
	///       stops 30s
	///       skip RLL Enllaç "Sant Joan" PSJ
	///       dwell INC 60s
	///       cross at Enllaç
	/// </code>
	/// Regiones de definición: una línea <c>days</c>/<c>color</c> (opcionalmente
	/// prefijada con <c>with</c>/<c>con</c>/<c>region</c>) abre un ámbito; los
	/// <c>require</c> más indentados heredan esos valores. Lo declarado en el
	/// propio require (inline o continuación) tiene prioridad.
	/// <c>delete HH:mm-HH:mm [all]</c> elimina circulaciones ya planificadas en esa franja
	/// (en orden de script; ver <see cref="DemandDeleteOp"/>).
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
			int scriptOrder = 0;
			int lineNumber = 0;
			DemandRequirement? current = null;
			int currentIndent = -1;
			List<DefinitionScope> scopes = new List<DefinitionScope>();

			while (lineNumber < lines.Length)
			{
				int sourceLine = lineNumber + 1;
				string raw = lines[lineNumber];
				lineNumber++;

				int indent = MeasureIndent(raw);
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

				// Cerrar el require abierto si la indentación no es estrictamente mayor.
				if (current is not null && indent <= currentIndent)
				{
					current = null;
					currentIndent = -1;
				}

				// Continuación del require actual (más indentada).
				if (current is not null && indent > currentIndent)
				{
					ParseContinuation(tokens, sourceLine, current, result);
					continue;
				}

				// Pop de regiones cuyo cuerpo ya terminó (indent <= scope.Indent).
				while (scopes.Count > 0 && scopes[scopes.Count - 1].Indent >= indent)
				{
					scopes.RemoveAt(scopes.Count - 1);
				}

				string head = tokens[0].ToLowerInvariant();
				if (head == "plan")
				{
					if (indent > 0)
					{
						result.AddError(sourceLine, "'plan' solo se admite al nivel raíz (sin indentar).");
						continue;
					}

					ParsePlanLine(tokens, sourceLine, result);
					continue;
				}

				if (head == "require" || head == "req")
				{
					ServiceDays defaultDays;
					string defaultColor;
					ResolveScopeDefaults(scopes, out defaultDays, out defaultColor);
					current = ParseRequireLine(
						tokens,
						sourceLine,
						result,
						ref autoId,
						ref scriptOrder,
						defaultDays,
						defaultColor);
					currentIndent = current is null ? -1 : indent;
					continue;
				}

				if (head == "delete" || head == "del")
				{
					ServiceDays defaultDays;
					string defaultColor;
					ResolveScopeDefaults(scopes, out defaultDays, out defaultColor);
					ParseDeleteLine(tokens, sourceLine, result, ref scriptOrder, defaultDays);
					current = null;
					currentIndent = -1;
					continue;
				}

				if (LooksLikeScopeHeader(tokens))
				{
					DefinitionScope? scope;
					if (!TryParseScopeHeader(tokens, indent, sourceLine, result, out scope) || scope is null)
					{
						continue;
					}

					scopes.Add(scope);
					continue;
				}

				result.AddError(
					sourceLine,
					"palabra clave desconocida '" + tokens[0]
					+ "' (se esperaba plan, require/req, delete, o región days|color|with|con|region).");
			}

			return result;
		}

		/// <summary>
		/// Cuenta la indentación (espacios; tab = 4) al inicio de la línea cruda.
		/// </summary>
		private static int MeasureIndent(string raw)
		{
			if (string.IsNullOrEmpty(raw))
			{
				return 0;
			}

			int n = 0;
			int i = 0;
			while (i < raw.Length)
			{
				char c = raw[i];
				if (c == ' ')
				{
					n++;
				}
				else if (c == '\t')
				{
					n += 4;
				}
				else
				{
					break;
				}

				i++;
			}

			return n;
		}

		private static void ResolveScopeDefaults(
			List<DefinitionScope> scopes,
			out ServiceDays days,
			out string color)
		{
			days = ServiceDays.All;
			color = string.Empty;
			int i = 0;
			while (i < scopes.Count)
			{
				DefinitionScope s = scopes[i];
				if (s.Days is not null)
				{
					days = s.Days;
				}

				if (s.Color is not null)
				{
					color = s.Color;
				}

				i++;
			}
		}

		private static bool LooksLikeScopeHeader(List<string> tokens)
		{
			if (tokens.Count == 0)
			{
				return false;
			}

			string head = tokens[0].ToLowerInvariant();
			if (head == "with" || head == "con" || head == "region")
			{
				return true;
			}

			return head == "days" || head == "on" || head == "color" || head == "colour";
		}

		private static bool TryParseScopeHeader(
			List<string> tokens,
			int indent,
			int sourceLine,
			DemandCompileResult result,
			out DefinitionScope? scope)
		{
			scope = null;
			int index = 0;
			string head = tokens[0].ToLowerInvariant();
			if (head == "with" || head == "con" || head == "region")
			{
				index = 1;
				if (index >= tokens.Count)
				{
					result.AddError(sourceLine, "uso: with days lab [color #rrggbb] …");
					return false;
				}
			}

			ServiceDays? days = null;
			string? color = null;
			bool any = false;

			while (index < tokens.Count)
			{
				string lower = tokens[index].ToLowerInvariant();
				if (lower == "days" || lower == "on")
				{
					ServiceDays parsedDays;
					int consumed;
					string? dayError;
					if (!ServiceDays.TryParse(tokens, index + 1, out parsedDays, out consumed, out dayError))
					{
						result.AddError(sourceLine, dayError ?? "días no válidos.");
						return false;
					}

					days = parsedDays;
					any = true;
					index += 1 + consumed;
					continue;
				}

				if (lower == "color" || lower == "colour")
				{
					if (index + 1 >= tokens.Count)
					{
						result.AddError(sourceLine, "uso: color #rrggbb");
						return false;
					}

					string? normalized;
					string? colorError;
					if (!TryNormalizeColor(tokens[index + 1], out normalized, out colorError) || normalized is null)
					{
						result.AddError(sourceLine, colorError ?? "color no válido.");
						return false;
					}

					color = normalized;
					any = true;
					index += 2;
					continue;
				}

				result.AddError(
					sourceLine,
					"en una región solo se admiten days|on|color|colour (token inesperado '"
					+ tokens[index] + "').");
				return false;
			}

			if (!any)
			{
				result.AddError(sourceLine, "región vacía: indique days y/o color.");
				return false;
			}

			scope = new DefinitionScope(indent, days, color);
			return true;
		}

		private sealed class DefinitionScope
		{
			public DefinitionScope(int indent, ServiceDays? days, string? color)
			{
				Indent = indent;
				Days = days;
				Color = color;
			}

			public int Indent { get; }

			/// <summary>Null = no redefine días en este nivel.</summary>
			public ServiceDays? Days { get; }

			/// <summary>Null = no redefine color en este nivel.</summary>
			public string? Color { get; }
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

		private static void ParseDeleteLine(
			List<string> tokens,
			int sourceLine,
			DemandCompileResult result,
			ref int scriptOrder,
			ServiceDays defaultDays)
		{
			// delete HH:mm-HH:mm [all]
			// delete from HH:mm to HH:mm [all]
			// del … (alias)
			int index = 1;
			if (index >= tokens.Count)
			{
				result.AddError(sourceLine, "uso: delete HH:mm-HH:mm [all]");
				return;
			}

			TimeOnly start;
			TimeOnly end;

			string t0 = tokens[index];
			if (string.Equals(t0, "from", StringComparison.OrdinalIgnoreCase)
				&& index + 3 < tokens.Count
				&& string.Equals(tokens[index + 2], "to", StringComparison.OrdinalIgnoreCase))
			{
				if (!TryParseTime(tokens[index + 1], out start))
				{
					result.AddError(sourceLine, "hora no válida '" + tokens[index + 1] + "'.");
					return;
				}

				if (!TryParseTime(tokens[index + 3], out end))
				{
					result.AddError(sourceLine, "hora no válida '" + tokens[index + 3] + "'.");
					return;
				}

				index += 4;
			}
			else
			{
				if (!TryParseTimeWindow(t0, out start, out end) || !t0.Contains('-'))
				{
					// Exigir rango explícito (no ventana de 1 h por una sola hora).
					result.AddError(sourceLine, "uso: delete HH:mm-HH:mm [all] (se esperaba un rango horario).");
					return;
				}

				index++;
			}

			if (end <= start)
			{
				result.AddError(sourceLine, "la franja de delete debe tener fin posterior al inicio.");
				return;
			}

			bool all = false;
			if (index < tokens.Count)
			{
				string flag = tokens[index].ToLowerInvariant();
				if (flag == "all" || flag == "any" || flag == "overlap" || flag == "journey")
				{
					all = true;
					index++;
				}
				else
				{
					result.AddError(sourceLine, "token inesperado '" + tokens[index] + "' tras delete (opcional: all).");
					return;
				}
			}

			if (index < tokens.Count)
			{
				result.AddError(sourceLine, "token inesperado '" + tokens[index] + "'.");
				return;
			}

			DemandDeleteOp op = new DemandDeleteOp(
				start,
				end,
				all,
				sourceLine,
				scriptOrder,
				defaultDays);
			scriptOrder++;
			result.AddDelete(op);
		}

		private static DemandRequirement? ParseRequireLine(
			List<string> tokens,
			int sourceLine,
			DemandCompileResult result,
			ref int autoId,
			ref int scriptOrder,
			ServiceDays defaultDays,
			string defaultColor)
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

			// Frecuencia opcional: si no hay token de cadencia, un solo tren (FrequencySpec.Once).
			FrequencySpec frequency = FrequencySpec.Once();
			if (LooksLikeFrequency(tokens, index))
			{
				FrequencySpec? parsedFreq;
				string? freqError;
				if (!TryParseFrequency(tokens, ref index, out parsedFreq, out freqError) || parsedFreq is null)
				{
					result.AddError(sourceLine, freqError ?? "frecuencia no válida.");
					return null;
				}

				frequency = parsedFreq;
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
			// Herencia de regiones; lo inline/continuación puede sobrescribir.
			string color = defaultColor ?? string.Empty;
			StopPattern stops = new StopPattern();
			ServiceDays serviceDays = defaultDays ?? ServiceDays.All;

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

				// days lab | days mon-fri | days lun mar ...
				if (lower == "days" || lower == "on")
				{
					ServiceDays parsedDays;
					int consumed;
					string? dayError;
					if (!ServiceDays.TryParse(tokens, index + 1, out parsedDays, out consumed, out dayError))
					{
						result.AddError(sourceLine, dayError ?? "días no válidos.");
						return null;
					}

					serviceDays = parsedDays;
					index += 1 + consumed;
					continue;
				}

				// Continuaciones inline: stops / skip / dwell / cross / color
				if (lower == "stops" || lower == "skip" || lower == "dwell" || lower == "cross")
				{
					List<string> rest = tokens.GetRange(index, tokens.Count - index);
					if (!TryParseStopCommand(rest, sourceLine, stops, result))
					{
						return null;
					}

					break;
				}

				if (lower == "color" || lower == "colour")
				{
					if (index + 1 >= tokens.Count)
					{
						result.AddError(sourceLine, "uso: color #rrggbb");
						return null;
					}

					string? normalizedInline;
					string? colorErrorInline;
					if (!TryNormalizeColor(tokens[index + 1], out normalizedInline, out colorErrorInline)
						|| normalizedInline is null)
					{
						result.AddError(sourceLine, colorErrorInline ?? "color no válido.");
						return null;
					}

					color = normalizedInline;
					index += 2;
					continue;
				}

				TimeOnly compactStart;
				TimeOnly compactEnd;
				if (TryParseTimeWindow(token, out compactStart, out compactEnd))
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
				stops,
				serviceDays,
				color,
				scriptOrder);
			scriptOrder++;

			result.AddRequirement(requirement);
			return requirement;
		}

		private static void ParseContinuation(
			List<string> tokens,
			int sourceLine,
			DemandRequirement current,
			DemandCompileResult result)
		{
			if (tokens.Count == 0)
			{
				return;
			}

			string head = tokens[0].ToLowerInvariant();
			if (head == "days" || head == "on")
			{
				ServiceDays parsedDays;
				int consumed;
				string? dayError;
				if (!ServiceDays.TryParse(tokens, 1, out parsedDays, out consumed, out dayError))
				{
					result.AddError(sourceLine, dayError ?? "días no válidos.");
					return;
				}

				current.ServiceDays = parsedDays;
				return;
			}

			if (head == "color" || head == "colour")
			{
				if (tokens.Count < 2)
				{
					result.AddError(sourceLine, "uso: color #rrggbb");
					return;
				}

				string? normalized;
				string? colorError;
				if (!TryNormalizeColor(tokens[1], out normalized, out colorError) || normalized is null)
				{
					result.AddError(sourceLine, colorError ?? "color no válido.");
					return;
				}

				current.Color = normalized;
				return;
			}

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

			result.AddError(sourceLine, "continuación desconocida '" + tokens[0] + "' (stops|skip|dwell|cross|days|color).");
			return false;
		}

		/// <summary>
		/// Normaliza a <c>#rrggbb</c> (minúsculas). Acepta <c>#rgb</c>, <c>#rrggbb</c>,
		/// hex sin almohadilla y nombres CSS básicos.
		/// </summary>
		internal static bool TryNormalizeColor(string text, out string? color, out string? error)
		{
			color = null;
			error = null;
			if (string.IsNullOrWhiteSpace(text))
			{
				error = "falta el valor de color.";
				return false;
			}

			string t = text.Trim();
			string lower = t.ToLowerInvariant();

			// Nombres CSS básicos
			string? named = NamedCssColor(lower);
			if (named is not null)
			{
				color = named;
				return true;
			}

			string hex = lower;
			if (hex.StartsWith("#", StringComparison.Ordinal))
			{
				hex = hex.Substring(1);
			}

			if (hex.Length == 3
				&& IsHexDigit(hex[0]) && IsHexDigit(hex[1]) && IsHexDigit(hex[2]))
			{
				// #rgb → #rrggbb
				color = "#"
					+ hex[0] + hex[0]
					+ hex[1] + hex[1]
					+ hex[2] + hex[2];
				return true;
			}

			if (hex.Length == 6
				&& IsAllHex(hex))
			{
				color = "#" + hex;
				return true;
			}

			if (hex.Length == 8
				&& IsAllHex(hex))
			{
				// #rrggbbaa → se ignora alpha para stroke SVG simple
				color = "#" + hex.Substring(0, 6);
				return true;
			}

			error = "color no válido '" + text + "' (ej. #38bdf8, #f00, red, aa00aa).";
			return false;
		}

		private static string? NamedCssColor(string lower)
		{
			switch (lower)
			{
				case "red": return "#ff0000";
				case "green": return "#008000";
				case "blue": return "#0000ff";
				case "yellow": return "#ffff00";
				case "orange": return "#ffa500";
				case "purple": return "#800080";
				case "pink": return "#ffc0cb";
				case "cyan": return "#00ffff";
				case "magenta": return "#ff00ff";
				case "white": return "#ffffff";
				case "black": return "#000000";
				case "gray":
				case "grey": return "#808080";
				case "lime": return "#00ff00";
				case "teal": return "#008080";
				case "navy": return "#000080";
				case "maroon": return "#800000";
				case "olive": return "#808000";
				case "silver": return "#c0c0c0";
				case "aqua": return "#00ffff";
				case "fuchsia": return "#ff00ff";
				default: return null;
			}
		}

		private static bool IsAllHex(string s)
		{
			int i = 0;
			while (i < s.Length)
			{
				if (!IsHexDigit(s[i]))
				{
					return false;
				}

				i++;
			}

			return s.Length > 0;
		}

		private static bool IsHexDigit(char c)
		{
			return (c >= '0' && c <= '9')
				|| (c >= 'a' && c <= 'f')
				|| (c >= 'A' && c <= 'F');
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

		/// <summary>
		/// True si el token en <paramref name="index"/> inicia una frecuencia
		/// (<c>N/h</c>, <c>every N min</c>, <c>N per hour</c>).
		/// </summary>
		private static bool LooksLikeFrequency(List<string> tokens, int index)
		{
			if (tokens is null || index >= tokens.Count)
			{
				return false;
			}

			string t0 = tokens[index];
			if (t0.EndsWith("/h", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (string.Equals(t0, "every", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (index + 2 < tokens.Count
				&& string.Equals(tokens[index + 1], "per", StringComparison.OrdinalIgnoreCase)
				&& (string.Equals(tokens[index + 2], "hour", StringComparison.OrdinalIgnoreCase)
					|| string.Equals(tokens[index + 2], "hours", StringComparison.OrdinalIgnoreCase)))
			{
				int n;
				if (int.TryParse(t0, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n > 0)
				{
					return true;
				}
			}

			return false;
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
				&& (string.Equals(tokens[index + 2], "hour", StringComparison.OrdinalIgnoreCase)
					|| string.Equals(tokens[index + 2], "hours", StringComparison.OrdinalIgnoreCase)))
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

		/// <summary>
		/// Ventana horaria compacta:
		/// <c>06:00-22:00</c> → inicio y fin explícitos;
		/// <c>5:35</c> → inicio y fin = inicio + 1 h (p. ej. 5:35-6:35).
		/// </summary>
		private static bool TryParseTimeWindow(string text, out TimeOnly start, out TimeOnly end)
		{
			start = default;
			end = default;
			if (string.IsNullOrWhiteSpace(text))
			{
				return false;
			}

			int dash = text.IndexOf('-');
			if (dash > 0 && dash < text.Length - 1)
			{
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

			// Una sola hora: ventana de 1 hora [t, t+1h]
			if (!TryParseTime(text, out start))
			{
				return false;
			}

			end = AddOneHourClamped(start);
			return true;
		}

		/// <summary>
		/// Suma 1 hora; si rebasa medianoche, cierra a 23:59 (la ventana no puede cruzar el día en el DSL).
		/// </summary>
		private static TimeOnly AddOneHourClamped(TimeOnly start)
		{
			int totalMinutes = start.Hour * 60 + start.Minute + 60;
			if (totalMinutes >= 24 * 60)
			{
				return new TimeOnly(23, 59);
			}

			return new TimeOnly(totalMinutes / 60, totalMinutes % 60, start.Second);
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
					// #rrggbb / #rgb / #rrggbbaa son colores, no comentarios.
					if (IsHexColorPrefix(line, index))
					{
						index++;
						continue;
					}

					return line.Substring(0, index);
				}

				index++;
			}

			return line;
		}

		/// <summary>
		/// True si en <paramref name="index"/> hay un literal de color hex
		/// (<c>#</c> + 3, 6 u 8 dígitos hex, no seguido de más hex).
		/// </summary>
		private static bool IsHexColorPrefix(string line, int index)
		{
			if (index >= line.Length || line[index] != '#')
			{
				return false;
			}

			int hexCount = 0;
			int i = index + 1;
			while (i < line.Length && IsHexDigit(line[i]))
			{
				hexCount++;
				i++;
			}

			if (hexCount != 3 && hexCount != 6 && hexCount != 8)
			{
				return false;
			}

			// Si el siguiente carácter es alfanumérico no hex, no es un color limpio.
			// Tras hex solo dígitos a-f, cualquier otro char (espacio, fin, etc.) es OK.
			return true;
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Diamond.Motion;

namespace Diamond.Timed
{
	/// <summary>
	/// Parser determinista del mini-DSL de demanda ferroviaria.
	/// </summary>
	/// <remarks>
	/// <code>
	/// include toposfm227
	/// plan "nombre"
	/// notes "descripción o observaciones del plan"
	/// train default "Modelo" accel 0.9 brake 0.8 vmax 160
	/// train s3300 "Civia" accel 0.85 brake 0.75 vmax 120
	/// days lab
	///   color #38bdf8
	///     require|req [both [ways]] [&lt;freq&gt;] &lt;from&gt; -&gt; &lt;to&gt; [ventana] [using id] [as id]
	///       stops 30s
	///       skip RLL Enllaç "Sant Joan" PSJ
	///       dwell INC 60s
	///       cross at Enllaç
	/// </code>
	/// <c>include [topo] nombre</c> declara la topología Diamond (XML) que usará el plan.
	/// Basta el nombre (<c>include toposfm227</c>); se asume extensión <c>.xml</c> salvo que
	/// la ruta ya la lleve o sea un path explícito con <c>.xml</c>.
	/// Se carga al compilar (ver <see cref="Plan.CompileDemand"/> y <see cref="TopoStorage"/>).
	/// <c>train|tren id ["nombre"] [accel N] [brake N] [vmax N]</c> define un
	/// <see cref="TrainSpecs"/> del catálogo; si no hay ninguno, el plan usa el modelo por defecto.
	/// Regiones de definición: una línea <c>days</c>/<c>color</c> (opcionalmente
	/// prefijada con <c>with</c>/<c>con</c>/<c>region</c>) abre un ámbito; los
	/// <c>require</c> y <c>asim</c> más indentados heredan esos valores.
	/// <c>asim FROM -&gt; TO numbers 49## color #hex</c> define serie SFM y color de traza
	/// por OD dirigido (PMI→MAN ≠ MAN→PMI) y días de la región.
	/// <c>delete HH:mm-HH:mm [all]</c> elimina circulaciones ya planificadas en esa franja
	/// (en orden de script; ver <see cref="DemandDeleteOp"/>).
	/// Restricciones de topología de sesión (no modifican el XML base):
	/// <c>single track A -&gt; B [on EJE]</c>, <c>tracks N A -&gt; B</c>,
	/// <c>limit 60 [km/h] A -&gt; B</c>, <c>vmax 80 A -&gt; B</c>.
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
			AsimDefBuilder? currentAsim = null;
			int currentAsimIndent = -1;
			TrainDefBuilder? currentTrain = null;
			int currentTrainIndent = -1;
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

				// Cerrar y materializar la def asim abierta.
				if (currentAsim is not null && indent <= currentAsimIndent)
				{
					CommitAsimDef(currentAsim, result);
					currentAsim = null;
					currentAsimIndent = -1;
				}

				// Cerrar y materializar el train abierto.
				if (currentTrain is not null && indent <= currentTrainIndent)
				{
					CommitTrainDef(currentTrain, result);
					currentTrain = null;
					currentTrainIndent = -1;
				}

				// Continuación del require actual (más indentada).
				if (current is not null && indent > currentIndent)
				{
					ParseContinuation(tokens, sourceLine, current, result);
					continue;
				}

				// Continuación de asim (numbers / color / days).
				if (currentAsim is not null && indent > currentAsimIndent)
				{
					ParseAsimContinuation(tokens, sourceLine, currentAsim, result);
					continue;
				}

				// Continuación de train (name / accel / brake / vmax).
				if (currentTrain is not null && indent > currentTrainIndent)
				{
					ParseTrainContinuation(tokens, sourceLine, currentTrain, result);
					continue;
				}

				// Pop de regiones cuyo cuerpo ya terminó (indent <= scope.Indent).
				while (scopes.Count > 0 && scopes[scopes.Count - 1].Indent >= indent)
				{
					scopes.RemoveAt(scopes.Count - 1);
				}

				string head = tokens[0].ToLowerInvariant();
				if (head == "include" || head == "incluir")
				{
					if (indent > 0)
					{
						result.AddError(sourceLine, "'include' solo se admite al nivel raíz (sin indentar).");
						continue;
					}

					ParseIncludeLine(tokens, sourceLine, result);
					continue;
				}

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

				if (head == "notes" || head == "note" || head == "notas" || head == "nota")
				{
					if (indent > 0)
					{
						result.AddError(sourceLine, "'notes' solo se admite al nivel raíz (sin indentar).");
						continue;
					}

					ParseNotesLine(tokens, sourceLine, result);
					continue;
				}

				if (head == "train" || head == "tren" || head == "fleet" || head == "trainspecs")
				{
					if (indent > 0)
					{
						result.AddError(sourceLine, "'train' solo se admite al nivel raíz (sin indentar).");
						continue;
					}

					if (currentAsim is not null)
					{
						CommitAsimDef(currentAsim, result);
						currentAsim = null;
						currentAsimIndent = -1;
					}

					current = null;
					currentIndent = -1;
					currentTrain = ParseTrainLine(tokens, sourceLine, result);
					currentTrainIndent = currentTrain is null ? -1 : indent;
					continue;
				}

				if (head == "require" || head == "req")
				{
					if (currentAsim is not null)
					{
						CommitAsimDef(currentAsim, result);
						currentAsim = null;
						currentAsimIndent = -1;
					}

					if (currentTrain is not null)
					{
						CommitTrainDef(currentTrain, result);
						currentTrain = null;
						currentTrainIndent = -1;
					}

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

				if (head == "asim" || head == "asimilacion" || head == "asimilación" || head == "asimilation")
				{
					if (currentAsim is not null)
					{
						CommitAsimDef(currentAsim, result);
						currentAsim = null;
						currentAsimIndent = -1;
					}

					if (currentTrain is not null)
					{
						CommitTrainDef(currentTrain, result);
						currentTrain = null;
						currentTrainIndent = -1;
					}

					current = null;
					currentIndent = -1;
					ServiceDays defaultDays;
					string defaultColor;
					ResolveScopeDefaults(scopes, out defaultDays, out defaultColor);
					currentAsim = ParseAsimLine(
						tokens,
						sourceLine,
						result,
						ref scriptOrder,
						defaultDays,
						defaultColor);
					currentAsimIndent = currentAsim is null ? -1 : indent;
					continue;
				}

				if (head == "delete" || head == "del")
				{
					if (currentAsim is not null)
					{
						CommitAsimDef(currentAsim, result);
						currentAsim = null;
						currentAsimIndent = -1;
					}

					if (currentTrain is not null)
					{
						CommitTrainDef(currentTrain, result);
						currentTrain = null;
						currentTrainIndent = -1;
					}

					ServiceDays defaultDays;
					string defaultColor;
					ResolveScopeDefaults(scopes, out defaultDays, out defaultColor);
					ParseDeleteLine(tokens, sourceLine, result, ref scriptOrder, defaultDays);
					current = null;
					currentIndent = -1;
					continue;
				}

				if (IsTopoConstraintHead(head))
				{
					if (currentAsim is not null)
					{
						CommitAsimDef(currentAsim, result);
						currentAsim = null;
						currentAsimIndent = -1;
					}

					if (currentTrain is not null)
					{
						CommitTrainDef(currentTrain, result);
						currentTrain = null;
						currentTrainIndent = -1;
					}

					ParseTopoConstraintLine(tokens, sourceLine, result, ref scriptOrder);
					current = null;
					currentIndent = -1;
					continue;
				}

				if (LooksLikeScopeHeader(tokens))
				{
					if (currentAsim is not null)
					{
						CommitAsimDef(currentAsim, result);
						currentAsim = null;
						currentAsimIndent = -1;
					}

					if (currentTrain is not null)
					{
						CommitTrainDef(currentTrain, result);
						currentTrain = null;
						currentTrainIndent = -1;
					}

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
					+ "' (se esperaba include, plan, notes, train/tren, require/req, asim, delete, single/tracks/limit/vmax, o región days|color|with|con|region).");
			}

			if (currentAsim is not null)
			{
				CommitAsimDef(currentAsim, result);
			}

			if (currentTrain is not null)
			{
				CommitTrainDef(currentTrain, result);
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

		/// <summary>
		/// <c>asim FROM -&gt; TO [numbers 49##] [color #hex] [days lab]</c>
		/// </summary>
		private static AsimDefBuilder? ParseAsimLine(
			List<string> tokens,
			int sourceLine,
			DemandCompileResult result,
			ref int scriptOrder,
			ServiceDays defaultDays,
			string defaultColor)
		{
			// tokens[0] = asim|…
			if (tokens.Count < 4)
			{
				result.AddError(
					sourceLine,
					"uso: asim ORIGEN -> DESTINO [numbers 49##] [color #rrggbb]");
				return null;
			}

			int arrowIndex = -1;
			int i = 1;
			while (i < tokens.Count)
			{
				if (tokens[i] == "->" || tokens[i] == "→")
				{
					arrowIndex = i;
					break;
				}

				i++;
			}

			if (arrowIndex < 0 || arrowIndex == 1 || arrowIndex >= tokens.Count - 1)
			{
				result.AddError(sourceLine, "uso: asim ORIGEN -> DESTINO … (falta flecha '->').");
				return null;
			}

			string fromText = JoinTokens(tokens, 1, arrowIndex - 1);
			string toText = tokens[arrowIndex + 1];
			// Destino de un solo token; si hay más tokens son atributos (numbers/color/days).
			// Si el destino es multi-palabra entre comillas ya viene como un token.

			AsimDefBuilder builder = new AsimDefBuilder(
				new StationRef(fromText),
				new StationRef(toText),
				defaultDays,
				defaultColor,
				sourceLine,
				scriptOrder);
			scriptOrder++;

			int index = arrowIndex + 2;
			while (index < tokens.Count)
			{
				if (!TryApplyAsimAttribute(tokens, ref index, sourceLine, builder, result))
				{
					return builder;
				}
			}

			return builder;
		}

		private static void ParseAsimContinuation(
			List<string> tokens,
			int sourceLine,
			AsimDefBuilder builder,
			DemandCompileResult result)
		{
			int index = 0;
			while (index < tokens.Count)
			{
				if (!TryApplyAsimAttribute(tokens, ref index, sourceLine, builder, result))
				{
					return;
				}
			}
		}

		private static bool TryApplyAsimAttribute(
			List<string> tokens,
			ref int index,
			int sourceLine,
			AsimDefBuilder builder,
			DemandCompileResult result)
		{
			if (index >= tokens.Count)
			{
				return false;
			}

			string lower = tokens[index].ToLowerInvariant();
			if (lower == "numbers" || lower == "number" || lower == "nums" || lower == "num"
				|| lower == "serie" || lower == "series" || lower == "numeracion" || lower == "numeración")
			{
				if (index + 1 >= tokens.Count)
				{
					result.AddError(sourceLine, "uso: numbers 49##");
					return false;
				}

				string pattern;
				string? err;
				if (!TrainNumbering.TryParseNumberPattern(tokens[index + 1], out pattern, out err))
				{
					result.AddError(sourceLine, err ?? "patrón de numeración no válido.");
					return false;
				}

				builder.NumberPattern = pattern;
				index += 2;
				return true;
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

				builder.Color = normalized;
				index += 2;
				return true;
			}

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

				builder.Days = parsedDays;
				index += 1 + consumed;
				return true;
			}

			result.AddError(
				sourceLine,
				"en asim solo se admiten numbers|serie|color|days (token inesperado '"
				+ tokens[index] + "').");
			return false;
		}

		private static void CommitAsimDef(AsimDefBuilder builder, DemandCompileResult result)
		{
			if (!builder.HasNumberPattern && !builder.HasColor)
			{
				result.AddError(
					builder.SourceLine,
					"asim sin numbers ni color: indique al menos 'numbers 49##' / 'P##MTX' o 'color #rrggbb'.");
				return;
			}

			DemandAsimilationDef def = new DemandAsimilationDef(
				builder.From,
				builder.To,
				builder.Days,
				builder.NumberPattern,
				builder.Color,
				builder.SourceLine,
				builder.ScriptOrder);
			result.AddAsimilationDef(def);
		}

		/// <summary>
		/// <c>train id ["nombre"] [accel N] [brake N] [vmax N]</c>
		/// · alias <c>tren</c> / <c>fleet</c> / <c>trainspecs</c>.
		/// </summary>
		private static TrainDefBuilder? ParseTrainLine(
			List<string> tokens,
			int sourceLine,
			DemandCompileResult result)
		{
			// tokens[0] = train|tren|…
			if (tokens.Count < 2)
			{
				result.AddError(sourceLine, "uso: train <id> [\"nombre\"] [accel N] [brake N] [vmax N]");
				return null;
			}

			string id = tokens[1];
			if (id.Length == 0)
			{
				result.AddError(sourceLine, "train: el identificador no puede estar vacío.");
				return null;
			}

			if (IsTrainPropertyKeyword(id) || IsReservedTrainToken(id))
			{
				result.AddError(sourceLine, "train: '" + id + "' no es un id válido (palabra reservada).");
				return null;
			}

			TrainDefBuilder builder = new TrainDefBuilder(id, sourceLine);
			int index = 2;

			// Nombre opcional: literal no-keyword, o name/nombre "…"
			if (index < tokens.Count)
			{
				string lower = tokens[index].ToLowerInvariant();
				if (lower == "name" || lower == "nombre")
				{
					if (index + 1 >= tokens.Count)
					{
						result.AddError(sourceLine, "uso: train id name \"nombre\"");
						return builder;
					}

					builder.Name = tokens[index + 1];
					index += 2;
				}
				else if (!IsTrainPropertyKeyword(tokens[index]))
				{
					builder.Name = tokens[index];
					index++;
				}
			}

			while (index < tokens.Count)
			{
				if (!TryApplyTrainProperty(tokens, ref index, sourceLine, builder, result))
				{
					return builder;
				}
			}

			return builder;
		}

		private static void ParseTrainContinuation(
			List<string> tokens,
			int sourceLine,
			TrainDefBuilder builder,
			DemandCompileResult result)
		{
			int index = 0;
			while (index < tokens.Count)
			{
				if (!TryApplyTrainProperty(tokens, ref index, sourceLine, builder, result))
				{
					return;
				}
			}
		}

		private static bool TryApplyTrainProperty(
			List<string> tokens,
			ref int index,
			int sourceLine,
			TrainDefBuilder builder,
			DemandCompileResult result)
		{
			if (index >= tokens.Count)
			{
				return false;
			}

			string lower = tokens[index].ToLowerInvariant();

			if (lower == "name" || lower == "nombre")
			{
				if (index + 1 >= tokens.Count)
				{
					result.AddError(sourceLine, "uso: name \"nombre del tren\"");
					return false;
				}

				builder.Name = tokens[index + 1];
				index += 2;
				return true;
			}

			if (lower == "accel" || lower == "a" || lower == "acceleration"
				|| lower == "aceleracion" || lower == "aceleración")
			{
				double value;
				if (!TryParseTrainNumber(tokens, ref index, sourceLine, "accel", out value, result))
				{
					return false;
				}

				if (value <= 0.0)
				{
					result.AddError(sourceLine, "accel debe ser > 0 (m/s²).");
					return false;
				}

				builder.Acceleration = value;
				return true;
			}

			if (lower == "brake" || lower == "b" || lower == "freno"
				|| lower == "servicebrake" || lower == "decel")
			{
				double value;
				if (!TryParseTrainNumber(tokens, ref index, sourceLine, "brake", out value, result))
				{
					return false;
				}

				if (value <= 0.0)
				{
					result.AddError(sourceLine, "brake debe ser > 0 (m/s²).");
					return false;
				}

				builder.ServiceBrake = value;
				return true;
			}

			if (lower == "vmax" || lower == "v" || lower == "maxspeed"
				|| lower == "speed" || lower == "vel")
			{
				double value;
				if (!TryParseTrainNumber(tokens, ref index, sourceLine, "vmax", out value, result))
				{
					return false;
				}

				if (value <= 0.0)
				{
					result.AddError(sourceLine, "vmax debe ser > 0 (km/h).");
					return false;
				}

				builder.MaxSpeedKmh = value;
				return true;
			}

			result.AddError(
				sourceLine,
				"propiedad de train desconocida '" + tokens[index]
				+ "' (name|accel|brake|vmax).");
			return false;
		}

		/// <summary>
		/// Consume keyword + número [unidad opcional]. Avanza <paramref name="index"/> tras el keyword.
		/// </summary>
		private static bool TryParseTrainNumber(
			List<string> tokens,
			ref int index,
			int sourceLine,
			string keyword,
			out double value,
			DemandCompileResult result)
		{
			value = 0.0;
			// index apunta al keyword
			if (index + 1 >= tokens.Count)
			{
				result.AddError(sourceLine, "uso: " + keyword + " <número>");
				return false;
			}

			string numberToken = tokens[index + 1];
			if (!double.TryParse(numberToken, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
			{
				result.AddError(sourceLine, keyword + ": número no válido '" + numberToken + "'.");
				return false;
			}

			index += 2;
			// Unidad opcional: m/s2, km/h, …
			if (index < tokens.Count && IsTrainUnitToken(tokens[index]))
			{
				index++;
			}

			return true;
		}

		private static bool IsTrainPropertyKeyword(string token)
		{
			string lower = token.ToLowerInvariant();
			return lower == "name" || lower == "nombre"
				|| lower == "accel" || lower == "a" || lower == "acceleration"
				|| lower == "aceleracion" || lower == "aceleración"
				|| lower == "brake" || lower == "b" || lower == "freno"
				|| lower == "servicebrake" || lower == "decel"
				|| lower == "vmax" || lower == "v" || lower == "maxspeed"
				|| lower == "speed" || lower == "vel";
		}

		private static bool IsReservedTrainToken(string token)
		{
			string lower = token.ToLowerInvariant();
			return lower == "train" || lower == "tren" || lower == "fleet" || lower == "trainspecs"
				|| lower == "include" || lower == "plan" || lower == "require" || lower == "req"
				|| lower == "using" || lower == "as";
		}

		private static bool IsTrainUnitToken(string token)
		{
			string lower = token.ToLowerInvariant();
			return lower == "m/s2" || lower == "m/s²" || lower == "ms-2" || lower == "ms2"
				|| lower == "km/h" || lower == "kmh" || lower == "kph" || lower == "km";
		}

		private static void CommitTrainDef(TrainDefBuilder builder, DemandCompileResult result)
		{
			if (builder.Id.Length == 0)
			{
				result.AddError(builder.SourceLine, "train sin identificador.");
				return;
			}

			int i = 0;
			while (i < result.Fleet.Count)
			{
				if (string.Equals(result.Fleet[i].Id, builder.Id, StringComparison.OrdinalIgnoreCase))
				{
					result.AddError(
						builder.SourceLine,
						"train id duplicado '" + builder.Id + "'.");
					return;
				}

				i++;
			}

			TrainSpecs defaults = TrainSpecs.DefaultModel;
			string name = builder.Name;
			if (name.Length == 0)
			{
				name = builder.Id;
			}

			double accel = builder.Acceleration ?? defaults.Acceleration;
			double brake = builder.ServiceBrake ?? defaults.ServiceBrake;
			double vmax = builder.MaxSpeedKmh ?? defaults.MaxSpeedKmh;

			try
			{
				result.AddFleet(new TrainSpecs(builder.Id, name, accel, brake, vmax));
			}
			catch (ArgumentException ex)
			{
				result.AddError(builder.SourceLine, "train '" + builder.Id + "': " + ex.Message);
			}
		}

		private sealed class TrainDefBuilder
		{
			private readonly string mvarId;
			private string mvarName;
			private double? mvarAcceleration;
			private double? mvarServiceBrake;
			private double? mvarMaxSpeedKmh;
			private readonly int mvarSourceLine;

			public TrainDefBuilder(string id, int sourceLine)
			{
				mvarId = id ?? string.Empty;
				mvarName = string.Empty;
				mvarAcceleration = null;
				mvarServiceBrake = null;
				mvarMaxSpeedKmh = null;
				mvarSourceLine = sourceLine;
			}

			public string Id
			{
				get { return mvarId; }
			}

			public string Name
			{
				get { return mvarName; }
				set { mvarName = value ?? string.Empty; }
			}

			public double? Acceleration
			{
				get { return mvarAcceleration; }
				set { mvarAcceleration = value; }
			}

			public double? ServiceBrake
			{
				get { return mvarServiceBrake; }
				set { mvarServiceBrake = value; }
			}

			public double? MaxSpeedKmh
			{
				get { return mvarMaxSpeedKmh; }
				set { mvarMaxSpeedKmh = value; }
			}

			public int SourceLine
			{
				get { return mvarSourceLine; }
			}
		}

		private static string JoinTokens(List<string> tokens, int fromInclusive, int toInclusive)
		{
			if (fromInclusive > toInclusive)
			{
				return string.Empty;
			}

			if (fromInclusive == toInclusive)
			{
				return tokens[fromInclusive];
			}

			StringBuilder sb = new StringBuilder();
			int i = fromInclusive;
			while (i <= toInclusive)
			{
				if (i > fromInclusive)
				{
					sb.Append(' ');
				}

				sb.Append(tokens[i]);
				i++;
			}

			return sb.ToString();
		}

		private sealed class AsimDefBuilder
		{
			private readonly StationRef mvarFrom;
			private readonly StationRef mvarTo;
			private ServiceDays mvarDays;
			private string mvarNumberPattern;
			private string mvarColor;
			private readonly int mvarSourceLine;
			private readonly int mvarScriptOrder;

			public AsimDefBuilder(
				StationRef from,
				StationRef to,
				ServiceDays days,
				string defaultColor,
				int sourceLine,
				int scriptOrder)
			{
				mvarFrom = from;
				mvarTo = to;
				mvarDays = days;
				mvarNumberPattern = string.Empty;
				mvarColor = defaultColor ?? string.Empty;
				mvarSourceLine = sourceLine;
				mvarScriptOrder = scriptOrder;
			}

			public StationRef From
			{
				get { return mvarFrom; }
			}

			public StationRef To
			{
				get { return mvarTo; }
			}

			public ServiceDays Days
			{
				get { return mvarDays; }
				set { mvarDays = value; }
			}

			public string NumberPattern
			{
				get { return mvarNumberPattern; }
				set { mvarNumberPattern = value ?? string.Empty; }
			}

			public string Color
			{
				get { return mvarColor; }
				set { mvarColor = value ?? string.Empty; }
			}

			public bool HasNumberPattern
			{
				get { return mvarNumberPattern.Length > 0; }
			}

			public bool HasColor
			{
				get { return mvarColor.Length > 0; }
			}

			public int SourceLine
			{
				get { return mvarSourceLine; }
			}

			public int ScriptOrder
			{
				get { return mvarScriptOrder; }
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
			private readonly int mvarIndent;
			private readonly ServiceDays? mvarDays;
			private readonly string? mvarColor;

			public DefinitionScope(int indent, ServiceDays? days, string? color)
			{
				mvarIndent = indent;
				mvarDays = days;
				mvarColor = color;
			}

			public int Indent
			{
				get { return mvarIndent; }
			}

			/// <summary>Null = no redefine días en este nivel.</summary>
			public ServiceDays? Days
			{
				get { return mvarDays; }
			}

			/// <summary>Null = no redefine color en este nivel.</summary>
			public string? Color
			{
				get { return mvarColor; }
			}
		}

		private static void ParsePlanLine(List<string> tokens, int sourceLine, DemandCompileResult result)
		{
			if (tokens.Count < 2)
			{
				result.AddError(sourceLine, "uso: plan \"nombre\"");
				return;
			}

			result.PlanName = JoinTokensFrom(tokens, 1);
		}

		/// <summary>
		/// <c>notes "texto libre del plan"</c> · alias <c>notas</c> / <c>note</c>.
		/// </summary>
		private static void ParseNotesLine(List<string> tokens, int sourceLine, DemandCompileResult result)
		{
			if (tokens.Count < 2)
			{
				result.AddError(sourceLine, "uso: notes \"texto de observaciones del plan\"");
				return;
			}

			result.Notes = JoinTokensFrom(tokens, 1);
		}

		private static string JoinTokensFrom(List<string> tokens, int fromIndex)
		{
			StringBuilder sb = new StringBuilder();
			int index = fromIndex;
			while (index < tokens.Count)
			{
				if (sb.Length > 0)
				{
					sb.Append(' ');
				}

				sb.Append(tokens[index]);
				index++;
			}

			return sb.ToString();
		}

		/// <summary>
		/// <c>include toposfm227</c> · <c>include "ruta.xml"</c> · <c>include topo nombre</c> · <c>incluir …</c>
		/// Sin extensión se asume <c>.xml</c> (ver <see cref="TopoStorage.EnsureXmlExtension"/>).
		/// </summary>
		private static void ParseIncludeLine(List<string> tokens, int sourceLine, DemandCompileResult result)
		{
			if (tokens.Count < 2)
			{
				result.AddError(sourceLine, "uso: include nombre-topo (o include topo \"ruta.xml\").");
				return;
			}

			int index = 1;
			string marker = tokens[index].ToLowerInvariant();
			if (marker == "topo" || marker == "topologia" || marker == "topología" || marker == "topology")
			{
				index++;
			}

			if (index >= tokens.Count)
			{
				result.AddError(sourceLine, "uso: include nombre-topo (falta el nombre o la ruta del XML).");
				return;
			}

			// Una sola ruta (token ya desentrecomillado por Tokenize). Si hay más tokens, unir con espacio
			// (p. ej. rutas con espacios entre comillas rotas — normalmente es un token).
			StringBuilder path = new StringBuilder();
			while (index < tokens.Count)
			{
				if (path.Length > 0)
				{
					path.Append(' ');
				}

				path.Append(tokens[index]);
				index++;
			}

			string includePath = TopoStorage.EnsureXmlExtension(path.ToString());
			if (includePath.Length == 0)
			{
				result.AddError(sourceLine, "uso: include nombre-topo (ruta vacía).");
				return;
			}

			if (result.IncludedTopoPath.Length > 0)
			{
				if (!string.Equals(result.IncludedTopoPath, includePath, StringComparison.OrdinalIgnoreCase))
				{
					result.AddError(
						sourceLine,
						"solo se admite un include de topología; ya hay include \""
						+ result.IncludedTopoPath + "\".");
				}

				return;
			}

			result.IncludedTopoPath = includePath;
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

		private static bool IsTopoConstraintHead(string head)
		{
			return head == "single"
				|| head == "tracks"
				|| head == "track"
				|| head == "via"
				|| head == "vía"
				|| head == "vias"
				|| head == "vías"
				|| head == "limit"
				|| head == "limite"
				|| head == "límite"
				|| head == "vmax"
				|| head == "speed";
		}

		/// <summary>
		/// <c>single [track|tracks|via|vía] FROM -&gt; TO [on AXIS]</c><br/>
		/// <c>via simple FROM -&gt; TO</c><br/>
		/// <c>tracks N FROM -&gt; TO [on AXIS]</c><br/>
		/// <c>limit N [km/h|kmh] FROM -&gt; TO [on AXIS]</c><br/>
		/// <c>vmax N FROM -&gt; TO [on AXIS]</c>
		/// </summary>
		private static void ParseTopoConstraintLine(
			List<string> tokens,
			int sourceLine,
			DemandCompileResult result,
			ref int scriptOrder)
		{
			if (tokens.Count < 2)
			{
				result.AddError(
					sourceLine,
					"uso: single track A -> B | tracks 1 A -> B | limit 60 A -> B | vmax 80 A -> B");
				return;
			}

			string head = tokens[0].ToLowerInvariant();
			int index = 1;
			DemandTopoConstraintKind kind;
			int value;

			if (head == "single"
				|| head == "via"
				|| head == "vía")
			{
				// single [track|tracks|via|vía] | via simple | single
				if (index < tokens.Count)
				{
					string opt = tokens[index].ToLowerInvariant();
					if (opt == "track" || opt == "tracks" || opt == "via" || opt == "vía"
						|| opt == "simple" || opt == "unica" || opt == "única")
					{
						index++;
					}
				}

				kind = DemandTopoConstraintKind.TrackCount;
				value = 1;
			}
			else if (head == "tracks" || head == "track" || head == "vias" || head == "vías")
			{
				if (index >= tokens.Count)
				{
					result.AddError(sourceLine, "uso: tracks N ORIGEN -> DESTINO [on EJE]");
					return;
				}

				int n;
				if (!int.TryParse(tokens[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out n)
					|| n < 1)
				{
					result.AddError(sourceLine, "número de vías no válido '" + tokens[index] + "' (entero ≥ 1).");
					return;
				}

				index++;
				kind = DemandTopoConstraintKind.TrackCount;
				value = n;
			}
			else if (head == "limit" || head == "limite" || head == "límite"
				|| head == "vmax" || head == "speed")
			{
				if (index >= tokens.Count)
				{
					result.AddError(sourceLine, "uso: limit 60 [km/h] ORIGEN -> DESTINO [on EJE]");
					return;
				}

				int speed;
				if (!int.TryParse(tokens[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out speed)
					|| speed < 1)
				{
					result.AddError(sourceLine, "velocidad no válida '" + tokens[index] + "' (km/h ≥ 1).");
					return;
				}

				index++;
				// Unidad opcional.
				if (index < tokens.Count)
				{
					string unit = tokens[index].ToLowerInvariant();
					if (unit == "km/h" || unit == "kmh" || unit == "kph" || unit == "km"
						|| unit == "kmph")
					{
						index++;
					}
				}

				kind = DemandTopoConstraintKind.SpeedLimit;
				value = speed;
			}
			else
			{
				result.AddError(sourceLine, "restricción de topología no reconocida '" + tokens[0] + "'.");
				return;
			}

			StationRef? from;
			StationRef? to;
			string? axisId;
			string? spanError;
			if (!TryParseStationSpan(tokens, ref index, sourceLine, out from, out to, out axisId, out spanError)
				|| from is null
				|| to is null)
			{
				result.AddError(sourceLine, spanError ?? "uso: … ORIGEN -> DESTINO [on EJE]");
				return;
			}

			if (index < tokens.Count)
			{
				result.AddError(sourceLine, "token inesperado '" + tokens[index] + "'.");
				return;
			}

			DemandTopoConstraint constraint = new DemandTopoConstraint(
				kind,
				value,
				from,
				to,
				axisId,
				sourceLine,
				scriptOrder);
			scriptOrder++;
			result.AddTopoConstraint(constraint);
		}

		/// <summary>
		/// Lee <c>FROM -&gt; TO [on AXIS]</c> a partir de <paramref name="index"/>.
		/// </summary>
		private static bool TryParseStationSpan(
			List<string> tokens,
			ref int index,
			int sourceLine,
			out StationRef? from,
			out StationRef? to,
			out string? axisId,
			out string? error)
		{
			from = null;
			to = null;
			axisId = null;
			error = null;

			if (index >= tokens.Count)
			{
				error = "falta estación de origen.";
				return false;
			}

			from = new StationRef(tokens[index]);
			index++;

			if (index >= tokens.Count || tokens[index] != "->")
			{
				error = "se esperaba '->' entre origen y destino.";
				return false;
			}

			index++;
			if (index >= tokens.Count)
			{
				error = "falta estación de destino.";
				return false;
			}

			to = new StationRef(tokens[index]);
			index++;

			if (index < tokens.Count
				&& string.Equals(tokens[index], "on", StringComparison.OrdinalIgnoreCase)
				&& index + 1 < tokens.Count)
			{
				axisId = tokens[index + 1];
				index += 2;
			}

			return true;
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

					// Patrones de numeración SFM: 49## (comodín), no comentario.
					if (IsNumberPatternHash(line, index))
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

		/// <summary>
		/// True si el <c>#</c> en <paramref name="index"/> forma parte de un patrón
		/// de numeración tipo <c>49##</c> o <c>P##MTX</c> (letra/dígito + uno o más <c>#</c>).
		/// </summary>
		private static bool IsNumberPatternHash(string line, int index)
		{
			if (index >= line.Length || line[index] != '#')
			{
				return false;
			}

			int i = index - 1;
			while (i >= 0 && line[i] == '#')
			{
				i--;
			}

			// 49## (dígito) o P##MTX (letra) o _## (identificador)
			return i >= 0 && (char.IsLetterOrDigit(line[i]) || line[i] == '_' || line[i] == '-');
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Diamond.Rauta;
using Diamond.Topo;

namespace Diamond.Timed
{
	/// <summary>
	/// Compilador inverso: malla rauta (+ catálogo de asimilaciones del topo)
	/// → borrador de script mini-DSL de demanda.
	/// Determinista: mismo input → mismo script.
	/// </summary>
	public static class DemandInverseCompiler
	{
		public sealed class InverseCompileResult
		{
			public string Script { get; set; } = string.Empty;
			public List<string> Notes { get; } = new List<string>();
			public List<string> Warnings { get; } = new List<string>();
			public int RequirementCount { get; set; }
		}

		/// <summary>
		/// Genera un script de demanda a partir de un plan rauta y las asimilaciones del topo.
		/// </summary>
		public static InverseCompileResult Compile(
			RautaPlan plan,
			TopoAsimilationCatalog asimilations,
			TopoLayout? layout = null)
		{
			if (plan is null)
			{
				throw new ArgumentNullException(nameof(plan));
			}

			if (asimilations is null)
			{
				throw new ArgumentNullException(nameof(asimilations));
			}

			InverseCompileResult result = new InverseCompileResult();
			StringBuilder script = new StringBuilder();

			string planName = plan.Name.Length > 0 ? plan.Name : plan.Id;
			script.Append("plan \"");
			script.Append(EscapeQuotes(planName));
			script.AppendLine("\"");
			script.AppendLine("# Generado por DemandInverseCompiler (borrador editable)");
			script.AppendLine("# Los tiempos de paso cinemáticos pueden diferir de las asimilaciones Onice.");
			script.AppendLine();

			// Agrupar bloques por (OD canónica, freq, stop-signature) y detectar ida/vuelta.
			List<BlockAnalysis> analyses = new List<BlockAnalysis>();
			int bi = 0;
			while (bi < plan.Blocks.Count)
			{
				RautaBlock block = plan.Blocks[bi];
				BlockAnalysis? analysis = AnalyzeBlock(block, asimilations, layout, result);
				if (analysis is not null)
				{
					analyses.Add(analysis);
				}

				bi++;
			}

			// Emparejar ida/vuelta
			List<InferredRequirement> inferred = PairAndInfer(analyses, result);

			// Emitir en orden estable: freq, origin, dest, asm
			inferred.Sort(static (a, b) =>
			{
				int c = string.CompareOrdinal(a.DaysToken, b.DaysToken);
				if (c != 0)
				{
					return c;
				}

				c = string.CompareOrdinal(a.FromToken, b.FromToken);
				if (c != 0)
				{
					return c;
				}

				c = string.CompareOrdinal(a.ToToken, b.ToToken);
				if (c != 0)
				{
					return c;
				}

				return string.CompareOrdinal(a.RequirementId, b.RequirementId);
			});

			int ri = 0;
			while (ri < inferred.Count)
			{
				EmitRequirement(script, inferred[ri]);
				script.AppendLine();
				ri++;
			}

			result.Script = script.ToString();
			result.RequirementCount = inferred.Count;
			return result;
		}

		private static BlockAnalysis? AnalyzeBlock(
			RautaBlock block,
			TopoAsimilationCatalog asimilations,
			TopoLayout? layout,
			InverseCompileResult result)
		{
			string asmId = block.AsimilationId;
			if (asmId.Length == 0 && block.Circulations.Count > 0)
			{
				asmId = block.Circulations[0].AsimilationId ?? string.Empty;
			}

			if (asmId.Length == 0)
			{
				result.Warnings.Add("Bloque sin asm ignorado (pattern=" + block.Pattern + ").");
				return null;
			}

			if (block.Circulations.Count == 0)
			{
				result.Warnings.Add("Bloque " + asmId + " sin circulaciones.");
				return null;
			}

			TopoAsimilationTemplate? template = asimilations.Find(asmId);
			if (template is null)
			{
				result.Warnings.Add("Asimilación '" + asmId + "' no encontrada en el topo; se omite el bloque.");
				return null;
			}

			string fromName = template.OriginName;
			string toName = template.DestinationName.Length > 0 ? template.DestinationName : template.Name;
			string fromToken = ResolveStationToken(fromName, template.OriginCode, layout);
			string toToken = ResolveStationToken(toName, template.DestinationCode, layout);

			List<TimeSpan> deps = new List<TimeSpan>();
			int ci = 0;
			while (ci < block.Circulations.Count)
			{
				deps.Add(block.Circulations[ci].Departure);
				ci++;
			}

			deps.Sort();
			int medianMinutes = EstimateMedianHeadwayMinutes(deps);
			TimeSpan windowStart = deps[0];
			TimeSpan windowEnd = deps[deps.Count - 1];

			// Ventana: ampliar un poco más allá de la última salida no es necesario;
			// el require usa first–last como referencia de servicio.
			StopPatternDraft stops = InferStopPattern(template, layout, fromToken, toToken);

			string freq = block.Freq.Length > 0 ? block.Freq : (block.Circulations[0].Freq ?? "lab");
			string daysToken = FreqToDaysToken(freq);

			string pathSignature = string.Empty;
			bool multiAxis = false;
			long originRoutePk = 0L;
			long destRoutePk = 0L;
			bool hasRoutePks = false;
			if (layout is not null)
			{
				DescribePhysicalPath(
					layout,
					fromToken,
					toToken,
					out pathSignature,
					out multiAxis,
					out originRoutePk,
					out destRoutePk,
					out hasRoutePks);
			}

			BlockAnalysis analysis = new BlockAnalysis();
			analysis.AsimilationId = asmId;
			analysis.FromToken = fromToken;
			analysis.ToToken = toToken;
			analysis.FromName = fromName;
			analysis.ToName = toName;
			analysis.DaysToken = daysToken;
			analysis.Freq = freq;
			analysis.Pattern = block.Pattern;
			analysis.MedianHeadwayMinutes = medianMinutes;
			analysis.WindowStart = windowStart;
			analysis.WindowEnd = windowEnd;
			analysis.CirculationCount = deps.Count;
			analysis.Stops = stops;
			analysis.FirstDep = deps[0];
			analysis.RouteKey = CanonicalRouteKey(fromToken, toToken);
			analysis.ReverseRouteKey = CanonicalRouteKey(toToken, fromToken);
			analysis.PathSignature = pathSignature;
			analysis.OriginRoutePk = originRoutePk;
			analysis.DestRoutePk = destRoutePk;
			analysis.HasRoutePks = hasRoutePks;
			analysis.IsMultiAxis = multiAxis;
			return analysis;
		}

		private static List<InferredRequirement> PairAndInfer(List<BlockAnalysis> analyses, InverseCompileResult result)
		{
			List<InferredRequirement> list = new List<InferredRequirement>();
			bool[] used = new bool[analyses.Count];

			int i = 0;
			while (i < analyses.Count)
			{
				if (used[i])
				{
					i++;
					continue;
				}

				BlockAnalysis a = analyses[i];
				int partner = -1;
				int j = i + 1;
				while (j < analyses.Count)
				{
					if (!used[j])
					{
						BlockAnalysis b = analyses[j];
						if (string.Equals(a.DaysToken, b.DaysToken, StringComparison.Ordinal)
							&& string.Equals(a.RouteKey, b.ReverseRouteKey, StringComparison.Ordinal)
							&& string.Equals(a.ReverseRouteKey, b.RouteKey, StringComparison.Ordinal))
						{
							// Preferir mismos “familia” de patrón (prefijo numérico 44 vs 48)
							partner = j;
							break;
						}
					}

					j++;
				}

				if (partner >= 0)
				{
					BlockAnalysis b = analyses[partner];
					used[i] = true;
					used[partner] = true;

					// Orientación canónica: sentido de PK creciente en la ruta (SFM impares = ↑PK).
					// No usar orden alfabético de tokens (MAN < PMI dejaba ida = Manacor→Palma).
					BlockAnalysis forward = a;
					BlockAnalysis ret = b;
					if (!TryOrientAscending(a, b, out forward, out ret))
					{
						// Fallback: token origen menor ordinal
						forward = a;
						ret = b;
						if (string.CompareOrdinal(a.FromToken, a.ToToken) > 0)
						{
							forward = b;
							ret = a;
						}

						if (!string.Equals(forward.FromToken, ret.ToToken, StringComparison.Ordinal)
							|| !string.Equals(forward.ToToken, ret.FromToken, StringComparison.Ordinal))
						{
							if (string.CompareOrdinal(a.FromToken, b.FromToken) <= 0)
							{
								forward = a;
								ret = b;
							}
							else
							{
								forward = b;
								ret = a;
							}

							if (!string.Equals(forward.FromToken, ret.ToToken, StringComparison.Ordinal)
								|| !string.Equals(forward.ToToken, ret.FromToken, StringComparison.Ordinal))
							{
								list.Add(ToSingleRequirement(a, result));
								list.Add(ToSingleRequirement(b, result));
								i++;
								continue;
							}
						}
					}

					// Servicios dispersos (p. ej. 70x: un 7001 a las 05:35 y un 7002 a las 22:30):
					// no inventar both-ways cada 60 min todo el día; emitir dos sentidos sueltos.
					if (IsSparseBlock(forward) || IsSparseBlock(ret))
					{
						list.Add(ToSingleRequirement(forward, result));
						list.Add(ToSingleRequirement(ret, result));
						result.Notes.Add(
							"Par OD disperso (no cadencia): " + forward.AsimilationId + " y "
							+ ret.AsimilationId + " como sentidos independientes"
							+ FormatPathSuffix(
								forward.PathSignature.Length > 0 ? forward.PathSignature : ret.PathSignature,
								forward.IsMultiAxis || ret.IsMultiAxis) + ".");
					}
					else
					{
						InferredRequirement req = new InferredRequirement();
						req.RequirementId = "R-" + SanitizeId(forward.FromToken) + "-" + SanitizeId(forward.ToToken) + "-" + forward.DaysToken;
						req.BothWays = true;
						req.FromToken = forward.FromToken;
						req.ToToken = forward.ToToken;
						req.DaysToken = forward.DaysToken;
						req.HeadwayMinutes = ChooseHeadway(forward.MedianHeadwayMinutes, ret.MedianHeadwayMinutes);
						req.WindowStart = MinTime(forward.WindowStart, ret.WindowStart);
						req.WindowEnd = MaxTime(forward.WindowEnd, ret.WindowEnd);
						req.Stops = forward.Stops;
						req.PathSignature = forward.PathSignature.Length > 0 ? forward.PathSignature : ret.PathSignature;
						req.IsMultiAxis = forward.IsMultiAxis || ret.IsMultiAxis;
						req.Comment = "asm " + forward.AsimilationId + " / " + ret.AsimilationId
							+ " · n=" + forward.CirculationCount + "+" + ret.CirculationCount
							+ " · phase≈" + FormatPhase(forward.FirstDep, ret.FirstDep)
							+ FormatPathSuffix(req.PathSignature, req.IsMultiAxis);
						req.PhaseComment = "ida " + FormatClock(forward.FirstDep) + " / vuelta " + FormatClock(ret.FirstDep);
						list.Add(req);

						result.Notes.Add(
							"Emparejado both ways: " + forward.AsimilationId + " ↔ " + ret.AsimilationId
							+ " (" + forward.FromToken + "–" + forward.ToToken + ", " + forward.DaysToken + ")"
							+ FormatPathSuffix(req.PathSignature, req.IsMultiAxis) + ".");
					}
				}
				else
				{
					used[i] = true;
					list.Add(ToSingleRequirement(a, result));
				}

				i++;
			}

			return list;
		}

		/// <summary>
		/// Elige como "forward" el sentido con PK de ruta creciente (si la firma lo permite).
		/// Firma tipica: T3:0&gt;64185 (ida) vs T3:64185&gt;0 no aparece; se infiere por OriginRoutePk.
		/// </summary>
		private static bool TryOrientAscending(
			BlockAnalysis a,
			BlockAnalysis b,
			out BlockAnalysis forward,
			out BlockAnalysis ret)
		{
			forward = a;
			ret = b;

			if (!string.Equals(a.FromToken, b.ToToken, StringComparison.Ordinal)
				|| !string.Equals(a.ToToken, b.FromToken, StringComparison.Ordinal))
			{
				return false;
			}

			// OriginRoutePk menor = sale desde el extremo de PK más bajo = sentido creciente natural.
			if (a.HasRoutePks && b.HasRoutePks)
			{
				if (a.OriginRoutePk <= b.OriginRoutePk)
				{
					forward = a;
					ret = b;
				}
				else
				{
					forward = b;
					ret = a;
				}

				return true;
			}

			// Heurística SFM: hub Palma (PMI) como origen de la ida si está en el par.
			if (string.Equals(a.FromToken, "PMI", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(a.FromToken, "01", StringComparison.Ordinal))
			{
				forward = a;
				ret = b;
				return true;
			}

			if (string.Equals(b.FromToken, "PMI", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(b.FromToken, "01", StringComparison.Ordinal))
			{
				forward = b;
				ret = a;
				return true;
			}

			return false;
		}

		/// <summary>
		/// Bloque con muy pocas circulaciones o huecos enormes: no modelar como cadencia densa.
		/// </summary>
		private static bool IsSparseBlock(BlockAnalysis a)
		{
			if (a.CirculationCount <= 2)
			{
				return true;
			}

			return a.MedianHeadwayMinutes >= 6 * 60;
		}

		private static InferredRequirement ToSingleRequirement(BlockAnalysis a, InverseCompileResult result)
		{
			InferredRequirement req = new InferredRequirement();
			req.RequirementId = "R-" + SanitizeId(a.AsimilationId);
			req.BothWays = false;
			req.FromToken = a.FromToken;
			req.ToToken = a.ToToken;
			req.DaysToken = a.DaysToken;
			// Sin serie de salidas: una (o pocas) franjas; headway largo evita inventar trenes.
			int headway = a.MedianHeadwayMinutes > 0 ? a.MedianHeadwayMinutes : (24 * 60);
			if (a.CirculationCount <= 2 && headway < 12 * 60)
			{
				headway = 24 * 60;
			}

			req.HeadwayMinutes = headway;
			req.WindowStart = a.WindowStart;
			req.WindowEnd = a.WindowEnd;
			req.Stops = a.Stops;
			req.PathSignature = a.PathSignature;
			req.IsMultiAxis = a.IsMultiAxis;
			req.Comment = "asm " + a.AsimilationId + " · n=" + a.CirculationCount
				+ " · pattern " + a.Pattern
				+ FormatPathSuffix(a.PathSignature, a.IsMultiAxis);
			result.Notes.Add(
				"Requisito simple: " + a.AsimilationId + " " + a.FromToken + "->" + a.ToToken
				+ FormatPathSuffix(a.PathSignature, a.IsMultiAxis) + ".");
			return req;
		}

		private static string FormatPathSuffix(string pathSignature, bool multiAxis)
		{
			if (pathSignature.Length == 0)
			{
				return string.Empty;
			}

			if (multiAxis)
			{
				return " · multi-eje " + pathSignature;
			}

			return " · path " + pathSignature;
		}

		private static void EmitRequirement(StringBuilder script, InferredRequirement req)
		{
			script.Append("# ");
			script.AppendLine(req.Comment);
			if (req.PhaseComment.Length > 0)
			{
				script.Append("# ");
				script.AppendLine(req.PhaseComment);
			}

			if (req.IsMultiAxis && req.PathSignature.Length > 0)
			{
				script.Append("# vista multi-eje: ");
				script.AppendLine(req.PathSignature);
			}

			script.Append("require ");
			if (req.BothWays)
			{
				script.Append("both ways ");
			}

			script.Append("every ");
			script.Append(req.HeadwayMinutes.ToString(CultureInfo.InvariantCulture));
			script.Append(" min ");
			script.Append(req.FromToken);
			script.Append(" -> ");
			script.Append(req.ToToken);
			script.Append(' ');
			script.Append(FormatClock(req.WindowStart));
			script.Append('-');
			// ventana: última salida + 1h de holgura visual (el planificador usa la ventana como marco de salidas)
			TimeSpan end = req.WindowEnd + TimeSpan.FromHours(1);
			if (end.TotalHours >= 24)
			{
				end = new TimeSpan(23, 59, 0);
			}

			script.Append(FormatClock(end));
			script.Append(" as ");
			script.Append(req.RequirementId);
			script.AppendLine();

			script.Append("  days ");
			script.AppendLine(req.DaysToken);

			if (req.Stops.DefaultDwellSeconds > 0)
			{
				script.Append("  stops ");
				script.Append(FormatDwellToken(TimeSpan.FromSeconds(req.Stops.DefaultDwellSeconds)));
				script.AppendLine();
			}

			if (req.Stops.Skips.Count > 0)
			{
				script.Append("  skip");
				int s = 0;
				while (s < req.Stops.Skips.Count)
				{
					script.Append(' ');
					script.Append(QuoteIfNeeded(req.Stops.Skips[s]));
					s++;
				}

				script.AppendLine();
			}

			int o = 0;
			while (o < req.Stops.Overrides.Count)
			{
				StopDwellDraft ov = req.Stops.Overrides[o];
				script.Append("  dwell ");
				script.Append(QuoteIfNeeded(ov.StationToken));
				script.Append(' ');
				script.Append(FormatDwellToken(TimeSpan.FromSeconds(ov.DwellSeconds)));
				script.AppendLine();
				o++;
			}
		}

		/// <summary>
		/// Infiere stops/skip/dwell a partir de los trips Onice.
		/// - Excluye el destino (último trip; dwell 0 = llegada, no skip comercial).
		/// - Colapsa el nudo de enlace multi-eje (p. ej. Enllaç dest 19→30 con run=0).
		/// - Emite tokens resueltos (AVR/id) cuando hay layout.
		/// </summary>
		private static StopPatternDraft InferStopPattern(
			TopoAsimilationTemplate template,
			TopoLayout? layout,
			string fromToken,
			string toToken)
		{
			StopPatternDraft draft = new StopPatternDraft();
			if (template.Trips.Count == 0)
			{
				return draft;
			}

			// Pasos comerciales intermedios (sin destino final), colapsando transferencias.
			List<CommercialStopDraft> intermediates = new List<CommercialStopDraft>();
			int lastTripIndex = template.Trips.Count - 1;
			int ti = 0;
			while (ti < lastTripIndex)
			{
				TopoAsimilationTrip trip = template.Trips[ti];
				string token = ResolveStationToken(trip.StationName, trip.DestCode, layout);
				int dwellSec = (int)Math.Round(trip.Dwell.TotalSeconds);
				if (dwellSec < 0)
				{
					dwellSec = 0;
				}

				// Transferencia multi-eje: mismo nudo, run≈0 en el trip actual o el siguiente con mismo nombre.
				// Colapsamos a un solo stop comercial con el mayor dwell.
				if (intermediates.Count > 0
					&& string.Equals(intermediates[intermediates.Count - 1].Token, token, StringComparison.OrdinalIgnoreCase)
					&& trip.RunTime <= TimeSpan.FromSeconds(1))
				{
					CommercialStopDraft prev = intermediates[intermediates.Count - 1];
					if (dwellSec > prev.DwellSeconds)
					{
						prev.DwellSeconds = dwellSec;
					}

					ti++;
					continue;
				}

				// No incluir origen/destino del OD como intermedia.
				if (string.Equals(token, fromToken, StringComparison.OrdinalIgnoreCase)
					|| string.Equals(token, toToken, StringComparison.OrdinalIgnoreCase))
				{
					ti++;
					continue;
				}

				intermediates.Add(new CommercialStopDraft(token, dwellSec));
				ti++;
			}

			// Contar dwells > 0 para moda (default)
			Dictionary<int, int> dwellCounts = new Dictionary<int, int>();
			int index = 0;
			while (index < intermediates.Count)
			{
				int sec = intermediates[index].DwellSeconds;
				if (sec > 0)
				{
					int count;
					if (!dwellCounts.TryGetValue(sec, out count))
					{
						count = 0;
					}

					dwellCounts[sec] = count + 1;
				}

				index++;
			}

			int defaultSec = 30;
			int bestCount = -1;
			foreach (KeyValuePair<int, int> kv in dwellCounts)
			{
				if (kv.Value > bestCount || (kv.Value == bestCount && kv.Key < defaultSec))
				{
					bestCount = kv.Value;
					defaultSec = kv.Key;
				}
			}

			if (bestCount < 0)
			{
				defaultSec = 0;
			}

			draft.DefaultDwellSeconds = defaultSec;

			index = 0;
			while (index < intermediates.Count)
			{
				CommercialStopDraft stop = intermediates[index];
				string token = stop.Token;
				int sec = stop.DwellSeconds;

				if (sec <= 0)
				{
					if (token.Length > 0 && !ListContainsIgnoreCase(draft.Skips, token))
					{
						draft.Skips.Add(token);
					}
				}
				else if (sec != defaultSec)
				{
					draft.Overrides.Add(new StopDwellDraft(token, sec));
				}

				index++;
			}

			return draft;
		}

		/// <summary>
		/// Resuelve un token de estación estable (preferentemente AVR).
		/// Prioridad: id de Onice → AVR exacto → nombre exacto (con/sin diacríticos) → heurística SFM.
		/// No usa coincidencias parciales (evita Inca ⊂ Pont d'Inca → pdi).
		/// </summary>
		private static string ResolveStationToken(string name, string code, TopoLayout? layout)
		{
			if (layout is not null)
			{
				// 1) Código Onice = id de estación en el topo
				if (!string.IsNullOrWhiteSpace(code))
				{
					Station? byId = layout.FindStationById(code.Trim());
					if (byId is not null)
					{
						return PreferredToken(byId);
					}
				}

				// 2) AVR exacto (case-insensitive)
				if (!string.IsNullOrWhiteSpace(name))
				{
					Station? byAvr = FindStationByAvrExact(layout, name.Trim());
					if (byAvr is not null)
					{
						return PreferredToken(byAvr);
					}

					// 3) Nombre exacto (con y sin diacríticos)
					Station? byName = FindStationByNameExact(layout, name.Trim());
					if (byName is not null)
					{
						return PreferredToken(byName);
					}
				}
			}

			// Heurística sin layout / fallback SFM
			string n = (name ?? string.Empty).Trim();
			string heuristic = HeuristicSfmToken(n, code);
			if (heuristic.Length > 0)
			{
				return heuristic;
			}

			return SanitizeStationToken(n.Length > 0 ? n : (code ?? string.Empty));
		}

		private static string PreferredToken(Station station)
		{
			if (station.Avr.Length > 0)
			{
				return station.Avr;
			}

			if (station.Id.Length > 0)
			{
				return station.Id;
			}

			return SanitizeStationToken(station.Name);
		}

		private static Station? FindStationByAvrExact(TopoLayout layout, string key)
		{
			Station? best = null;
			int i = 0;
			while (i < layout.Stations.Count)
			{
				Station s = layout.Stations[i];
				if (string.Equals(s.Avr, key, StringComparison.OrdinalIgnoreCase))
				{
					if (best is null || string.CompareOrdinal(s.Id, best.Id) < 0)
					{
						best = s;
					}
				}

				i++;
			}

			return best;
		}

		private static Station? FindStationByNameExact(TopoLayout layout, string key)
		{
			string keyNorm = RemoveDiacritics(key);
			Station? best = null;
			int i = 0;
			while (i < layout.Stations.Count)
			{
				Station s = layout.Stations[i];
				string name = s.Name ?? string.Empty;
				bool match = string.Equals(name, key, StringComparison.OrdinalIgnoreCase)
					|| string.Equals(RemoveDiacritics(name), keyNorm, StringComparison.OrdinalIgnoreCase);
				if (match)
				{
					if (best is null || string.CompareOrdinal(s.Id, best.Id) < 0)
					{
						best = s;
					}
				}

				i++;
			}

			return best;
		}

		private static string HeuristicSfmToken(string n, string code)
		{
			if (n.Equals("Palma", StringComparison.OrdinalIgnoreCase) || code == "01" || code == "40")
			{
				return "PMI";
			}

			if (n.Equals("Manacor", StringComparison.OrdinalIgnoreCase) || code == "24")
			{
				return "MAN";
			}

			if (n.Equals("Sa Pobla", StringComparison.OrdinalIgnoreCase)
				|| n.Equals("Sa pobla", StringComparison.OrdinalIgnoreCase)
				|| code == "33")
			{
				return "SPB";
			}

			if (n.Equals("UIB", StringComparison.OrdinalIgnoreCase) || code == "48")
			{
				return "UIB";
			}

			if (n.Equals("Marratxí", StringComparison.OrdinalIgnoreCase)
				|| n.Equals("mtx", StringComparison.OrdinalIgnoreCase)
				|| n.IndexOf("Marratx", StringComparison.OrdinalIgnoreCase) >= 0
				|| code == "11")
			{
				return "MTX";
			}

			// Exacto "Inca" (no "Pont d'Inca")
			if (n.Equals("Inca", StringComparison.OrdinalIgnoreCase) || code == "17")
			{
				return "INC";
			}

			if (n.IndexOf("Enlla", StringComparison.OrdinalIgnoreCase) >= 0 || code == "19" || code == "30")
			{
				return "ELÁ";
			}

			if (n.Equals("Llubí", StringComparison.OrdinalIgnoreCase)
				|| n.Equals("llubi", StringComparison.OrdinalIgnoreCase)
				|| code == "31")
			{
				return "LLB";
			}

			if (n.Equals("Muro", StringComparison.OrdinalIgnoreCase) || code == "32")
			{
				return "mur";
			}

			return string.Empty;
		}

		/// <summary>
		/// Describe el camino físico (posiblemente multi-eje) entre dos tokens de estación.
		/// </summary>
		private static void DescribePhysicalPath(
			TopoLayout layout,
			string fromToken,
			string toToken,
			out string pathSignature,
			out bool multiAxis,
			out long originRoutePk,
			out long destRoutePk,
			out bool hasRoutePks)
		{
			pathSignature = string.Empty;
			multiAxis = false;
			originRoutePk = 0L;
			destRoutePk = 0L;
			hasRoutePks = false;

			Station? from;
			Station? to;
			string? err;
			if (!DemandStationResolver.TryResolve(fromToken, layout, out from, out err) || from is null)
			{
				return;
			}

			if (!DemandStationResolver.TryResolve(toToken, layout, out to, out err) || to is null)
			{
				return;
			}

			RouteView? view;
			StationOnRoute? origin;
			StationOnRoute? destination;
			if (!RouteView.TryFindPath(layout, from, to, out view, out origin, out destination)
				|| view is null
				|| origin is null
				|| destination is null)
			{
				return;
			}

			pathSignature = view.PathSignature();
			multiAxis = view.Legs.Count > 1;
			originRoutePk = origin.PK;
			destRoutePk = destination.PK;
			hasRoutePks = true;
		}

		private static string RemoveDiacritics(string text)
		{
			string formD = text.Normalize(NormalizationForm.FormD);
			StringBuilder sb = new StringBuilder();
			int i = 0;
			while (i < formD.Length)
			{
				System.Globalization.UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(formD[i]);
				if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
				{
					sb.Append(formD[i]);
				}

				i++;
			}

			return sb.ToString().Normalize(NormalizationForm.FormC);
		}

		private static int EstimateMedianHeadwayMinutes(List<TimeSpan> sortedDeps)
		{
			// Una sola salida en el bloque: no inventar cadencia horaria (caso 7001/7002).
			if (sortedDeps.Count < 2)
			{
				return 24 * 60;
			}

			List<int> gaps = new List<int>();
			int i = 1;
			while (i < sortedDeps.Count)
			{
				double sec = (sortedDeps[i] - sortedDeps[i - 1]).TotalSeconds;
				// Incluir huecos largos (antes se recortaban a &lt; 6 h y se caía al default 60).
				if (sec > 60 && sec < 20 * 3600)
				{
					gaps.Add((int)Math.Round(sec / 60.0));
				}

				i++;
			}

			if (gaps.Count == 0)
			{
				return 24 * 60;
			}

			gaps.Sort();
			return gaps[gaps.Count / 2];
		}

		private static int ChooseHeadway(int a, int b)
		{
			if (a <= 0)
			{
				return b > 0 ? b : 60;
			}

			if (b <= 0)
			{
				return a;
			}

			// Media redondeada a enteros “bonitos”
			int m = (a + b) / 2;
			if (Math.Abs(a - b) <= 5)
			{
				return Math.Min(a, b);
			}

			return m;
		}

		private static string FreqToDaysToken(string freq)
		{
			string f = (freq ?? string.Empty).Trim().ToLowerInvariant();
			if (f == "lab" || f == "laborables")
			{
				return "lab";
			}

			if (f == "fes" || f == "festivos")
			{
				return "fes";
			}

			return "all";
		}

		private static string CanonicalRouteKey(string from, string to)
		{
			return from + "\u001f" + to;
		}

		private static string SanitizeId(string text)
		{
			StringBuilder sb = new StringBuilder();
			int i = 0;
			while (i < text.Length)
			{
				char c = text[i];
				if (char.IsLetterOrDigit(c))
				{
					sb.Append(c);
				}
				else if (c == '-' || c == '_')
				{
					sb.Append(c);
				}

				i++;
			}

			return sb.Length > 0 ? sb.ToString() : "X";
		}

		private static string SanitizeStationToken(string name)
		{
			string t = name.Trim();
			if (t.Length == 0)
			{
				return "UNK";
			}

			// Preferir token simple; si tiene espacios se emitirá entre comillas
			return t;
		}

		private static string QuoteIfNeeded(string token)
		{
			if (token.IndexOf(' ') >= 0 || token.IndexOf('"') >= 0)
			{
				return "\"" + token.Replace("\"", "\\\"") + "\"";
			}

			return token;
		}

		private static string EscapeQuotes(string text)
		{
			return text.Replace("\"", "\\\"");
		}

		private static string FormatClock(TimeSpan ts)
		{
			int h = (int)ts.TotalHours;
			if (h < 0)
			{
				h = 0;
			}

			if (h > 23)
			{
				h = 23;
			}

			return h.ToString("00", CultureInfo.InvariantCulture) + ":"
				+ Math.Abs(ts.Minutes).ToString("00", CultureInfo.InvariantCulture);
		}

		private static string FormatDwellToken(TimeSpan dwell)
		{
			if (dwell.TotalSeconds >= 60 && Math.Abs(dwell.TotalSeconds % 60) < 0.01)
			{
				return ((int)dwell.TotalMinutes).ToString(CultureInfo.InvariantCulture) + "min";
			}

			return ((int)Math.Round(dwell.TotalSeconds)).ToString(CultureInfo.InvariantCulture) + "s";
		}

		private static string FormatPhase(TimeSpan a, TimeSpan b)
		{
			double sec = Math.Abs((a - b).TotalSeconds);
			return ((int)Math.Round(sec / 60.0)).ToString(CultureInfo.InvariantCulture) + "min";
		}

		private static TimeSpan MinTime(TimeSpan a, TimeSpan b)
		{
			return a <= b ? a : b;
		}

		private static TimeSpan MaxTime(TimeSpan a, TimeSpan b)
		{
			return a >= b ? a : b;
		}

		private static bool ListContainsIgnoreCase(List<string> list, string value)
		{
			int i = 0;
			while (i < list.Count)
			{
				if (string.Equals(list[i], value, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}

				i++;
			}

			return false;
		}

		private sealed class BlockAnalysis
		{
			public string AsimilationId = string.Empty;
			public string FromToken = string.Empty;
			public string ToToken = string.Empty;
			public string FromName = string.Empty;
			public string ToName = string.Empty;
			public string DaysToken = "lab";
			public string Freq = string.Empty;
			public string Pattern = string.Empty;
			public int MedianHeadwayMinutes;
			public TimeSpan WindowStart;
			public TimeSpan WindowEnd;
			public TimeSpan FirstDep;
			public int CirculationCount;
			public StopPatternDraft Stops = new StopPatternDraft();
			public string RouteKey = string.Empty;
			public string ReverseRouteKey = string.Empty;
			public string PathSignature = string.Empty;
			public bool IsMultiAxis;
			public long OriginRoutePk;
			public long DestRoutePk;
			public bool HasRoutePks;
		}

		private sealed class InferredRequirement
		{
			public string RequirementId = string.Empty;
			public bool BothWays;
			public string FromToken = string.Empty;
			public string ToToken = string.Empty;
			public string DaysToken = "lab";
			public int HeadwayMinutes = 60;
			public TimeSpan WindowStart;
			public TimeSpan WindowEnd;
			public StopPatternDraft Stops = new StopPatternDraft();
			public string Comment = string.Empty;
			public string PhaseComment = string.Empty;
			public string PathSignature = string.Empty;
			public bool IsMultiAxis;
		}

		private sealed class StopPatternDraft
		{
			public int DefaultDwellSeconds;
			public List<string> Skips { get; } = new List<string>();
			public List<StopDwellDraft> Overrides { get; } = new List<StopDwellDraft>();
		}

		private sealed class StopDwellDraft
		{
			public StopDwellDraft(string stationToken, int dwellSeconds)
			{
				StationToken = stationToken;
				DwellSeconds = dwellSeconds;
			}

			public string StationToken { get; }
			public int DwellSeconds { get; }
		}

		private sealed class CommercialStopDraft
		{
			public CommercialStopDraft(string token, int dwellSeconds)
			{
				Token = token;
				DwellSeconds = dwellSeconds;
			}

			public string Token { get; }
			public int DwellSeconds { get; set; }
		}
	}
}

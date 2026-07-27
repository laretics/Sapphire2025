using System;
using System.Collections.Generic;
using Diamond.Topo;

namespace Diamond.Timed
{
	/// <summary>
	/// Resuelve <see cref="StationRef"/> contra un <see cref="TopoLayout"/> de forma determinista.
	/// Orden de coincidencia: Id exacto, AVR exacto (ordinal), nombre exacto, AVR/nombre sin distinguir mayúsculas.
	/// Si hay ambigüedad en el último paso, error.
	/// </summary>
	public static class DemandStationResolver
	{
		public static void Resolve(DemandCompileResult compileResult, TopoLayout layout, List<string>? errors = null)
		{
			if (compileResult is null)
			{
				throw new ArgumentNullException(nameof(compileResult));
			}

			if (layout is null)
			{
				throw new ArgumentNullException(nameof(layout));
			}

			List<string> errorList = errors ?? new List<string>();
			int index = 0;
			while (index < compileResult.Requirements.Count)
			{
				DemandRequirement req = compileResult.Requirements[index];
				Station? from;
				string? fromError;
				if (!TryResolve(req.From.Text, layout, out from, out fromError))
				{
					errorList.Add(FormatResolveError(req, "origen", req.From.Text, fromError));
				}
				else
				{
					req.FromStation = from;
				}

				Station? to;
				string? toError;
				if (!TryResolve(req.To.Text, layout, out to, out toError))
				{
					errorList.Add(FormatResolveError(req, "destino", req.To.Text, toError));
				}
				else
				{
					req.ToStation = to;
				}

				index++;
			}
		}

		public static bool TryResolve(string text, TopoLayout layout, out Station? station, out string? error)
		{
			station = null;
			error = null;

			if (string.IsNullOrWhiteSpace(text))
			{
				error = "referencia vacía.";
				return false;
			}

			string key = text.Trim();

			// 1) Id exacto
			Station? byId = layout.FindStationById(key);
			if (byId is not null)
			{
				station = byId;
				return true;
			}

			// 2) AVR exacto (ordinal)
			List<Station> avrExact = new List<Station>();
			List<Station> nameExact = new List<Station>();
			List<Station> avrIgnore = new List<Station>();
			List<Station> nameIgnore = new List<Station>();

			int index = 0;
			while (index < layout.Stations.Count)
			{
				Station s = layout.Stations[index];
				if (string.Equals(s.Avr, key, StringComparison.Ordinal))
				{
					avrExact.Add(s);
				}

				if (string.Equals(s.Name, key, StringComparison.Ordinal))
				{
					nameExact.Add(s);
				}

				if (string.Equals(s.Avr, key, StringComparison.OrdinalIgnoreCase))
				{
					avrIgnore.Add(s);
				}

				if (string.Equals(s.Name, key, StringComparison.OrdinalIgnoreCase))
				{
					nameIgnore.Add(s);
				}

				index++;
			}

			// Si el layout legacy duplica la misma estación en varios ejes (mismo AVR, distinto id),
			// elegimos de forma determinista el Id menor (orden ordinal).
			if (avrExact.Count >= 1)
			{
				station = PickDeterministic(avrExact);
				return true;
			}

			if (nameExact.Count >= 1)
			{
				station = PickDeterministic(nameExact);
				return true;
			}

			if (avrIgnore.Count >= 1)
			{
				station = PickDeterministic(avrIgnore);
				return true;
			}

			if (nameIgnore.Count >= 1)
			{
				station = PickDeterministic(nameIgnore);
				return true;
			}

			error = "estación desconocida '" + key + "'.";
			return false;
		}

		private static string FormatResolveError(DemandRequirement req, string role, string text, string? detail)
		{
			string prefix = req.SourceLine > 0
				? "line " + req.SourceLine.ToString(System.Globalization.CultureInfo.InvariantCulture) + ": "
				: string.Empty;
			return prefix + "requisito " + req.Id + " " + role + ": " + (detail ?? ("no se pudo resolver '" + text + "'."));
		}

		private static Station PickDeterministic(List<Station> candidates)
		{
			candidates.Sort(static (left, right) => string.CompareOrdinal(left.Id, right.Id));
			return candidates[0];
		}
	}
}

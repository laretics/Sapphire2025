using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Diamond.Timed
{
	/// <summary>
	/// Patrón de paradas de un requisito: dwell por defecto, exclusiones y excepciones.
	/// </summary>
	public sealed class StopPattern
	{
		private TimeSpan? mvarDefaultDwell;
		private readonly List<StationRef> mcolSkip;
		private readonly List<StopDwellOverride> mcolOverrides;
		private StationRef? mvarCrossAt;

		public StopPattern()
		{
			mvarDefaultDwell = null;
			mcolSkip = new List<StationRef>();
			mcolOverrides = new List<StopDwellOverride>();
			mvarCrossAt = null;
		}

		/// <summary>
		/// Si tiene valor, se paran todas las estaciones/apeaderos del trayecto salvo skip.
		/// Si es null, modo legacy del planificador (solo principales, dwell 0).
		/// </summary>
		public TimeSpan? DefaultDwell
		{
			get { return mvarDefaultDwell; }
			set { mvarDefaultDwell = value; }
		}

		public IReadOnlyList<StationRef> Skip
		{
			get { return mcolSkip; }
		}

		public IReadOnlyList<StopDwellOverride> Overrides
		{
			get { return mcolOverrides; }
		}

		/// <summary>
		/// Punto preferido de cruce entre sentidos opuestos (p. ej. Enllaç).
		/// </summary>
		public StationRef? CrossAt
		{
			get { return mvarCrossAt; }
			set { mvarCrossAt = value; }
		}

		public bool HasExplicitPattern
		{
			get
			{
				return mvarDefaultDwell.HasValue
					|| mcolSkip.Count > 0
					|| mcolOverrides.Count > 0
					|| mvarCrossAt is not null;
			}
		}

		public void AddSkip(StationRef station)
		{
			if (station is null)
			{
				throw new ArgumentNullException(nameof(station));
			}

			mcolSkip.Add(station);
		}

		public void AddOverride(StationRef station, TimeSpan dwell)
		{
			if (station is null)
			{
				throw new ArgumentNullException(nameof(station));
			}

			if (dwell < TimeSpan.Zero)
			{
				throw new ArgumentOutOfRangeException(nameof(dwell));
			}

			mcolOverrides.Add(new StopDwellOverride(station, dwell));
		}

		/// <summary>
		/// Indica si debe detenerse y con qué dwell.
		/// </summary>
		public bool TryGetDwell(string stationId, string avr, string name, out TimeSpan dwell)
		{
			dwell = TimeSpan.Zero;

			int o = 0;
			while (o < mcolOverrides.Count)
			{
				if (Matches(mcolOverrides[o].Station, stationId, avr, name))
				{
					dwell = mcolOverrides[o].Dwell;
					return dwell > TimeSpan.Zero;
				}

				o++;
			}

			int s = 0;
			while (s < mcolSkip.Count)
			{
				if (Matches(mcolSkip[s], stationId, avr, name))
				{
					return false;
				}

				s++;
			}

			if (mvarDefaultDwell.HasValue)
			{
				dwell = mvarDefaultDwell.Value;
				return dwell > TimeSpan.Zero;
			}

			return false;
		}

		/// <summary>
		/// Coincide por id, AVR o nombre completo (sin distinguir mayúsculas ni acentos).
		/// No usa subcadenas: <c>INC</c> no debe emparejar <c>Pont d'Inca</c>
		/// (antes <c>IndexOf</c> hinchaba el dwell en varias estaciones).
		/// </summary>
		public static bool Matches(StationRef reference, string stationId, string avr, string name)
		{
			string key = reference.Text.Trim();
			if (key.Length == 0)
			{
				return false;
			}

			if (string.Equals(key, stationId, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (avr.Length > 0 && string.Equals(key, avr, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (name.Length > 0 && string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			// Misma igualdad sin acentos (Enllaç / Enllac, Lloseta / …).
			string keyFold = FoldAccents(key);
			if (avr.Length > 0 && string.Equals(keyFold, FoldAccents(avr), StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (name.Length > 0 && string.Equals(keyFold, FoldAccents(name), StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			return false;
		}

		/// <summary>
		/// Quita marcas diacríticas para comparar nombres/AVR de forma tolerante.
		/// </summary>
		internal static string FoldAccents(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return string.Empty;
			}

			string normalized = text.Normalize(NormalizationForm.FormD);
			StringBuilder sb = new StringBuilder(normalized.Length);
			int i = 0;
			while (i < normalized.Length)
			{
				UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(normalized[i]);
				if (cat != UnicodeCategory.NonSpacingMark)
				{
					sb.Append(normalized[i]);
				}

				i++;
			}

			return sb.ToString().Normalize(NormalizationForm.FormC);
		}
	}

	public sealed class StopDwellOverride
	{
		private readonly StationRef mvarStation;
		private readonly TimeSpan mvarDwell;

		public StopDwellOverride(StationRef station, TimeSpan dwell)
		{
			mvarStation = station;
			mvarDwell = dwell;
		}

		public StationRef Station
		{
			get { return mvarStation; }
		}

		public TimeSpan Dwell
		{
			get { return mvarDwell; }
		}
	}
}

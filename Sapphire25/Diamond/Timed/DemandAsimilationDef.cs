using System;

namespace Diamond.Timed
{
	/// <summary>
	/// Definición de asimilación en el script de demanda: patrón de numeración
	/// (<c>49##</c>, <c>P##MTX</c>, …) y color de representación por corredor OD y días.
	/// </summary>
	/// <remarks>
	/// <code>
	/// days lab
	///   asim PMI -> MAN numbers 49## color #38bdf8
	///   asim PMI -> MTX numbers P##MTX color orange
	/// </code>
	/// El OD es <strong>dirigido</strong>: <c>PMI -&gt; MAN</c> y <c>MAN -&gt; PMI</c>
	/// son asimilaciones distintas (pueden llevar <c>numbers</c>/<c>color</c> distintos).
	/// </remarks>
	public sealed class DemandAsimilationDef
	{
		private readonly StationRef mvarFrom;
		private readonly StationRef mvarTo;
		private readonly ServiceDays mvarDays;
		private readonly string mvarNumberPattern;
		private readonly string mvarColor;
		private readonly int mvarSourceLine;
		private readonly int mvarScriptOrder;

		public DemandAsimilationDef(
			StationRef from,
			StationRef to,
			ServiceDays days,
			string numberPattern,
			string? color,
			int sourceLine,
			int scriptOrder)
		{
			if (from is null)
			{
				throw new ArgumentNullException(nameof(from));
			}

			if (to is null)
			{
				throw new ArgumentNullException(nameof(to));
			}

			if (days is null)
			{
				throw new ArgumentNullException(nameof(days));
			}

			mvarFrom = from;
			mvarTo = to;
			mvarDays = days;
			mvarNumberPattern = numberPattern ?? string.Empty;
			mvarColor = string.IsNullOrWhiteSpace(color) ? string.Empty : color.Trim();
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
		}

		/// <summary>
		/// Patrón de numeración (p. ej. <c>49##</c>, <c>P##MTX</c>).
		/// Vacío = solo color (sin forzar numeración).
		/// </summary>
		public string NumberPattern
		{
			get { return mvarNumberPattern; }
		}

		public string Color
		{
			get { return mvarColor; }
		}

		public bool HasColor
		{
			get { return mvarColor.Length > 0; }
		}

		public bool HasNumberPattern
		{
			get { return mvarNumberPattern.Length > 0; }
		}

		/// <summary>Compat: true si hay patrón de numeración.</summary>
		public bool HasSeries
		{
			get { return HasNumberPattern; }
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
}

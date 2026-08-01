using System;
using Diamond.Topo;

namespace Diamond.Timed
{
	/// <summary>
	/// Tipo de restricción de topología de sesión (script de malla, no XML base).
	/// </summary>
	public enum DemandTopoConstraintKind
	{
		/// <summary>Número de vías en un tramo (1 = vía simple, 2 = doble…).</summary>
		TrackCount = 0,
		/// <summary>Limitación de velocidad (km/h) en un tramo.</summary>
		SpeedLimit = 1
	}

	/// <summary>
	/// Restricción de topología declarada en el mini-DSL para condicionar la malla
	/// de la sesión sin modificar la topología base (XML / fijas).
	/// </summary>
	/// <remarks>
	/// Ejemplos de script:
	/// <code>
	/// single track Enllaç -&gt; Manacor
	/// tracks 1 INC -&gt; MAN on T3
	/// limit 60 Petra -&gt; Manacor
	/// vmax 80 Enllaç -&gt; MAN
	/// </code>
	/// </remarks>
	public sealed class DemandTopoConstraint
	{
		private readonly DemandTopoConstraintKind mvarKind;
		private readonly int mvarValue;
		private readonly StationRef mvarFrom;
		private readonly StationRef mvarTo;
		private readonly string mvarAxisId;
		private readonly int mvarSourceLine;
		private readonly int mvarScriptOrder;

		private Station? mvarFromStation;
		private Station? mvarToStation;
		private Axis? mvarResolvedAxis;
		private long mvarPk0;
		private long mvarPkf;
		private bool mvarIsResolved;

		public DemandTopoConstraint(
			DemandTopoConstraintKind kind,
			int value,
			StationRef from,
			StationRef to,
			string? axisId,
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

			if (value < 1)
			{
				throw new ArgumentOutOfRangeException(nameof(value));
			}

			if (kind == DemandTopoConstraintKind.SpeedLimit && value > 500)
			{
				throw new ArgumentOutOfRangeException(nameof(value), "velocidad irreal.");
			}

			mvarKind = kind;
			mvarValue = value;
			mvarFrom = from;
			mvarTo = to;
			mvarAxisId = string.IsNullOrWhiteSpace(axisId) ? string.Empty : axisId.Trim();
			mvarSourceLine = sourceLine;
			mvarScriptOrder = scriptOrder;
			mvarFromStation = null;
			mvarToStation = null;
			mvarResolvedAxis = null;
			mvarPk0 = 0L;
			mvarPkf = 0L;
			mvarIsResolved = false;
		}

		public DemandTopoConstraintKind Kind
		{
			get { return mvarKind; }
		}

		/// <summary>Vías (TrackCount) o km/h (SpeedLimit).</summary>
		public int Value
		{
			get { return mvarValue; }
		}

		public StationRef From
		{
			get { return mvarFrom; }
		}

		public StationRef To
		{
			get { return mvarTo; }
		}

		/// <summary>Eje opcional (p. ej. T3). Vacío = se infiere del par de estaciones.</summary>
		public string AxisId
		{
			get { return mvarAxisId; }
		}

		public bool HasAxisId
		{
			get { return mvarAxisId.Length > 0; }
		}

		public int SourceLine
		{
			get { return mvarSourceLine; }
		}

		public int ScriptOrder
		{
			get { return mvarScriptOrder; }
		}

		public Station? FromStation
		{
			get { return mvarFromStation; }
			internal set { mvarFromStation = value; }
		}

		public Station? ToStation
		{
			get { return mvarToStation; }
			internal set { mvarToStation = value; }
		}

		/// <summary>Eje donde se aplicó el tramo (tras resolver).</summary>
		public Axis? ResolvedAxis
		{
			get { return mvarResolvedAxis; }
		}

		/// <summary>PK inicial inclusivo del tramo en el eje resuelto.</summary>
		public long Pk0
		{
			get { return mvarPk0; }
		}

		/// <summary>PK final exclusivo del tramo en el eje resuelto.</summary>
		public long Pkf
		{
			get { return mvarPkf; }
		}

		public bool IsResolved
		{
			get { return mvarIsResolved; }
		}

		internal void MarkResolved(Axis axis, long pk0, long pkf)
		{
			if (axis is null)
			{
				throw new ArgumentNullException(nameof(axis));
			}

			mvarResolvedAxis = axis;
			mvarPk0 = pk0;
			mvarPkf = pkf;
			mvarIsResolved = true;
		}

		public override string ToString()
		{
			if (mvarKind == DemandTopoConstraintKind.TrackCount)
			{
				return "tracks " + mvarValue.ToString(System.Globalization.CultureInfo.InvariantCulture)
					+ " " + mvarFrom.Text + " -> " + mvarTo.Text;
			}

			return "limit " + mvarValue.ToString(System.Globalization.CultureInfo.InvariantCulture)
				+ " " + mvarFrom.Text + " -> " + mvarTo.Text;
		}
	}
}

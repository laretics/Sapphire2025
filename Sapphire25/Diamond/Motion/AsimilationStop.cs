using System;
using Diamond.Topo;

namespace Diamond.Motion
{
	/// <summary>
	/// Parada comercial en un punto del eje, con tiempo de detención (dwell).
	/// </summary>
	public sealed class AsimilationStop
	{
		private readonly StationOnAxis mvarPlacement;
		private readonly TimeSpan mvarDwell;

		public AsimilationStop(StationOnAxis placement, TimeSpan dwell)
		{
			if (placement is null)
			{
				throw new ArgumentNullException(nameof(placement));
			}

			if (dwell < TimeSpan.Zero)
			{
				throw new ArgumentOutOfRangeException(nameof(dwell));
			}

			mvarPlacement = placement;
			mvarDwell = dwell;
		}

		public StationOnAxis Placement
		{
			get { return mvarPlacement; }
		}

		/// <summary>
		/// Tiempo parado en estación (puertas, correspondencia, etc.).
		/// En origen puede ser cero; en destino suele ignorarse para la marcha posterior.
		/// </summary>
		public TimeSpan Dwell
		{
			get { return mvarDwell; }
		}

		public long PK
		{
			get { return mvarPlacement.PK; }
		}
	}
}

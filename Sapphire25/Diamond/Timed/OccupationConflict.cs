using System;

namespace Diamond.Timed
{
	/// <summary>
	/// Conflicto duro de acantonamiento/cruce: dos ocupaciones incompatibles y su intersección
	/// en el plano tiempo×espacio (lo que se pinta en rojo en la malla).
	/// </summary>
	public sealed class OccupationConflict
	{
		private readonly string mvarCirculationIdA;
		private readonly string mvarCirculationIdB;
		private readonly CantonOccupationRect mvarIntersection;
		private readonly string mvarKind;

		public OccupationConflict(
			string circulationIdA,
			string circulationIdB,
			CantonOccupationRect intersection,
			string kind)
		{
			if (intersection is null)
			{
				throw new ArgumentNullException(nameof(intersection));
			}

			mvarCirculationIdA = circulationIdA ?? string.Empty;
			mvarCirculationIdB = circulationIdB ?? string.Empty;
			mvarIntersection = intersection;
			mvarKind = kind ?? "conflicto";
		}

		public string CirculationIdA
		{
			get { return mvarCirculationIdA; }
		}

		public string CirculationIdB
		{
			get { return mvarCirculationIdB; }
		}

		/// <summary>
		/// Rectángulo de solape (tiempo × PK) a resaltar en la malla.
		/// </summary>
		public CantonOccupationRect Intersection
		{
			get { return mvarIntersection; }
		}

		/// <summary>
		/// "acantonamiento" o "cruce en vía única".
		/// </summary>
		public string Kind
		{
			get { return mvarKind; }
		}

		public override string ToString()
		{
			return mvarKind + ": " + mvarCirculationIdA + " ∩ " + mvarCirculationIdB
				+ " @ " + mvarIntersection;
		}
	}
}

using System.Collections.Generic;

namespace Diamond.Rauta
{
	/// <summary>
	/// Paquete de horarios (rautatie): metadatos + planes de malla.
	/// </summary>
	public sealed class RautaDocument
	{
		private readonly RautaInfo mvarInfo;
		private readonly List<RautaPlan> mcolPlans;

		public RautaDocument()
		{
			mvarInfo = new RautaInfo();
			mcolPlans = new List<RautaPlan>();
		}

		public RautaInfo Info
		{
			get { return mvarInfo; }
		}

		public IReadOnlyList<RautaPlan> Plans
		{
			get { return mcolPlans; }
		}

		public void AddPlan(RautaPlan plan)
		{
			if (plan is not null)
			{
				mcolPlans.Add(plan);
			}
		}

		public RautaPlan? FindPlanById(string id)
		{
			int index = 0;
			while (index < mcolPlans.Count)
			{
				if (string.Equals(mcolPlans[index].Id, id, System.StringComparison.Ordinal))
				{
					return mcolPlans[index];
				}

				index++;
			}

			return null;
		}
	}

	public sealed class RautaInfo
	{
		public string Id { get; set; } = string.Empty;
		public string TopoId { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public string Comment { get; set; } = string.Empty;
		public string Version { get; set; } = string.Empty;
		public string Author { get; set; } = string.Empty;
	}

	/// <summary>
	/// Un plan de malla (p. ej. Invierno 2026).
	/// </summary>
	public sealed class RautaPlan
	{
		private readonly List<RautaBlock> mcolBlocks;

		public RautaPlan()
		{
			mcolBlocks = new List<RautaBlock>();
		}

		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public string Comment { get; set; } = string.Empty;

		public IReadOnlyList<RautaBlock> Blocks
		{
			get { return mcolBlocks; }
		}

		public void AddBlock(RautaBlock block)
		{
			if (block is not null)
			{
				mcolBlocks.Add(block);
			}
		}
	}

	/// <summary>
	/// Bloque de circulaciones que comparten asimilación y calendario.
	/// </summary>
	public sealed class RautaBlock
	{
		private readonly List<RautaCirculation> mcolCirculations;

		public RautaBlock()
		{
			mcolCirculations = new List<RautaCirculation>();
		}

		/// <summary>Id de asimilación del topo (p. ej. 44x3L).</summary>
		public string AsimilationId { get; set; } = string.Empty;

		/// <summary>lab | fes | …</summary>
		public string Freq { get; set; } = string.Empty;

		/// <summary>Máscara de numeración (p. ej. 49##).</summary>
		public string Pattern { get; set; } = string.Empty;

		public IReadOnlyList<RautaCirculation> Circulations
		{
			get { return mcolCirculations; }
		}

		public void AddCirculation(RautaCirculation circulation)
		{
			if (circulation is not null)
			{
				mcolCirculations.Add(circulation);
			}
		}
	}

	public sealed class RautaCirculation
	{
		public string Id { get; set; } = string.Empty;
		public TimeSpan Departure { get; set; }

		/// <summary>Si viene en el cir en lugar del block.</summary>
		public string? AsimilationId { get; set; }

		public string? Freq { get; set; }
	}
}

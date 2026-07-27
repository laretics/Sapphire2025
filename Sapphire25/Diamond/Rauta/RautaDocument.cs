using System;
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
		private string mvarId = string.Empty;
		private string mvarTopoId = string.Empty;
		private string mvarName = string.Empty;
		private string mvarDescription = string.Empty;
		private string mvarComment = string.Empty;
		private string mvarVersion = string.Empty;
		private string mvarAuthor = string.Empty;

		public string Id
		{
			get { return mvarId; }
			set { mvarId = value ?? string.Empty; }
		}

		public string TopoId
		{
			get { return mvarTopoId; }
			set { mvarTopoId = value ?? string.Empty; }
		}

		public string Name
		{
			get { return mvarName; }
			set { mvarName = value ?? string.Empty; }
		}

		public string Description
		{
			get { return mvarDescription; }
			set { mvarDescription = value ?? string.Empty; }
		}

		public string Comment
		{
			get { return mvarComment; }
			set { mvarComment = value ?? string.Empty; }
		}

		public string Version
		{
			get { return mvarVersion; }
			set { mvarVersion = value ?? string.Empty; }
		}

		public string Author
		{
			get { return mvarAuthor; }
			set { mvarAuthor = value ?? string.Empty; }
		}
	}

	/// <summary>
	/// Un plan de malla (p. ej. Invierno 2026).
	/// </summary>
	public sealed class RautaPlan
	{
		private readonly List<RautaBlock> mcolBlocks;
		private string mvarId = string.Empty;
		private string mvarName = string.Empty;
		private string mvarComment = string.Empty;

		public RautaPlan()
		{
			mcolBlocks = new List<RautaBlock>();
		}

		public string Id
		{
			get { return mvarId; }
			set { mvarId = value ?? string.Empty; }
		}

		public string Name
		{
			get { return mvarName; }
			set { mvarName = value ?? string.Empty; }
		}

		public string Comment
		{
			get { return mvarComment; }
			set { mvarComment = value ?? string.Empty; }
		}

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
		private string mvarAsimilationId = string.Empty;
		private string mvarFreq = string.Empty;
		private string mvarPattern = string.Empty;

		public RautaBlock()
		{
			mcolCirculations = new List<RautaCirculation>();
		}

		/// <summary>Id de asimilación del topo (p. ej. 44x3L).</summary>
		public string AsimilationId
		{
			get { return mvarAsimilationId; }
			set { mvarAsimilationId = value ?? string.Empty; }
		}

		/// <summary>lab | fes | …</summary>
		public string Freq
		{
			get { return mvarFreq; }
			set { mvarFreq = value ?? string.Empty; }
		}

		/// <summary>Máscara de numeración (p. ej. 49##).</summary>
		public string Pattern
		{
			get { return mvarPattern; }
			set { mvarPattern = value ?? string.Empty; }
		}

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
		private string mvarId = string.Empty;
		private TimeSpan mvarDeparture;
		private string? mvarAsimilationId;
		private string? mvarFreq;

		public string Id
		{
			get { return mvarId; }
			set { mvarId = value ?? string.Empty; }
		}

		public TimeSpan Departure
		{
			get { return mvarDeparture; }
			set { mvarDeparture = value; }
		}

		/// <summary>Si viene en el cir en lugar del block.</summary>
		public string? AsimilationId
		{
			get { return mvarAsimilationId; }
			set { mvarAsimilationId = value; }
		}

		public string? Freq
		{
			get { return mvarFreq; }
			set { mvarFreq = value; }
		}
	}
}

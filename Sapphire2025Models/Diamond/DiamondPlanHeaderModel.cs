namespace Sapphire2025Models.Diamond
{
	/// <summary>
	/// Metadatos de un plan de explotación Diamond (sin el script completo en listados largos;
	/// el script va en el detalle vía getplan).
	/// </summary>
	public class DiamondPlanHeaderModel
	{
		public Guid Id { get; set; }

		public string Name { get; set; } = string.Empty;

		public string ContentHash { get; set; } = string.Empty;

		public int ScriptByteLength { get; set; }

		public Guid TopoId { get; set; }

		public string TopoName { get; set; } = string.Empty;

		public string TopoContentHash { get; set; } = string.Empty;

		public string TopoStructuralHash { get; set; } = string.Empty;

		public string IncludedTopoPath { get; set; } = string.Empty;

		public string SourceFileName { get; set; } = string.Empty;

		public string Author { get; set; } = string.Empty;

		public string Notes { get; set; } = string.Empty;

		public bool IsActive { get; set; }

		public DateTime? ValidFrom { get; set; }

		public DateTime CreatedUtc { get; set; }

		public DateTime UpdatedUtc { get; set; }

		/// <summary>Script completo (solo en getplan / save).</summary>
		public string? SourceScript { get; set; }
	}
}

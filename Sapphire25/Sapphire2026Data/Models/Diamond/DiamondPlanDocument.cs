using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sapphire2026.Data.Models.Diamond
{
	/// <summary>
	/// Plan de explotación Diamond (script mini-DSL) versionado en Sapphire.
	/// Siempre referencia una topología del almacén <see cref="DiamondTopoDocument"/>.
	/// </summary>
	[Table("DiamondPlans")]
	public class DiamondPlanDocument
	{
		[Key]
		public Guid Id { get; set; }

		/// <summary>Nombre legible (plan "…" del script o del fichero).</summary>
		[Required]
		[MaxLength(200)]
		public string Name { get; set; } = string.Empty;

		/// <summary>Script fuente del mini-DSL Diamond (.ddm).</summary>
		[Required]
		public string SourceScript { get; set; } = string.Empty;

		/// <summary>SHA-256 del script UTF-8 (hex). Deduplicación opcional.</summary>
		[Required]
		[MaxLength(64)]
		public string ContentHash { get; set; } = string.Empty;

		public int ScriptByteLength { get; set; }

		/// <summary>Topología del almacén a la que está anclado este plan.</summary>
		public Guid TopoId { get; set; }

		/// <summary>Huella de contenido de la topo al guardar (detección de desfase).</summary>
		[MaxLength(64)]
		public string TopoContentHash { get; set; } = string.Empty;

		/// <summary>Huella estructural Diamond de la topo al guardar.</summary>
		[MaxLength(64)]
		public string TopoStructuralHash { get; set; } = string.Empty;

		/// <summary>Ruta include del script (p. ej. toposfm227.xml), si se pudo parsear.</summary>
		[MaxLength(260)]
		public string IncludedTopoPath { get; set; } = string.Empty;

		[MaxLength(260)]
		public string SourceFileName { get; set; } = string.Empty;

		[MaxLength(200)]
		public string Author { get; set; } = string.Empty;

		public string Notes { get; set; } = string.Empty;

		public bool IsActive { get; set; } = true;

		public DateTime? ValidFrom { get; set; }

		public DateTime CreatedUtc { get; set; }

		public DateTime UpdatedUtc { get; set; }

		[ForeignKey(nameof(TopoId))]
		public DiamondTopoDocument? Topo { get; set; }
	}
}

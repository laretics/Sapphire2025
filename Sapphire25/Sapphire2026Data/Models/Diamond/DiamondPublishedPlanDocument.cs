using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sapphire2026.Data.Models.Diamond
{
	/// <summary>
	/// Plan de explotación <strong>publicado</strong> (compilado) para clientes (Tourmaline).
	/// Inmutable por ContentHash; nueva versión = nueva fila.
	/// </summary>
	[Table("DiamondPublishedPlans")]
	public class DiamondPublishedPlanDocument
	{
		[Key]
		public Guid Id { get; set; }

		/// <summary>Plan de autoría origen (script), si se publicó desde el almacén.</summary>
		public Guid? SourcePlanId { get; set; }

		[Required]
		[MaxLength(200)]
		public string Name { get; set; } = string.Empty;

		public Guid TopoId { get; set; }

		[MaxLength(64)]
		public string TopoContentHash { get; set; } = string.Empty;

		[MaxLength(64)]
		public string TopoStructuralHash { get; set; } = string.Empty;

		/// <summary>Inicio de vigencia (día civil, UTC midnight o local según convención del servidor).</summary>
		public DateTime ValidFrom { get; set; }

		/// <summary>Fin de vigencia inclusivo; null = sin caducidad.</summary>
		public DateTime? ValidTo { get; set; }

		public DateTime CompiledUtc { get; set; }

		[Required]
		[MaxLength(64)]
		public string ContentHash { get; set; } = string.Empty;

		/// <summary>Formato del payload (p.ej. diamond-published-v1).</summary>
		[Required]
		[MaxLength(32)]
		public string Format { get; set; } = "diamond-published-v1";

		/// <summary>Paquete JSON compilado (Project por día).</summary>
		[Required]
		public byte[] Payload { get; set; } = Array.Empty<byte>();

		public int ByteLength { get; set; }

		/// <summary>Recuento de circulaciones (suma de días) para listados.</summary>
		public int CirculationCount { get; set; }

		public int AsimilationCount { get; set; }

		public string Notes { get; set; } = string.Empty;

		/// <summary>False = retirada de explotación (sigue en histórico).</summary>
		public bool IsActive { get; set; } = true;

		public DateTime CreatedUtc { get; set; }

		[ForeignKey(nameof(SourcePlanId))]
		public DiamondPlanDocument? SourcePlan { get; set; }

		[ForeignKey(nameof(TopoId))]
		public DiamondTopoDocument? Topo { get; set; }
	}
}

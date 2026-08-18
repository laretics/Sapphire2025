using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sapphire2026.Data.Models.Diamond
{
	/// <summary>
	/// Limitación temporal de velocidad anclada a una topología del almacén
	/// (misma relación que un plan de explotación: FK a <see cref="DiamondTopoDocument"/>).
	/// Un registro por tramo; el editor agrupa por eje.
	/// </summary>
	[Table("DiamondTemporaryLimits")]
	public class DiamondTemporaryLimit
	{
		[Key]
		public Guid Id { get; set; }

		public Guid TopoId { get; set; }

		/// <summary>Id del eje en la topología (p. ej. T3, M1).</summary>
		[Required]
		[MaxLength(64)]
		public string AxisId { get; set; } = string.Empty;

		public long Pk0 { get; set; }

		public long Pkf { get; set; }

		/// <summary>Velocidad máxima del tramo (km/h).</summary>
		public int Speed { get; set; }

		/// <summary><see cref="Sapphire2025Models.Diamond.TemporaryLimitTrack"/>.</summary>
		public byte Track { get; set; }

		public bool IsNewCreation { get; set; }

		/// <summary><see cref="Sapphire2025Models.Diamond.TemporaryLimitReason"/>.</summary>
		public byte Reason { get; set; }

		public DateTime CreatedUtc { get; set; }

		public bool SignaledOnTrack { get; set; } = true;

		/// <summary>Texto libre opcional (detalle del motivo, o descripción si es «Otros»).</summary>
		[MaxLength(500)]
		public string Observations { get; set; } = string.Empty;

		[ForeignKey(nameof(TopoId))]
		public DiamondTopoDocument? Topo { get; set; }
	}
}

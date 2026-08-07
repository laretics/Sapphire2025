using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sapphire2026.Data.Models.Diamond
{
	/// <summary>
	/// Artefacto topográfico Diamond versionado: documento XML completo en blob,
	/// con metadatos e huellas para enlazar mallas (.dmesh) y clientes (Tourmaline).
	/// </summary>
	[Table("DiamondTopos")]
	public class DiamondTopoDocument
	{
		[Key]
		public Guid Id { get; set; }

		/// <summary>Nombre legible (de LayoutInfo o del fichero).</summary>
		[Required]
		[MaxLength(200)]
		public string Name { get; set; } = string.Empty;

		/// <summary>SHA-256 del payload crudo (hex mayúsculas). Identidad de contenido.</summary>
		[Required]
		[MaxLength(64)]
		public string ContentHash { get; set; } = string.Empty;

		/// <summary>
		/// Huella estructural Diamond (<c>MeshBinarySerializer.ComputeTopoFingerprint</c>).
		/// La usan los planes de explotación para detectar desfase topo/malla.
		/// </summary>
		[MaxLength(64)]
		public string StructuralHash { get; set; } = string.Empty;

		/// <summary>Formato del payload: "xml" o "xml-gz".</summary>
		[Required]
		[MaxLength(16)]
		public string Format { get; set; } = "xml";

		/// <summary>Documento topográfico completo.</summary>
		[Required]
		public byte[] Payload { get; set; } = Array.Empty<byte>();

		public int ByteLength { get; set; }

		[MaxLength(260)]
		public string SourceFileName { get; set; } = string.Empty;

		[MaxLength(200)]
		public string Author { get; set; } = string.Empty;

		/// <summary>Id de layout del XML (LayoutInfo.Id), si existe.</summary>
		[MaxLength(64)]
		public string LayoutId { get; set; } = string.Empty;

		public int StationCount { get; set; }

		public int AxisCount { get; set; }

		public string Notes { get; set; } = string.Empty;

		/// <summary>False = baja lógica (sigue en BD para integridad de mallas históricas).</summary>
		public bool IsActive { get; set; } = true;

		/// <summary>Inicio de vigencia operativa (opcional).</summary>
		public DateTime? ValidFrom { get; set; }

		public DateTime CreatedUtc { get; set; }
	}
}

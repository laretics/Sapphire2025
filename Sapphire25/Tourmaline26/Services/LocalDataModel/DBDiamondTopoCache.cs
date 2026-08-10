using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tourmaline26.Services.LocalDataModel
{
	/// <summary>
	/// Metadatos de la topología Diamond en caché local (payload en disco).
	/// </summary>
	[Table("DiamondTopoCache")]
	public class DBDiamondTopoCache
	{
		[Key]
		public Guid TopoId { get; set; }

		public string Name { get; set; } = string.Empty;

		public string ContentHash { get; set; } = string.Empty;

		public string StructuralHash { get; set; } = string.Empty;

		public string LayoutId { get; set; } = string.Empty;

		/// <summary>Ruta relativa al directorio de datos de la app (p. ej. cache/diamond/topo/{id}.xml).</summary>
		public string RelativePath { get; set; } = string.Empty;

		public int ByteLength { get; set; }

		public DateTime DownloadedUtc { get; set; }

		public DateTime? ServerCreatedUtc { get; set; }
	}
}

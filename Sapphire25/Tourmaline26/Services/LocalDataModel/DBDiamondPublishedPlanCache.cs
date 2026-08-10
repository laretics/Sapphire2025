using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tourmaline26.Services.LocalDataModel
{
	/// <summary>
	/// Metadatos de un plan publicado Diamond en caché (payload en disco).
	/// </summary>
	[Table("DiamondPublishedPlanCache")]
	public class DBDiamondPublishedPlanCache
	{
		[Key]
		public Guid PlanId { get; set; }

		public Guid TopoId { get; set; }

		public string Name { get; set; } = string.Empty;

		public string ContentHash { get; set; } = string.Empty;

		public string Format { get; set; } = string.Empty;

		/// <summary>Inicio de vigencia (fecha civil).</summary>
		public DateTime ValidFrom { get; set; }

		/// <summary>Fin de vigencia (opcional).</summary>
		public DateTime? ValidTo { get; set; }

		public DateTime CompiledUtc { get; set; }

		public string RelativePath { get; set; } = string.Empty;

		public int ByteLength { get; set; }

		public int CirculationCount { get; set; }

		public int AsimilationCount { get; set; }

		public bool IsActive { get; set; } = true;

		public DateTime DownloadedUtc { get; set; }
	}
}

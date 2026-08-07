namespace Sapphire2025Models.Diamond
{
	/// <summary>Alta o actualización de un plan de explotación en el almacén.</summary>
	public class DiamondPlanSaveRequest
	{
		/// <summary>Vacío = alta; con valor = actualizar ese plan.</summary>
		public Guid? Id { get; set; }

		/// <summary>Topología del almacén a la que se ancla el plan (obligatorio).</summary>
		public Guid TopoId { get; set; }

		/// <summary>Script mini-DSL (.ddm).</summary>
		public string SourceScript { get; set; } = string.Empty;

		public string? Name { get; set; }

		public string? SourceFileName { get; set; }

		public string? Author { get; set; }

		public string? Notes { get; set; }

		public DateTime? ValidFrom { get; set; }
	}
}

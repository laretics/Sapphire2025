namespace Sapphire2025Models.Diamond
{
	/// <summary>
	/// Metadatos de una topología Diamond almacenada en Sapphire (sin payload).
	/// </summary>
	public class DiamondTopoHeaderModel
	{
		public Guid Id { get; set; }

		public string Name { get; set; } = string.Empty;

		/// <summary>SHA-256 del documento (hex).</summary>
		public string ContentHash { get; set; } = string.Empty;

		/// <summary>Huella estructural Diamond para emparejar con .dmesh.</summary>
		public string StructuralHash { get; set; } = string.Empty;

		public string Format { get; set; } = "xml";

		public int ByteLength { get; set; }

		public string SourceFileName { get; set; } = string.Empty;

		public string Author { get; set; } = string.Empty;

		public string LayoutId { get; set; } = string.Empty;

		public int StationCount { get; set; }

		public int AxisCount { get; set; }

		public string Notes { get; set; } = string.Empty;

		public bool IsActive { get; set; }

		public DateTime? ValidFrom { get; set; }

		public DateTime CreatedUtc { get; set; }
	}
}

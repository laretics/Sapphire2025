namespace Sapphire2025Models.Diamond
{
	/// <summary>
	/// Resultado de subir o registrar una topología Diamond en Sapphire.
	/// </summary>
	public class DiamondTopoUploadResult
	{
		public bool Success { get; set; }

		public string Message { get; set; } = string.Empty;

		/// <summary>True si el mismo ContentHash ya existía; no se duplicó el blob.</summary>
		public bool AlreadyExists { get; set; }

		public DiamondTopoHeaderModel? Header { get; set; }
	}
}

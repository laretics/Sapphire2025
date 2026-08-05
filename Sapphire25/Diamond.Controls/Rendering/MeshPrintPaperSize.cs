namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Formato de papel para impresión de malla (siempre apaisado).
	/// El SVG lógico es el mismo; el layout de impresión escala al área de la página.
	/// </summary>
	public enum MeshPrintPaperSize
	{
		/// <summary>420 × 297 mm.</summary>
		A3Landscape = 0,

		/// <summary>297 × 210 mm.</summary>
		A4Landscape = 1
	}
}

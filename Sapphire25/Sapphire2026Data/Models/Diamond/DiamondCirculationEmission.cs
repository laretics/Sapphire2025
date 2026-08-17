using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sapphire2026.Data.Models.Diamond
{
	/// <summary>
	/// Emisión oficial de documentación de circulación (libro / ficha).
	/// Permite verificar sellos SEL y auditar impresiones/exportaciones PDF.
	/// </summary>
	[Table("DiamondCirculationEmissions")]
	public class DiamondCirculationEmission
	{
		[Key]
		public Guid Id { get; set; }

		public DateTime EmittedAtUtc { get; set; }

		/// <summary>Usuario emisor (Id string del log Zafiro).</summary>
		[MaxLength(64)]
		public string UserId { get; set; } = string.Empty;

		/// <summary>libro | ficha</summary>
		[Required]
		[MaxLength(16)]
		public string DocumentKind { get; set; } = string.Empty;

		/// <summary>print | pdf</summary>
		[Required]
		[MaxLength(16)]
		public string Channel { get; set; } = string.Empty;

		/// <summary>Código SEL (12 hex).</summary>
		[Required]
		[MaxLength(32)]
		public string SealCode { get; set; } = string.Empty;

		/// <summary>Payload canónico firmado.</summary>
		[Required]
		[MaxLength(1024)]
		public string Payload { get; set; } = string.Empty;

		[MaxLength(200)]
		public string PlanOrTrain { get; set; } = string.Empty;

		[MaxLength(200)]
		public string EditionLabel { get; set; } = string.Empty;

		[MaxLength(120)]
		public string DayLabel { get; set; } = string.Empty;

		public int SheetCount { get; set; }

		[MaxLength(64)]
		public string CertThumbprint { get; set; } = string.Empty;

		[MaxLength(64)]
		public string PdfContentHash { get; set; } = string.Empty;

		/// <summary>Firma CMS Base64 (puede ser larga).</summary>
		public string PdfCmsSignatureBase64 { get; set; } = string.Empty;

		[MaxLength(512)]
		public string QrText { get; set; } = string.Empty;

		/// <summary>Hojas SVG empaquetadas (gzip+Base64) para reabrir el documento emitido.</summary>
		public string SvgArchive { get; set; } = string.Empty;

		/// <summary>Detalle / IP (alineado con SessionEvents.hostPoint).</summary>
		[MaxLength(255)]
		public string HostPoint { get; set; } = string.Empty;
	}
}

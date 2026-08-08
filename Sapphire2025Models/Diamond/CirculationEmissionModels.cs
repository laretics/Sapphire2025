using System;

namespace Sapphire2025Models.Diamond
{
	/// <summary>Registro de emisión oficial de documentación de circulación.</summary>
	public class CirculationEmissionModel
	{
		public Guid Id { get; set; }
		public DateTime EmittedAtUtc { get; set; }
		public string UserId { get; set; } = string.Empty;
		public string DocumentKind { get; set; } = string.Empty;
		public string Channel { get; set; } = string.Empty;
		public string SealCode { get; set; } = string.Empty;
		public string Payload { get; set; } = string.Empty;
		public string PlanOrTrain { get; set; } = string.Empty;
		public string EditionLabel { get; set; } = string.Empty;
		public string DayLabel { get; set; } = string.Empty;
		public int SheetCount { get; set; }
		public string CertThumbprint { get; set; } = string.Empty;
		public string PdfContentHash { get; set; } = string.Empty;
		public string QrText { get; set; } = string.Empty;
		public string HostPoint { get; set; } = string.Empty;
	}

	/// <summary>Petición para registrar una emisión (impresión/PDF).</summary>
	public class CirculationEmissionRegisterRequest
	{
		public Guid SessionToken { get; set; }
		public Guid EmissionId { get; set; }
		public string DocumentKind { get; set; } = string.Empty;
		public string Channel { get; set; } = string.Empty;
		public string SealCode { get; set; } = string.Empty;
		public string Payload { get; set; } = string.Empty;
		public string PlanOrTrain { get; set; } = string.Empty;
		public string EditionLabel { get; set; } = string.Empty;
		public string DayLabel { get; set; } = string.Empty;
		public int SheetCount { get; set; }
		public string CertThumbprint { get; set; } = string.Empty;
		public string PdfContentHash { get; set; } = string.Empty;
		public string PdfCmsSignatureBase64 { get; set; } = string.Empty;
		public string QrText { get; set; } = string.Empty;
	}

	public class CirculationEmissionRegisterResult
	{
		public bool Success { get; set; }
		public string Message { get; set; } = string.Empty;
		public Guid EmissionId { get; set; }
	}

	/// <summary>Verificación de sello (BD + respuesta).</summary>
	public class CirculationSealVerifyRequest
	{
		public Guid SessionToken { get; set; }
		/// <summary>Código SEL, texto QR o hex.</summary>
		public string SealOrQr { get; set; } = string.Empty;
	}

	public class CirculationSealVerifyResponse
	{
		public bool Ok { get; set; }
		public string Message { get; set; } = string.Empty;
		public string SealCode { get; set; } = string.Empty;
		public bool FoundInRegistry { get; set; }
		public CirculationEmissionModel? Emission { get; set; }
	}
}

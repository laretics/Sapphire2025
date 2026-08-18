namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Emisión oficial de documentación de circulación (impresión o PDF).
	/// Se registra en BD Zafiro / log de sesión y sirve para verificar sellos.
	/// </summary>
	public sealed class CirculationEmissionInfo
	{
		public Guid EmissionId { get; set; } = Guid.NewGuid();

		/// <summary>UTC de emisión.</summary>
		public DateTime EmittedAtUtc { get; set; } = DateTime.UtcNow;

		/// <summary>libro | ficha</summary>
		public string DocumentKind { get; set; } = string.Empty;

		/// <summary>print | pdf</summary>
		public string Channel { get; set; } = string.Empty;

		/// <summary>Código corto SEL (12 hex).</summary>
		public string SealCode { get; set; } = string.Empty;

		/// <summary>Payload canónico firmado/huella.</summary>
		public string Payload { get; set; } = string.Empty;

		/// <summary>Nombre de plan o número de tren.</summary>
		public string PlanOrTrain { get; set; } = string.Empty;

		public string EditionLabel { get; set; } = string.Empty;

		public string DayLabel { get; set; } = string.Empty;

		public int SheetCount { get; set; }

		/// <summary>Huella del certificado X.509 usado al firmar el PDF (si aplica).</summary>
		public string CertThumbprint { get; set; } = string.Empty;

		/// <summary>SHA-256 hex del PDF firmado (si channel=pdf).</summary>
		public string PdfContentHash { get; set; } = string.Empty;

		/// <summary>Firma CMS (Base64) del PDF, si se generó.</summary>
		public string PdfCmsSignatureBase64 { get; set; } = string.Empty;

		/// <summary>Usuario emisor (Id), si el host lo rellena.</summary>
		public string UserId { get; set; } = string.Empty;

		/// <summary>Texto QR embebido en las hojas.</summary>
		public string QrText { get; set; } = string.Empty;

		/// <summary>Hojas SVG empaquetadas para reabrir el documento emitido.</summary>
		public string SvgArchive { get; set; } = string.Empty;
	}

	/// <summary>Resultado de verificación de sello en UI.</summary>
	public sealed class CirculationSealVerifyResult
	{
		public bool Ok { get; set; }
		public string Message { get; set; } = string.Empty;
		public string SealCode { get; set; } = string.Empty;
		public string Payload { get; set; } = string.Empty;
		public bool FoundInRegistry { get; set; }
		public CirculationEmissionInfo? Emission { get; set; }
		public bool CryptographicMatch { get; set; }
	}
}

using System;
using System.Text;

namespace Sapphire2025Models.Diamond
{
	/// <summary>
	/// Normaliza un sello impreso, un texto QR o un hex suelto al código de 12 hex
	/// que se guarda en <c>DiamondCirculationEmissions.SealCode</c>.
	/// </summary>
	public static class CirculationSealText
	{
		public const string QrPrefix = "ZAFSEL:v1:";
		public const string SealPrefix = "SEL";

		public static string Normalize(string? sealOrQr)
		{
			if (string.IsNullOrWhiteSpace(sealOrQr))
			{
				return string.Empty;
			}

			string seal = sealOrQr.Trim();
			if (seal.StartsWith(QrPrefix, StringComparison.OrdinalIgnoreCase))
			{
				string rest = seal.Substring(QrPrefix.Length).Trim();
				int colon = rest.IndexOf(':');
				seal = colon > 0 ? rest.Substring(0, colon) : rest;
			}
			else if (seal.StartsWith(SealPrefix, StringComparison.OrdinalIgnoreCase))
			{
				seal = seal.Substring(SealPrefix.Length);
			}

			StringBuilder sb = new StringBuilder(seal.Length);
			int i = 0;
			while (i < seal.Length)
			{
				char ch = seal[i];
				if (!char.IsWhiteSpace(ch) && ch != '-')
				{
					sb.Append(char.ToLowerInvariant(ch));
				}

				i++;
			}

			return sb.ToString();
		}
	}

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
		public bool HasArchive { get; set; }
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
		/// <summary>Hojas SVG empaquetadas (gzip+Base64) para recuperar el documento.</summary>
		public string SvgArchive { get; set; } = string.Empty;
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
		public bool CryptographicMatch { get; set; }
		public bool HasArchive { get; set; }
		public CirculationEmissionModel? Emission { get; set; }
	}

	/// <summary>Copia recuperable de un documento oficial.</summary>
	public class CirculationEmissionDocumentResponse
	{
		public bool Ok { get; set; }
		public string Message { get; set; } = string.Empty;
		public CirculationEmissionModel? Emission { get; set; }
		public string SvgArchive { get; set; } = string.Empty;
		public List<string> SvgPages { get; set; } = new List<string>();
	}
}

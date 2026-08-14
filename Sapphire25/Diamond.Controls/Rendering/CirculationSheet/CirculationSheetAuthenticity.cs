using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Autenticidad de documentos de circulación (libro / ficha de marcha).
	/// · Marca de agua visual
	/// · Sello corto (SEL) firmado con certificado X.509 local o HMAC
	/// · QR verificable (ZAFSEL:v1:…)
	/// · Registro de emisiones + log Zafiro (host)
	/// </summary>
	public static class CirculationSheetAuthenticity
	{
		/// <summary>Texto diagonal de marca de agua.</summary>
		public const string DefaultWatermarkText = "DOCUMENTO DE CIRCULACIÓN · CONTROLADO";

		/// <summary>
		/// Clave HMAC opcional (servidor). Si está vacía se usa firma RSA del certificado local.
		/// </summary>
		public static string? SigningKey { get; set; }

		/// <summary>Prefijo impreso junto al código.</summary>
		public const string SealPrefix = "SEL";

		/// <summary>True si el sello se calcula con el certificado X.509 local.</summary>
		public static bool UsesCertificateSeal
		{
			get
			{
				if (!string.IsNullOrEmpty(SigningKey))
				{
					return false;
				}

				try
				{
					using X509Certificate2 cert = CirculationSheetCertificate.GetOrCreate();
					return cert.HasPrivateKey;
				}
				catch
				{
					return false;
				}
			}
		}

		public static bool HasSigningKey
		{
			get { return !string.IsNullOrEmpty(SigningKey); }
		}

		/// <summary>Payload canónico a firmar.</summary>
		public static string BuildPayload(
			string documentKind,
			string planOrTrain,
			string editionLabel,
			string dayOrDays,
			int pageNumber,
			int pageCount,
			string? extra = null)
		{
			StringBuilder sb = new StringBuilder(256);
			sb.Append("v1|");
			sb.Append(Normalize(documentKind));
			sb.Append('|');
			sb.Append(Normalize(planOrTrain));
			sb.Append('|');
			sb.Append(Normalize(editionLabel));
			sb.Append('|');
			sb.Append(Normalize(dayOrDays));
			sb.Append('|');
			sb.Append(pageNumber.ToString(CultureInfo.InvariantCulture));
			sb.Append('/');
			sb.Append(pageCount.ToString(CultureInfo.InvariantCulture));
			if (!string.IsNullOrWhiteSpace(extra))
			{
				sb.Append('|');
				sb.Append(Normalize(extra));
			}

			return sb.ToString();
		}

		/// <summary>
		/// Payload de documento completo (todas las hojas) para emisión/registro.
		/// </summary>
		public static string BuildDocumentPayload(
			string documentKind,
			string planOrTrain,
			string editionLabel,
			string dayOrDays,
			int sheetCount,
			string? extra = null)
		{
			return BuildPayload(documentKind, planOrTrain, editionLabel, dayOrDays, 0, sheetCount, extra);
		}

		/// <summary>
		/// Código corto (12 hex). Prioridad: HMAC SigningKey → RSA cert local → SHA-256.
		/// </summary>
		public static string ComputeSealCode(string payload)
		{
			byte[] data = Encoding.UTF8.GetBytes(payload ?? string.Empty);
			byte[] material;

			if (!string.IsNullOrEmpty(SigningKey))
			{
				using HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SigningKey));
				material = hmac.ComputeHash(data);
			}
			else
			{
				try
				{
					using X509Certificate2 cert = CirculationSheetCertificate.GetOrCreate();
					using RSA? rsa = cert.GetRSAPrivateKey();
					if (rsa is not null)
					{
						byte[] sig = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
						material = SHA256.HashData(sig);
					}
					else
					{
						material = SHA256.HashData(data);
					}
				}
				catch
				{
					material = SHA256.HashData(data);
				}
			}

			return ToHex12(material);
		}

		public static string FormatSealLabel(string sealCode)
		{
			if (string.IsNullOrEmpty(sealCode))
			{
				return string.Empty;
			}

			return SealPrefix + " " + sealCode;
		}

		public static bool VerifySealCode(string payload, string? printedCode)
		{
			if (string.IsNullOrWhiteSpace(printedCode))
			{
				return false;
			}

			string expected = ComputeSealCode(payload);
			string got = printedCode.Trim();
			if (got.StartsWith(SealPrefix, StringComparison.OrdinalIgnoreCase))
			{
				got = got.Substring(SealPrefix.Length).Trim();
			}

			return string.Equals(expected, got, StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Crea un registro de emisión (sello + QR) listo para registrar y firmar PDF.
		/// </summary>
		public static CirculationEmissionInfo CreateEmission(
			string documentKind,
			string channel,
			string planOrTrain,
			string editionLabel,
			string dayLabel,
			int sheetCount,
			string? extra = null)
		{
			string payload = BuildDocumentPayload(
				documentKind, planOrTrain, editionLabel, dayLabel, sheetCount, extra);
			string seal = ComputeSealCode(payload);
			string qr = CirculationSheetQr.BuildQrPayload(seal);
			string thumb;
			try
			{
				thumb = CirculationSheetCertificate.GetThumbprint();
			}
			catch
			{
				thumb = string.Empty;
			}

			return new CirculationEmissionInfo
			{
				EmissionId = Guid.NewGuid(),
				EmittedAtUtc = DateTime.UtcNow,
				DocumentKind = documentKind ?? string.Empty,
				Channel = channel ?? string.Empty,
				SealCode = seal,
				Payload = payload,
				PlanOrTrain = planOrTrain ?? string.Empty,
				EditionLabel = editionLabel ?? string.Empty,
				DayLabel = dayLabel ?? string.Empty,
				SheetCount = sheetCount,
				CertThumbprint = thumb,
				QrText = qr
			};
		}

		private static string Normalize(string? s)
		{
			if (string.IsNullOrWhiteSpace(s))
			{
				return "-";
			}

			return s.Trim().ToUpperInvariant();
		}

		private static string ToHex12(byte[] hash)
		{
			StringBuilder hex = new StringBuilder(12);
			int i = 0;
			while (i < 6 && i < hash.Length)
			{
				hex.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
				i++;
			}

			return hex.ToString();
		}
	}
}

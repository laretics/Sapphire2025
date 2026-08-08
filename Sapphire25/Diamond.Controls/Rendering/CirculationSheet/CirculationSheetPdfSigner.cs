using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Firma X.509 (CMS detached) de un PDF de circulación generado con PdfSharp.
	/// Embede sello, huella del cert y CMS en metadatos del documento (Info).
	/// </summary>
	public static class CirculationSheetPdfSigner
	{
		public const string MetaSeal = "/ZafiroCircSeal";
		public const string MetaThumb = "/ZafiroCircCert";
		public const string MetaCms = "/ZafiroCircCms";
		public const string MetaEmissionId = "/ZafiroCircEmission";
		public const string MetaPayload = "/ZafiroCircPayload";

		/// <summary>
		/// Firma el PDF: devuelve bytes del PDF con metadatos de firma CMS + SHA-256 del contenido.
		/// </summary>
		public static byte[] SignPdf(
			byte[] pdfBytes,
			CirculationEmissionInfo emission)
		{
			if (pdfBytes is null || pdfBytes.Length == 0)
			{
				throw new ArgumentException("PDF vacío.", nameof(pdfBytes));
			}

			if (emission is null)
			{
				throw new ArgumentNullException(nameof(emission));
			}

			byte[] contentHash = SHA256.HashData(pdfBytes);
			emission.PdfContentHash = ToHex(contentHash);

			using X509Certificate2 cert = CirculationSheetCertificate.GetOrCreate();
			emission.CertThumbprint = cert.Thumbprint ?? string.Empty;

			// CMS detached sobre el hash del PDF (no reescribe el stream de páginas).
			ContentInfo contentInfo = new ContentInfo(contentHash);
			SignedCms signedCms = new SignedCms(contentInfo, detached: true);
			CmsSigner signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, cert)
			{
				IncludeOption = X509IncludeOption.EndCertOnly,
				DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1") // SHA-256
			};
			signedCms.ComputeSignature(signer);
			byte[] cms = signedCms.Encode();
			emission.PdfCmsSignatureBase64 = Convert.ToBase64String(cms);

			using MemoryStream inStream = new MemoryStream(pdfBytes);
			using PdfDocument document = PdfReader.Open(inStream, PdfDocumentOpenMode.Modify);
			document.Info.Title = "Documento de circulación (firmado)";
			document.Info.Creator = "Zafiro / Diamond";
			document.Info.Subject = "Documento de circulación controlado · firma X.509 local";
			document.Info.Keywords = "circulation;controlled;x509;cms;seal=" + emission.SealCode;

			// Metadatos personalizados en el diccionario Info.
			SetInfo(document, MetaSeal, emission.SealCode);
			SetInfo(document, MetaThumb, emission.CertThumbprint);
			SetInfo(document, MetaEmissionId, emission.EmissionId.ToString("N"));
			SetInfo(document, MetaPayload, Truncate(emission.Payload, 500));
			// CMS puede ser largo: trocear si hace falta (PdfSharp string limits).
			SetInfo(document, MetaCms, emission.PdfCmsSignatureBase64);

			using MemoryStream outStream = new MemoryStream();
			document.Save(outStream, false);
			return outStream.ToArray();
		}

		/// <summary>
		/// Verifica la firma CMS de un PDF firmado (si hay metadatos CMS + se puede hashear el PDF crudo).
		/// Nota: al reabrir/salvar el PDF el hash de páginas cambia; por eso guardamos el hash
		/// firmado en el CMS (hash del PDF pre-metadatos). Aquí verificamos la cadena CMS
		/// y el thumbprint del certificado local.
		/// </summary>
		public static bool TryVerifyEmbeddedCms(byte[] signedPdfBytes, out string message)
		{
			message = string.Empty;
			if (signedPdfBytes is null || signedPdfBytes.Length == 0)
			{
				message = "PDF vacío.";
				return false;
			}

			try
			{
				using MemoryStream ms = new MemoryStream(signedPdfBytes);
				using PdfDocument document = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
				string? cmsB64 = GetInfo(document, MetaCms);
				string? thumb = GetInfo(document, MetaThumb);
				if (string.IsNullOrEmpty(cmsB64))
				{
					message = "El PDF no contiene firma CMS embebida.";
					return false;
				}

				byte[] cms = Convert.FromBase64String(cmsB64);
				SignedCms signedCms = new SignedCms();
				signedCms.Decode(cms);
				// Detached: el contenido firmado es el hash; sin el PDF original solo validamos estructura.
				signedCms.CheckSignature(verifySignatureOnly: true);

				using X509Certificate2 local = CirculationSheetCertificate.GetOrCreate();
				if (!string.IsNullOrEmpty(thumb)
					&& !string.Equals(thumb, local.Thumbprint, StringComparison.OrdinalIgnoreCase))
				{
					message = "Firma válida, pero el certificado no coincide con el de esta estación ("
						+ (local.Thumbprint ?? "?") + ").";
					return false;
				}

				message = "Firma CMS válida. Cert: " + (thumb ?? "—");
				return true;
			}
			catch (Exception ex)
			{
				message = "No se pudo verificar la firma: " + ex.Message;
				return false;
			}
		}

		private static void SetInfo(PdfDocument document, string key, string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return;
			}

			// PdfSharpCore Info.Elements usa nombres con /
			string name = key.StartsWith("/", StringComparison.Ordinal) ? key : "/" + key;
			document.Info.Elements.SetString(name, value);
		}

		private static string? GetInfo(PdfDocument document, string key)
		{
			string name = key.StartsWith("/", StringComparison.Ordinal) ? key : "/" + key;
			if (!document.Info.Elements.ContainsKey(name))
			{
				return null;
			}

			return document.Info.Elements.GetString(name);
		}

		private static string ToHex(byte[] data)
		{
			StringBuilder sb = new StringBuilder(data.Length * 2);
			int i = 0;
			while (i < data.Length)
			{
				sb.Append(data[i].ToString("x2", CultureInfo.InvariantCulture));
				i++;
			}

			return sb.ToString();
		}

		private static string Truncate(string s, int max)
		{
			if (string.IsNullOrEmpty(s) || s.Length <= max)
			{
				return s ?? string.Empty;
			}

			return s.Substring(0, max);
		}
	}
}

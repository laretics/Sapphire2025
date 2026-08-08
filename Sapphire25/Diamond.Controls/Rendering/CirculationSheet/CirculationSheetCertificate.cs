using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Certificado X.509 local para firmar documentos de circulación.
	/// Si no existe, genera un autofirmado en el directorio de datos de la app
	/// (%LocalAppData%/Zafiro/CirculationSigning/).
	/// </summary>
	public static class CirculationSheetCertificate
	{
		public const string DefaultCommonName = "Zafiro Circulation Document Signing";
		private const string PfxFileName = "circulation-signing.pfx";
		private const string PfxPassword = "ZafiroCircLocal-NotForProduction";

		/// <summary>Ruta del PFX (sobreescribible en tests).</summary>
		public static string? PfxPathOverride { get; set; }

		/// <summary>Obtiene o crea el certificado de firma con clave privada.</summary>
		public static X509Certificate2 GetOrCreate()
		{
			string path = ResolvePfxPath();
			string? dir = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}

			if (File.Exists(path))
			{
				try
				{
					return X509CertificateLoader.LoadPkcs12FromFile(
						path,
						PfxPassword,
						X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
				}
				catch
				{
					// Regenerar si el fichero está corrupto.
				}
			}

			using RSA rsa = RSA.Create(2048);
			CertificateRequest req = new CertificateRequest(
				"CN=" + DefaultCommonName,
				rsa,
				HashAlgorithmName.SHA256,
				RSASignaturePadding.Pkcs1);
			req.CertificateExtensions.Add(
				new X509BasicConstraintsExtension(false, false, 0, true));
			req.CertificateExtensions.Add(
				new X509KeyUsageExtension(
					X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation,
					true));
			req.CertificateExtensions.Add(
				new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

			DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddDays(-1);
			DateTimeOffset notAfter = notBefore.AddYears(10);
			using X509Certificate2 created = req.CreateSelfSigned(notBefore, notAfter);
			byte[] pfx = created.Export(X509ContentType.Pkcs12, PfxPassword);
			File.WriteAllBytes(path, pfx);

			return X509CertificateLoader.LoadPkcs12(
				pfx,
				PfxPassword,
				X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
		}

		/// <summary>Huella SHA-1 del certificado (hex mayúsculas), para metadatos.</summary>
		public static string GetThumbprint()
		{
			using X509Certificate2 cert = GetOrCreate();
			return cert.Thumbprint ?? string.Empty;
		}

		public static string ResolvePfxPath()
		{
			if (!string.IsNullOrWhiteSpace(PfxPathOverride))
			{
				return PfxPathOverride;
			}

			string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			return Path.Combine(root, "Zafiro", "CirculationSigning", PfxFileName);
		}
	}
}

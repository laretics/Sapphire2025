using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Diamond.Timed
{
	/// <summary>
	/// Firma digital RSA-SHA256 del plan de explotación (.dmesh).
	/// Exportación: se firma el payload con la clave <b>privada</b> (solo el planificador Diamond).
	/// Carga: se verifica con la clave <b>pública</b> (Sapphire, Tourmaline, etc.).
	/// Si el archivo se manipula, la verificación falla y no se abre.
	/// </summary>
	/// <remarks>
	/// No es cifrado confidencial: el contenido no es secreto; la firma garantiza
	/// autenticidad e integridad (equivalente práctico a “solo tiene sentido si es válido”).
	/// En producción, la clave privada no debe distribuirse a clientes/HMI.
	/// </remarks>
	public static class MeshSigning
	{
		/// <summary>Magic del contenedor firmado (Diamond SiGNed).</summary>
		public static readonly byte[] ContainerMagic = { (byte)'D', (byte)'S', (byte)'G', (byte)'N' };

		public const int ContainerVersion = 1;

		// Clave RSA-2048 del planificador Diamond (desarrollo). Rotar en producción
		// y no empaquetar la privada en clientes que solo lean planes.
		private const string PrivateKeyPem = @"-----BEGIN RSA PRIVATE KEY-----
MIIEpQIBAAKCAQEAny859Nuo1MwNehRSaYhN/nZ+SMEFijUuuj7olGHTXxiIj4kr
ZLufjBl8PvbJ35jKpLlbqaSAcU5Vafto2yYJPcBlhreT1lmE1ujYHwYlEPbR+TrX
gytN8DOFY7cUJegZly/68rMQct1BjbXt9uPBizMcIgoTLJ+c00Qw0ao5Qf443yXV
74oBd3mNwB5nzsNLyZWrrGjWAdcFgENDW8QkwyWvfwRjMBQ+jy6MLBrTd2chGtGy
E24zHUhxLFo0pPJ9fhOwLkK5Bk0DAAqjO5VB/3ZDTMrYGxMYXvsi+xkqX0fKrC3n
Zf2yGo+kexsadEQsGI9XUmw517JEcEfoU2U2jQIDAQABAoIBAEOW9uHb/vTT64kB
Zfi/UnaQc5CKvMJxUHTuTyzp2KkRLrLbiFxOOjFvhMzrVs4k/y4wMhZ27b6uhx/o
Cx66KMRalAE+o3wpKHlkBpgtsMFiO8e6T1W3pnEuPx4W12w8/9itG72/gGV1Qudb
7z2ceUqUmgZuBrQ01Y7y7lOoFoXJM2CkB+kFjElStoMuhN5ECv+9VbiS5LFgdqMP
EQ1V2yN+EfipGO25E5tTbdyM3qU/S3QcvqAFAnQuOGPyY8mEHq4V77vfqT5/dcCY
w899h09oigXTURpPiJnjyq2kaxosJIuPy7JZWeRTLQItLm99hB6DGjfmo6f/4x+q
kWdcBlUCgYEA0qw+PVSQMYxOLtQmVWXDOBOFWGZt5P8ZbHzEKTD881dq/PHUI4lD
GOLreE5pTTtEvNGEok2O3s6E1rzTpm/0hEeH/rDNqvRV8XPgFa5cKROdE2H49PKD
pw8MeZePO4G1fzkRuyABNAHkuCMwNAIaVYVoq810ajo4E5pgKoJqZK8CgYEAwW8H
t2VyoQUOsAlVjLHMX0xDoLVDagoZU+jwj6JktxIhqlkNmzohtw+scwAQ5VTDMi5Y
FdQZNZ/7ZOkHLc3h+hzdGAp1FzaVJ2MYESanCRuKf4mMYAUpyWnyBgaaQXR4y7bt
Vbwu/ycXmJ94mPW86tdrg4mYxeKbqHtT7eUFn4MCgYEAueHSNMkFRUKXxUfY3uag
Hj1THZGbCz9uTkOzzOh1d7w9IRu64vP8qNunigodbVCLMNLo5U/rbHnpyZLoaGVF
6OU4gCe+AEYAAsAMCvVCSdgNcwmx4WaNdPyZNDve3FFxM+TF2Ua+6QV2qmYZ5Pmq
BKVmmpf4TLY7vkgq67cQau8CgYEAglzZznfIcUx/QZSI23FYU0GKslx3OpfjyfFL
bwH67oaBudSanurmngEqL7bzrBscEk25f7yZ4NIZE0EZXX1LtsyNFQ63KxXWH91g
8u4h1LmC9cE1oeDY8K0+N2FrxIFCh8iY2xcgdUVbWbjgBfZXg83634N0OqkmTr4P
Kl0XFRECgYEApr1eeSymU7A7SDOx1u3l5Ezetoga/4jaHknYARMo3UNWEgi9RfWx
enK7m9ISutnvlYDP47SM1nJ5O0oJ0TGeLt0UPYWGfjwAWOUFkVgIkZAimDFe+DSs
n7UMAAAgk4QaihGbQS9ECfFAuMNI1RopcbRx+Qf2x1vbAf/aTSdk6PA=
-----END RSA PRIVATE KEY-----";

		private const string PublicKeyPem = @"-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAny859Nuo1MwNehRSaYhN
/nZ+SMEFijUuuj7olGHTXxiIj4krZLufjBl8PvbJ35jKpLlbqaSAcU5Vafto2yYJ
PcBlhreT1lmE1ujYHwYlEPbR+TrXgytN8DOFY7cUJegZly/68rMQct1BjbXt9uPB
izMcIgoTLJ+c00Qw0ao5Qf443yXV74oBd3mNwB5nzsNLyZWrrGjWAdcFgENDW8Qk
wyWvfwRjMBQ+jy6MLBrTd2chGtGyE24zHUhxLFo0pPJ9fhOwLkK5Bk0DAAqjO5VB
/3ZDTMrYGxMYXvsi+xkqX0fKrC3nZf2yGo+kexsadEQsGI9XUmw517JEcEfoU2U2
jQIDAQAB
-----END PUBLIC KEY-----";

		/// <summary>Firma el payload (contenido DMSH) con la clave privada.</summary>
		public static byte[] Sign(byte[] payload)
		{
			if (payload is null || payload.Length == 0)
			{
				throw new ArgumentException("Payload vacío.", nameof(payload));
			}

			using RSA rsa = RSA.Create();
			rsa.ImportFromPem(PrivateKeyPem);
			return rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		}

		/// <summary>Verifica la firma del payload con la clave pública.</summary>
		public static bool Verify(byte[] payload, byte[] signature)
		{
			if (payload is null || signature is null || payload.Length == 0 || signature.Length == 0)
			{
				return false;
			}

			using RSA rsa = RSA.Create();
			rsa.ImportFromPem(PublicKeyPem);
			return rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		}

		/// <summary>
		/// Escribe contenedor firmado: DSGN + payload + firma.
		/// </summary>
		public static void WriteSignedContainer(Stream output, byte[] payload)
		{
			if (output is null)
			{
				throw new ArgumentNullException(nameof(output));
			}

			byte[] signature = Sign(payload);
			using BinaryWriter w = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
			w.Write(ContainerMagic);
			w.Write(ContainerVersion);
			w.Write(payload.Length);
			w.Write(payload);
			w.Write(signature.Length);
			w.Write(signature);
			w.Flush();
		}

		/// <summary>
		/// Lee y verifica el contenedor. Lanza si la firma no es válida o está ausente.
		/// </summary>
		public static byte[] ReadAndVerifyContainer(Stream input)
		{
			if (input is null)
			{
				throw new ArgumentNullException(nameof(input));
			}

			using BinaryReader r = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
			byte[] magic = r.ReadBytes(4);
			if (magic.Length != 4
				|| magic[0] != ContainerMagic[0] || magic[1] != ContainerMagic[1]
				|| magic[2] != ContainerMagic[2] || magic[3] != ContainerMagic[3])
			{
				// ¿Payload crudo DMSH sin firmar?
				if (magic.Length == 4
					&& magic[0] == (byte)'D' && magic[1] == (byte)'M'
					&& magic[2] == (byte)'S' && magic[3] == (byte)'H')
				{
					throw new InvalidDataException(
						"El plan de explotación no está firmado. Exporte de nuevo desde Diamond.");
				}

				throw new InvalidDataException("No es un plan de explotación firmado Diamond (magic DSGN).");
			}

			int version = r.ReadInt32();
			if (version != ContainerVersion)
			{
				throw new InvalidDataException(
					"Versión de contenedor firmado no soportada: " + version + ".");
			}

			int payloadLen = r.ReadInt32();
			if (payloadLen <= 0 || payloadLen > 200_000_000)
			{
				throw new InvalidDataException("Longitud de payload inválida.");
			}

			byte[] payload = r.ReadBytes(payloadLen);
			if (payload.Length != payloadLen)
			{
				throw new EndOfStreamException("Payload truncado.");
			}

			int sigLen = r.ReadInt32();
			if (sigLen <= 0 || sigLen > 8192)
			{
				throw new InvalidDataException("Longitud de firma inválida.");
			}

			byte[] signature = r.ReadBytes(sigLen);
			if (signature.Length != sigLen)
			{
				throw new EndOfStreamException("Firma truncada.");
			}

			if (!Verify(payload, signature))
			{
				throw new CryptographicException(
					"Firma digital no válida: el archivo ha sido manipulado o no proviene de Diamond.");
			}

			return payload;
		}
	}
}

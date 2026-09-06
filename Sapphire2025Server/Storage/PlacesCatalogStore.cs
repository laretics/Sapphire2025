using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Sapphire2025Models.Diamond;

namespace Sapphire2025Server.Storage
{
	/// <summary>
	/// places.xml en disco del servidor (App_Data/catalog). Una sola copia
	/// editada desde Zafiro; Tourmaline la descarga si el hash cambia.
	/// </summary>
	public static class PlacesCatalogStore
	{
		public const int MaxXmlBytes = PlacesXmlValidator.MaxXmlBytes;

		public static string GetFilePath(IConfiguration config)
		{
			string? configured = config["Catalog:PlacesPath"];
			if (!string.IsNullOrWhiteSpace(configured))
			{
				if (Path.IsPathRooted(configured))
					return Path.GetFullPath(configured);
				return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured));
			}

			return Path.GetFullPath(Path.Combine(
				AppContext.BaseDirectory,
				"App_Data",
				"catalog",
				"places.xml"));
		}

		public static PlacesCatalogHeaderModel GetHeader(IConfiguration config)
		{
			string path = GetFilePath(config);
			if (!File.Exists(path))
			{
				return new PlacesCatalogHeaderModel
				{
					Exists = false,
					ContentHash = string.Empty,
					ByteLength = 0,
					UpdatedUtc = DateTime.MinValue
				};
			}

			FileInfo info = new FileInfo(path);
			byte[] bytes = File.ReadAllBytes(path);
			return new PlacesCatalogHeaderModel
			{
				Exists = true,
				ContentHash = Sha256Hex(bytes),
				ByteLength = bytes.Length,
				UpdatedUtc = info.LastWriteTimeUtc
			};
		}

		public static PlacesCatalogContentModel? ReadContent(IConfiguration config)
		{
			string path = GetFilePath(config);
			if (!File.Exists(path))
				return null;

			byte[] bytes = File.ReadAllBytes(path);
			FileInfo info = new FileInfo(path);
			return new PlacesCatalogContentModel
			{
				Xml = Encoding.UTF8.GetString(bytes),
				ContentHash = Sha256Hex(bytes),
				ByteLength = bytes.Length,
				UpdatedUtc = info.LastWriteTimeUtc
			};
		}

		public static IReadOnlyList<PlacesXmlIssue> ValidateXml(string? xml) =>
			PlacesXmlValidator.Validate(xml);

		public static PlacesCatalogHeaderModel WriteXml(IConfiguration config, string xml)
		{
			string path = GetFilePath(config);
			string? dir = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(dir))
				Directory.CreateDirectory(dir);

			byte[] bytes = Encoding.UTF8.GetBytes(xml);
			string temp = path + ".tmp";
			File.WriteAllBytes(temp, bytes);
			if (File.Exists(path))
			{
				File.Copy(temp, path, overwrite: true);
				File.Delete(temp);
			}
			else
				File.Move(temp, path);

			return GetHeader(config);
		}

		public static string Sha256Hex(byte[] payload)
		{
			byte[] hash = SHA256.HashData(payload);
			StringBuilder sb = new StringBuilder(hash.Length * 2);
			foreach (byte b in hash)
				sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
			return sb.ToString();
		}
	}
}

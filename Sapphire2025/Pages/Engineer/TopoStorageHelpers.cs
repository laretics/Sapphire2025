using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Sapphire2025.Pages.Engineer
{
	internal static class TopoStorageHelpers
	{
		public static string FormatBytes(int bytes)
		{
			if (bytes < 1024)
			{
				return bytes.ToString() + " B";
			}

			double kb = bytes / 1024.0;
			if (kb < 1024)
			{
				return kb.ToString("0.#", CultureInfo.InvariantCulture) + " KB";
			}

			return (kb / 1024.0).ToString("0.##", CultureInfo.InvariantCulture) + " MB";
		}

		public static string ShortGuid(Guid id)
		{
			return id.ToString("N").Substring(0, 8) + "…";
		}

		public static string ShortHash(string? hash)
		{
			if (string.IsNullOrWhiteSpace(hash))
			{
				return "—";
			}

			return hash.Length <= 12 ? hash : hash.Substring(0, 12) + "…";
		}

		public static string SanitizeFileName(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				return "export";
			}

			char[] invalid = Path.GetInvalidFileNameChars();
			StringBuilder sb = new StringBuilder(name.Length);
			int i = 0;
			while (i < name.Length)
			{
				char c = name[i];
				sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
				i++;
			}

			string result = sb.ToString().Trim();
			return result.Length == 0 ? "export" : result;
		}

		public static async Task<byte[]> MaterializeXmlAsync(byte[] payload, string format)
		{
			if (string.Equals(format, "xml-gz", StringComparison.OrdinalIgnoreCase)
				|| (payload.Length >= 2 && payload[0] == 0x1f && payload[1] == 0x8b))
			{
				using MemoryStream input = new MemoryStream(payload, writable: false);
				using GZipStream gzip = new GZipStream(input, CompressionMode.Decompress);
				using MemoryStream output = new MemoryStream();
				await gzip.CopyToAsync(output);
				return output.ToArray();
			}

			return payload;
		}

		public static string Friendly(Exception ex)
		{
			string msg = string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;
			if (ex.InnerException is not null && !string.IsNullOrWhiteSpace(ex.InnerException.Message))
			{
				msg += " → " + ex.InnerException.Message;
			}

			return msg;
		}
	}
}

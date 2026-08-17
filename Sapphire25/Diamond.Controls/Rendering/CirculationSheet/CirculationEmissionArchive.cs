using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Empaqueta las hojas SVG de una emisión oficial (gzip + Base64)
	/// para guardarlas en <c>DiamondCirculationEmissions.SvgArchive</c>.
	/// </summary>
	public static class CirculationEmissionArchive
	{
		public static string Pack(IReadOnlyList<string> pages)
		{
			if (pages is null || pages.Count == 0)
			{
				return string.Empty;
			}

			List<string> copy = new List<string>(pages.Count);
			int i = 0;
			while (i < pages.Count)
			{
				copy.Add(pages[i] ?? string.Empty);
				i++;
			}

			byte[] json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(copy));
			using MemoryStream output = new MemoryStream();
			using (GZipStream gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
			{
				gzip.Write(json, 0, json.Length);
			}

			return Convert.ToBase64String(output.ToArray());
		}

		public static IReadOnlyList<string> Unpack(string? archive)
		{
			if (string.IsNullOrWhiteSpace(archive))
			{
				return Array.Empty<string>();
			}

			try
			{
				byte[] packed = Convert.FromBase64String(archive.Trim());
				using MemoryStream input = new MemoryStream(packed);
				using GZipStream gzip = new GZipStream(input, CompressionMode.Decompress);
				using MemoryStream json = new MemoryStream();
				gzip.CopyTo(json);
				List<string>? pages = JsonSerializer.Deserialize<List<string>>(json.ToArray());
				if (pages is null)
				{
					return Array.Empty<string>();
				}

				return pages;
			}
			catch
			{
				return Array.Empty<string>();
			}
		}
	}
}

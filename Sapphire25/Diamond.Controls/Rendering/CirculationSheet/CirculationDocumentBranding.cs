using System.Globalization;
using System.Text;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Logotipo de empresa en consignas y libros itinerario.
	/// Por defecto <c>img/sfmImg.png</c> (wwwroot); se cambia con
	/// <c>Diamond:Documents:CompanyLogo</c> en appsettings.json.
	/// </summary>
	public static class CirculationDocumentBranding
	{
		public const string DefaultRelativePath = "img/sfmImg.png";
		public const double CoverLogoW = 180;
		public const double CoverLogoH = 52;
		public const double CoverLogoGapAfterHeader = 16;
		public const double CoverLogoGapAfter = 16;
		public const double HeaderLogoGap = 4;
		public const string HeaderGrayFilterId = "diamond-logo-gray";

		private static string mvarLogoPath = DefaultRelativePath;
		private static string? mvarDataUri;

		public static string LogoPath
		{
			get { return mvarLogoPath; }
		}

		public static string ImageHref
		{
			get
			{
				EnsureDefaultEmbedded();
				if (!string.IsNullOrEmpty(mvarDataUri))
				{
					return mvarDataUri;
				}

				return mvarLogoPath;
			}
		}

		public static void Configure(string? relativeOrAbsolutePath)
		{
			mvarDataUri = null;
			if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath))
			{
				mvarLogoPath = DefaultRelativePath;
				return;
			}

			string path = relativeOrAbsolutePath.Trim().Replace('\\', '/');
			if (path.StartsWith("~/", StringComparison.Ordinal))
			{
				path = path.Substring(2);
			}

			if (path.StartsWith("/", StringComparison.Ordinal)
				&& !path.StartsWith("//", StringComparison.Ordinal))
			{
				path = path.TrimStart('/');
			}

			mvarLogoPath = path;
		}

		public static void SetDataUri(string? dataUri)
		{
			mvarDataUri = string.IsNullOrWhiteSpace(dataUri) ? null : dataUri.Trim();
		}

		/// <summary>Carga el PNG/JPG desde wwwroot (host servidor / tests).</summary>
		public static bool TryLoadFromWebRoot(string? webRootPath)
		{
			if (string.IsNullOrWhiteSpace(webRootPath))
			{
				return false;
			}

			string full = Path.Combine(webRootPath, mvarLogoPath.Replace('/', Path.DirectorySeparatorChar));
			if (!File.Exists(full))
			{
				return false;
			}

			try
			{
				byte[] bytes = File.ReadAllBytes(full);
				string mime = MimeFromPath(full);
				mvarDataUri = "data:" + mime + ";base64," + Convert.ToBase64String(bytes);
				return true;
			}
			catch
			{
				return false;
			}
		}

		public static void ApplyFromConfiguration(string? configuredPath, string? webRootPath = null)
		{
			Configure(configuredPath);
			if (!string.IsNullOrWhiteSpace(webRootPath))
			{
				TryLoadFromWebRoot(webRootPath);
			}
		}

		public static double HeaderLogoWidth(double qrSize)
		{
			return qrSize * 1.275;
		}

		public static double HeaderLogoHeight(double qrSize)
		{
			return qrSize * 0.5;
		}

		public static void AppendGrayFilterDefs(StringBuilder sb)
		{
			if (sb is null)
			{
				return;
			}

			sb.Append("<defs><filter id=\"")
				.Append(HeaderGrayFilterId)
				.Append("\" color-interpolation-filters=\"sRGB\">")
				.Append("<feColorMatrix type=\"saturate\" values=\"0\"/>")
				.Append("</filter></defs>");
		}

		public static void AppendImage(
			StringBuilder sb,
			double x,
			double y,
			double width,
			double height,
			bool grayscale = false)
		{
			string href = ImageHref;
			if (string.IsNullOrEmpty(href) || sb is null)
			{
				return;
			}

			string extra = grayscale
				? " filter=\"url(#" + HeaderGrayFilterId + ")\" class=\"diamond-doc-logo diamond-doc-logo-gray\""
				: " class=\"diamond-doc-logo\"";
			sb.Append(CultureInfo.InvariantCulture,
				$"<image href=\"{CirculationSheetSvgRenderer.XmlEscape(href)}\" x=\"{CirculationSheetSvgRenderer.F(x)}\" y=\"{CirculationSheetSvgRenderer.F(y)}\" width=\"{CirculationSheetSvgRenderer.F(width)}\" height=\"{CirculationSheetSvgRenderer.F(height)}\" preserveAspectRatio=\"xMidYMid meet\"{extra}/>");
		}

		private static void EnsureDefaultEmbedded()
		{
			if (!string.IsNullOrEmpty(mvarDataUri))
			{
				return;
			}

			bool useDefault = string.Equals(mvarLogoPath, DefaultRelativePath, StringComparison.OrdinalIgnoreCase)
				|| mvarLogoPath.EndsWith("sfmImg.png", StringComparison.OrdinalIgnoreCase);
			if (!useDefault)
			{
				return;
			}

			try
			{
				using Stream? stream = typeof(CirculationDocumentBranding).Assembly
					.GetManifestResourceStream("Diamond.Controls.sfmImg.png");
				if (stream is null)
				{
					return;
				}

				using MemoryStream ms = new MemoryStream();
				stream.CopyTo(ms);
				mvarDataUri = "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
			}
			catch
			{
			}
		}

		private static string MimeFromPath(string path)
		{
			string ext = Path.GetExtension(path);
			if (string.Equals(ext, ".jpg", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(ext, ".jpeg", StringComparison.OrdinalIgnoreCase))
			{
				return "image/jpeg";
			}

			if (string.Equals(ext, ".svg", StringComparison.OrdinalIgnoreCase))
			{
				return "image/svg+xml";
			}

			if (string.Equals(ext, ".gif", StringComparison.OrdinalIgnoreCase))
			{
				return "image/gif";
			}

			if (string.Equals(ext, ".webp", StringComparison.OrdinalIgnoreCase))
			{
				return "image/webp";
			}

			return "image/png";
		}
	}
}

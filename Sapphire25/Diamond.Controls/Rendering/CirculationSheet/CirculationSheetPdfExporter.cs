using System.Globalization;
using System.Text;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Exporta las hojas del libro/ficha a PDF multipágina (A4 apaisado).
	/// 1) Preferente: Svg.Skia + SkiaSharp (servidor / desktop con nativos).
	/// 2) Fallback: PNG ya rasterizados en el navegador (sin libSkiaSharp).
	/// </summary>
	public static class CirculationSheetPdfExporter
	{
		/// <summary>A4 apaisado en puntos PDF (1 pt = 1/72").</summary>
		public static readonly double PageWidthPt = 297.0 * 72.0 / 25.4;

		public static readonly double PageHeightPt = 210.0 * 72.0 / 25.4;

		/// <summary>
		/// True si conviene intentar Skia nativo. En el navegador (WASM) siempre false
		/// y <b>no se toca</b> ningún tipo de SkiaSharp (evita DllNotFoundException de libSkiaSharp).
		/// </summary>
		public static bool IsSkiaAvailable
		{
			get
			{
				// Blazor WebAssembly: el PDF se compone en el cliente; no hay libSkiaSharp.dll.
				if (OperatingSystem.IsBrowser())
				{
					return false;
				}

				// Desktop/servidor: solo comprobar si el ensamblado nativo responde.
				// La implementación real está en CirculationSheetSkiaRaster (carga diferida).
				return CirculationSheetSkiaRaster.TryProbeNative();
			}
		}

		/// <summary>
		/// Genera un PDF rasterizando SVG con Skia (solo no-browser con nativos).
		/// En WASM o sin DLL nativa lanza; la UI debe usar <see cref="ExportPngBase64PagesToPdf"/>.
		/// </summary>
		public static byte[] ExportSvgSheetsToPdf(
			IReadOnlyList<string> svgSheets,
			CirculationEmissionInfo? emission = null)
		{
			if (svgSheets is null || svgSheets.Count == 0)
			{
				throw new ArgumentException("No hay hojas SVG para exportar.", nameof(svgSheets));
			}

			if (OperatingSystem.IsBrowser() || !CirculationSheetSkiaRaster.TryProbeNative())
			{
				throw new InvalidOperationException(
					"SkiaSharp nativo no está disponible aquí. "
					+ "El PDF de la ficha se compone en el proceso Blazor: "
					+ "en WebAssembly es el navegador (use rasterizado JS); "
					+ "en Interactive Server es el .exe host (añada SkiaSharp.NativeAssets.* a ese proyecto).");
			}

			List<byte[]> pngPages = new List<byte[]>(svgSheets.Count);
			int i = 0;
			while (i < svgSheets.Count)
			{
				string svg = svgSheets[i];
				if (!string.IsNullOrWhiteSpace(svg))
				{
					pngPages.Add(CirculationSheetSkiaRaster.RasterizeSvgToPng(svg));
				}

				i++;
			}

			return ExportPngPagesToPdf(pngPages, emission);
		}

		/// <summary>
		/// PDF a partir de PNG (bytes) por página — sin Skia (ruta WASM / fallback).
		/// </summary>
		public static byte[] ExportPngPagesToPdf(
			IReadOnlyList<byte[]> pngPages,
			CirculationEmissionInfo? emission = null)
		{
			if (pngPages is null || pngPages.Count == 0)
			{
				throw new ArgumentException("No hay páginas PNG para exportar.", nameof(pngPages));
			}

			using PdfDocument document = new PdfDocument();
			document.Info.Title = "Documento de circulación (controlado)";
			document.Info.Creator = "Zafiro / Diamond";
			document.Info.Subject = "Documento de circulación · uso controlado · no copiar sin autorización";
			document.Info.Keywords = emission is null
				? "circulation;controlled"
				: "circulation;controlled;signed;seal=" + emission.SealCode;

			int i = 0;
			while (i < pngPages.Count)
			{
				byte[] png = pngPages[i];
				if (png is null || png.Length == 0)
				{
					i++;
					continue;
				}

				PdfPage page = document.AddPage();
				page.Orientation = PdfSharpCore.PageOrientation.Landscape;
				page.Width = PageWidthPt;
				page.Height = PageHeightPt;

				using (XGraphics gfx = XGraphics.FromPdfPage(page))
				{
					gfx.DrawRectangle(XBrushes.White, 0, 0, page.Width, page.Height);
					using MemoryStream pngStream = new MemoryStream(png);
					using XImage ximg = XImage.FromStream(() => pngStream);
					gfx.DrawImage(ximg, 0, 0, page.Width, page.Height);
				}

				i++;
			}

			if (document.PageCount == 0)
			{
				throw new InvalidOperationException("El PDF no contiene páginas.");
			}

			using MemoryStream outStream = new MemoryStream();
			document.Save(outStream, false);
			byte[] raw = outStream.ToArray();
			if (emission is not null)
			{
				try
				{
					return CirculationSheetPdfSigner.SignPdf(raw, emission);
				}
				catch
				{
					// Firma X.509 no disponible (p. ej. browser): devolver PDF sin CMS.
					return raw;
				}
			}

			return raw;
		}

		/// <summary>
		/// PDF a partir de PNG en Base64 (resultado de <c>diamondCircSheet.rasterizeSvgs</c>).
		/// </summary>
		public static byte[] ExportPngBase64PagesToPdf(
			IReadOnlyList<string> pngBase64Pages,
			CirculationEmissionInfo? emission = null)
		{
			if (pngBase64Pages is null || pngBase64Pages.Count == 0)
			{
				throw new ArgumentException("No hay páginas para exportar.", nameof(pngBase64Pages));
			}

			List<byte[]> pngs = new List<byte[]>(pngBase64Pages.Count);
			int i = 0;
			while (i < pngBase64Pages.Count)
			{
				string b64 = pngBase64Pages[i] ?? string.Empty;
				if (b64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
				{
					int comma = b64.IndexOf(',');
					if (comma >= 0)
					{
						b64 = b64.Substring(comma + 1);
					}
				}

				if (b64.Length > 0)
				{
					pngs.Add(Convert.FromBase64String(b64));
				}

				i++;
			}

			return ExportPngPagesToPdf(pngs, emission);
		}

	}
}

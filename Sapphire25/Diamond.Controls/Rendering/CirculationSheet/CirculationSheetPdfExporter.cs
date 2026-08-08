using System.Globalization;
using System.Text;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SkiaSharp;
using Svg.Skia;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Exporta las hojas SVG del libro/ficha a un PDF multipágina (A4 apaisado).
	/// Usa PdfSharpCore + Svg.Skia (código abierto).
	/// </summary>
	public static class CirculationSheetPdfExporter
	{
		/// <summary>A4 apaisado en puntos PDF (1 pt = 1/72").</summary>
		public static readonly double PageWidthPt = 297.0 * 72.0 / 25.4;

		public static readonly double PageHeightPt = 210.0 * 72.0 / 25.4;

		/// <summary>
		/// Genera un PDF con una página por cada SVG de hoja física.
		/// Si <paramref name="emission"/> no es null, firma el PDF con el certificado X.509 local
		/// y rellena hash/CMS en la emisión.
		/// </summary>
		public static byte[] ExportSvgSheetsToPdf(
			IReadOnlyList<string> svgSheets,
			CirculationEmissionInfo? emission = null)
		{
			if (svgSheets is null || svgSheets.Count == 0)
			{
				throw new ArgumentException("No hay hojas SVG para exportar.", nameof(svgSheets));
			}

			using PdfDocument document = new PdfDocument();
			document.Info.Title = "Documento de circulación (controlado)";
			document.Info.Creator = "Zafiro / Diamond";
			document.Info.Subject = "Documento de circulación · uso controlado · no copiar sin autorización";
			document.Info.Keywords = emission is null
				? "circulation;controlled"
				: "circulation;controlled;signed;seal=" + emission.SealCode;

			int i = 0;
			while (i < svgSheets.Count)
			{
				string svg = svgSheets[i];
				if (string.IsNullOrWhiteSpace(svg))
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
					// Fondo blanco
					gfx.DrawRectangle(XBrushes.White, 0, 0, page.Width, page.Height);

					using SKSvg skSvg = new SKSvg();
					try
					{
						skSvg.FromSvg(EnsureSvgXmlns(svg));
					}
					catch (Exception ex)
					{
						// Fallback: mensaje en la página
						gfx.DrawString(
							"Error SVG p." + (i + 1).ToString(CultureInfo.InvariantCulture) + ": " + ex.Message,
							new XFont("Arial", 10),
							XBrushes.Red,
							new XRect(20, 20, page.Width - 40, 40),
							XStringFormats.TopLeft);
						i++;
						continue;
					}

					if (skSvg.Picture is null)
					{
						i++;
						continue;
					}

					// Rasterizar a PNG a buena resolución (≈150 dpi sobre A4).
					const int dpi = 150;
					int pxW = (int)Math.Round(297.0 / 25.4 * dpi);
					int pxH = (int)Math.Round(210.0 / 25.4 * dpi);

					SKRect? cull = skSvg.Picture.CullRect;
					float srcW = cull.HasValue && cull.Value.Width > 1 ? cull.Value.Width : (float)CirculationSheetSvgRenderer.SheetWidth;
					float srcH = cull.HasValue && cull.Value.Height > 1 ? cull.Value.Height : (float)CirculationSheetSvgRenderer.SheetHeight;

					using SKBitmap bitmap = new SKBitmap(pxW, pxH, SKColorType.Rgba8888, SKAlphaType.Premul);
					using (SKCanvas canvas = new SKCanvas(bitmap))
					{
						canvas.Clear(SKColors.White);
						float scale = Math.Min(pxW / srcW, pxH / srcH);
						float dx = (pxW - srcW * scale) * 0.5f;
						float dy = (pxH - srcH * scale) * 0.5f;
						canvas.Translate(dx, dy);
						canvas.Scale(scale);
						if (cull.HasValue)
						{
							canvas.Translate(-cull.Value.Left, -cull.Value.Top);
						}

						canvas.DrawPicture(skSvg.Picture);
					}

					using MemoryStream pngStream = new MemoryStream();
					using (SKImage image = SKImage.FromBitmap(bitmap))
					using (SKData data = image.Encode(SKEncodedImageFormat.Png, 95))
					{
						data.SaveTo(pngStream);
					}

					pngStream.Position = 0;
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
				return CirculationSheetPdfSigner.SignPdf(raw, emission);
			}

			return raw;
		}

		private static string EnsureSvgXmlns(string svg)
		{
			if (svg.IndexOf("xmlns=", StringComparison.Ordinal) >= 0)
			{
				return svg;
			}

			// Insertar xmlns en la etiqueta <svg …>
			int idx = svg.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
			if (idx < 0)
			{
				return svg;
			}

			int insertAt = idx + 4;
			return svg.Substring(0, insertAt)
				+ " xmlns=\"http://www.w3.org/2000/svg\""
				+ svg.Substring(insertAt);
		}
	}
}

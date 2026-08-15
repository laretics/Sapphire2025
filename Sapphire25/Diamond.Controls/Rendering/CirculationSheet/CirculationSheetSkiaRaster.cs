namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Rasterizado SVG→PNG con Svg.Skia / SkiaSharp.
	/// Separado del exportador principal para no cargar tipos Skia en WASM
	/// (libSkiaSharp no existe en el navegador).
	/// </summary>
	internal static class CirculationSheetSkiaRaster
	{
		private static int svarProbe; // 0 = no, 1 = ok, -1 = fail

		/// <summary>
		/// Comprueba nativos sin contaminar el camino WASM (no llamar si IsBrowser).
		/// </summary>
		public static bool TryProbeNative()
		{
			if (OperatingSystem.IsBrowser())
			{
				return false;
			}

			if (svarProbe != 0)
			{
				return svarProbe > 0;
			}

			try
			{
				using SkiaSharp.SKBitmap bmp = new SkiaSharp.SKBitmap(1, 1);
				svarProbe = bmp.Width == 1 ? 1 : -1;
			}
			catch
			{
				svarProbe = -1;
			}

			return svarProbe > 0;
		}

		public static byte[] RasterizeSvgToPng(string svg)
		{
			using Svg.Skia.SKSvg skSvg = new Svg.Skia.SKSvg();
			skSvg.FromSvg(EnsureSvgXmlns(svg));
			if (skSvg.Picture is null)
			{
				throw new InvalidOperationException("SVG sin picture.");
			}

			const int dpi = 150;
			int pxW = (int)Math.Round(297.0 / 25.4 * dpi);
			int pxH = (int)Math.Round(210.0 / 25.4 * dpi);

			SkiaSharp.SKRect? cull = skSvg.Picture.CullRect;
			float srcW = cull.HasValue && cull.Value.Width > 1
				? cull.Value.Width
				: (float)CirculationSheetSvgRenderer.SheetWidth;
			float srcH = cull.HasValue && cull.Value.Height > 1
				? cull.Value.Height
				: (float)CirculationSheetSvgRenderer.SheetHeight;

			using SkiaSharp.SKBitmap bitmap = new SkiaSharp.SKBitmap(
				pxW, pxH, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul);
			using (SkiaSharp.SKCanvas canvas = new SkiaSharp.SKCanvas(bitmap))
			{
				canvas.Clear(SkiaSharp.SKColors.White);
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
			using (SkiaSharp.SKImage image = SkiaSharp.SKImage.FromBitmap(bitmap))
			using (SkiaSharp.SKData data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 95))
			{
				data.SaveTo(pngStream);
			}

			return pngStream.ToArray();
		}

		private static string EnsureSvgXmlns(string svg)
		{
			if (svg.IndexOf("xmlns=", StringComparison.Ordinal) >= 0)
			{
				return svg;
			}

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

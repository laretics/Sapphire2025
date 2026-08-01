using Diamond.Controls.Rendering;

namespace Diamond.Tests.Controls
{
	public class MeshPrintColorTests
	{
		[Fact]
		public void BrightScreenColor_BecomesDarkPaperInk()
		{
			// Cian brillante de UI oscura → tinta oscura del mismo matiz.
			string paper = MeshPrintColor.ToPaperInk("#38bdf8");
			Assert.True(MeshPrintColor.TryParseHex(paper, out byte r, out byte g, out byte b));
			double y = RelativeLuminance(r, g, b);
			Assert.True(y < 0.35, "debe ser tinta relativamente oscura, y=" + y);
		}

		[Fact]
		public void DarkScreenColor_BecomesLighterPaperInk()
		{
			string paper = MeshPrintColor.ToPaperInk("#1e293b");
			Assert.True(MeshPrintColor.TryParseHex(paper, out byte r, out byte g, out byte b));
			double y = RelativeLuminance(r, g, b);
			Assert.True(y > 0.12, "no debe ser negro pleno, y=" + y);
			// Más claro que el original oscuro.
			double y0 = RelativeLuminance(0x1e, 0x29, 0x3b);
			Assert.True(y > y0, "oscuro en pantalla → más claro en papel");
		}

		[Fact]
		public void Palette_MapTrainColor_Paper_UsesTransform()
		{
			string mapped = MeshSvgPalette.Paper.MapTrainColor("#fbbf24");
			Assert.StartsWith("#", mapped);
			Assert.NotEqual("#fbbf24", mapped, StringComparer.OrdinalIgnoreCase);
		}

		private static double RelativeLuminance(byte r, byte g, byte b)
		{
			double R = Srgb(r / 255.0);
			double G = Srgb(g / 255.0);
			double B = Srgb(b / 255.0);
			return 0.2126 * R + 0.7152 * G + 0.0722 * B;
		}

		private static double Srgb(double c)
		{
			return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
		}
	}
}

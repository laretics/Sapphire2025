using Diamond.Controls.Rendering;

namespace Diamond.Tests.Controls
{
	public class CirculationDocumentBrandingTests
	{
		[Fact]
		public void DefaultPath_IsSfmImg()
		{
			Assert.Equal("img/sfmImg.png", CirculationDocumentBranding.DefaultRelativePath);
		}

		[Fact]
		public void Configure_OverridesPath_AndResetsToDefault()
		{
			string previous = CirculationDocumentBranding.LogoPath;
			try
			{
				CirculationDocumentBranding.Configure("img/otro.png");
				Assert.Equal("img/otro.png", CirculationDocumentBranding.LogoPath);
				Assert.Contains("img/otro.png", CirculationDocumentBranding.ImageHref, StringComparison.Ordinal);
				CirculationDocumentBranding.Configure(null);
				Assert.Equal(CirculationDocumentBranding.DefaultRelativePath, CirculationDocumentBranding.LogoPath);
			}
			finally
			{
				CirculationDocumentBranding.Configure(previous);
				CirculationDocumentBranding.SetDataUri(null);
			}
		}
	}
}

using Sapphire2025Models.Diamond;

namespace Diamond.Tests.Controls
{
	public class CirculationSealTextTests
	{
		[Fact]
		public void Normalize_AcceptsPrintedSealQrAndHex()
		{
			const string hex = "a1b2c3d4e5f6";
			Assert.Equal(hex, CirculationSealText.Normalize("SEL a1b2c3d4e5f6"));
			Assert.Equal(hex, CirculationSealText.Normalize("sel A1B2C3D4E5F6"));
			Assert.Equal(hex, CirculationSealText.Normalize("ZAFSEL:v1:a1b2c3d4e5f6"));
			Assert.Equal(hex, CirculationSealText.Normalize("zafsel:v1:A1B2C3D4E5F6:legacy-payload"));
			Assert.Equal(hex, CirculationSealText.Normalize("A1B2 C3D4-E5F6"));
		}

		[Fact]
		public void Normalize_Empty_ReturnsEmpty()
		{
			Assert.Equal(string.Empty, CirculationSealText.Normalize(null));
			Assert.Equal(string.Empty, CirculationSealText.Normalize("   "));
		}
	}
}

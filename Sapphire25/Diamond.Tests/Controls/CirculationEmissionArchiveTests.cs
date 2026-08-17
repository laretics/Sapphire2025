using Diamond.Controls.Rendering;

namespace Diamond.Tests.Controls
{
	public class CirculationEmissionArchiveTests
	{
		[Fact]
		public void PackUnpack_RoundtripsPages()
		{
			List<string> pages = new List<string>
			{
				"<svg>hoja1</svg>",
				"<svg>hoja2 con ñ y &amp;</svg>"
			};
			string packed = CirculationEmissionArchive.Pack(pages);
			Assert.False(string.IsNullOrEmpty(packed));
			IReadOnlyList<string> back = CirculationEmissionArchive.Unpack(packed);
			Assert.Equal(2, back.Count);
			Assert.Equal(pages[0], back[0]);
			Assert.Equal(pages[1], back[1]);
		}

		[Fact]
		public void Unpack_EmptyOrGarbage_ReturnsEmpty()
		{
			Assert.Empty(CirculationEmissionArchive.Unpack(null));
			Assert.Empty(CirculationEmissionArchive.Unpack(""));
			Assert.Empty(CirculationEmissionArchive.Unpack("%%%no-es-base64%%%"));
		}
	}
}

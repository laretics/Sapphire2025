using Sapphire2025Models.Diamond;

namespace Diamond.Tests
{
	public sealed class PlacesXmlValidatorTests
	{
		[Fact]
		public void Empty_document_is_rejected()
		{
			IReadOnlyList<PlacesXmlIssue> issues = PlacesXmlValidator.Validate("   ");
			Assert.Contains(issues, i => i.Message.Contains("vacío", StringComparison.OrdinalIgnoreCase));
		}

		[Fact]
		public void Malformed_xml_reports_line()
		{
			IReadOnlyList<PlacesXmlIssue> issues = PlacesXmlValidator.Validate(
				"<places>\n  <place id=\"a\">\n");
			Assert.NotEmpty(issues);
			Assert.Contains(issues, i => i.Message.Contains("mal formado", StringComparison.OrdinalIgnoreCase));
			Assert.True(issues[0].Line > 0);
		}

		[Fact]
		public void Collects_several_structural_errors()
		{
			const string xml = """
				<places>
				  <lugar id="x" />
				  <place>
				    <names canonical="A" />
				  </place>
				  <place id="dup" kind="tren">
				    <names canonical="B" internal="B" external="B" tft="B" tfta="B" />
				  </place>
				  <place id="dup">
				    <names canonical="C" internal="C" external="C" tft="C" tfta="C" led="C" />
				  </place>
				</places>
				""";

			IReadOnlyList<PlacesXmlIssue> issues = PlacesXmlValidator.Validate(xml);
			Assert.True(issues.Count >= 4);
			Assert.Contains(issues, i => i.Message.Contains("<lugar>", StringComparison.Ordinal));
			Assert.Contains(issues, i => i.Message.Contains("sin atributo id", StringComparison.Ordinal));
			Assert.Contains(issues, i => i.Message.Contains("kind", StringComparison.OrdinalIgnoreCase));
			Assert.Contains(issues, i => i.Message.Contains("dos lugares", StringComparison.Ordinal));
			Assert.Contains(issues, i => i.Message.Contains("led", StringComparison.Ordinal));
			Assert.Contains(issues, i => i.Message.Contains("internal", StringComparison.Ordinal));
		}

		[Fact]
		public void Valid_minimal_catalog_passes()
		{
			const string xml = """
				<places>
				  <place id="palma" kind="rail">
				    <keys avr="PMI" sfm="1">
				      <diamond id="01" />
				    </keys>
				    <names canonical="Palma" internal="Palma" external="Palma" tft="Palma" tfta="Palma" />
				    <announce>
				      <clip role="enum" file="catalog/announce/palma.enum.wav" />
				      <clip role="final" file="catalog/announce/palma.final.wav" />
				    </announce>
				  </place>
				  <messages>
				    <message id="delay" icon="Warning" importance="190" comment="Retraso">
				      <title ca="Retard" es="Retraso" en="Delay" />
				      <text ca="Circulam amb retard." es="Circulamos con retraso." en="We are running late." />
				    </message>
				  </messages>
				</places>
				""";

			Assert.Empty(PlacesXmlValidator.Validate(xml));
		}

		[Fact]
		public void Current_places_xml_passes()
		{
			string path = FindPlacesXml();
			string xml = File.ReadAllText(path);
			IReadOnlyList<PlacesXmlIssue> issues = PlacesXmlValidator.Validate(xml);
			Assert.True(
				issues.Count == 0,
				path + ":\n" + string.Join("\n", issues.Select(i => "L" + i.Line + " " + i.Message)));
		}

		private static string FindPlacesXml()
		{
			string dir = AppContext.BaseDirectory;
			int i = 0;
			while (i < 12)
			{
				string[] candidates =
				[
					Path.Combine(dir, "App_Data", "catalog", "places.xml"),
					Path.Combine(dir, "wwwroot", "catalog", "places.xml"),
					Path.Combine(dir, "Sapphire2025Server", "App_Data", "catalog", "places.xml"),
					Path.Combine(dir, "Tourmaline26", "wwwroot", "catalog", "places.xml"),
					Path.Combine(dir, "Sapphire25", "Tourmaline26", "wwwroot", "catalog", "places.xml")
				];
				foreach (string candidate in candidates)
				{
					if (File.Exists(candidate))
						return candidate;
				}

				string? parent = Directory.GetParent(dir)?.FullName;
				if (parent is null || string.Equals(parent, dir, StringComparison.OrdinalIgnoreCase))
					break;
				dir = parent;
				i++;
			}

			throw new FileNotFoundException("No se encontró places.xml para el test de formato.");
		}
	}
}

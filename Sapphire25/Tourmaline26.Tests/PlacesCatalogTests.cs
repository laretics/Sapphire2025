using System.Xml.Linq;

namespace Tourmaline26.Tests;

public sealed class PlacesCatalogTests
{
	[Fact]
	public void PlacesXml_uses_internal_external_tft_and_tfta()
	{
		XDocument doc = XDocument.Load(FindPlacesXml());
		List<XElement> places = doc.Root!.Elements("place").ToList();
		Assert.True(places.Count >= 30);

		foreach (XElement place in places)
		{
			string id = (string?)place.Attribute("id") ?? "?";
			XElement? names = place.Element("names");
			Assert.NotNull(names);
			Assert.False(string.IsNullOrWhiteSpace((string?)names!.Attribute("internal")), id);
			Assert.False(string.IsNullOrWhiteSpace((string?)names.Attribute("external")), id);
			Assert.False(string.IsNullOrWhiteSpace((string?)names.Attribute("tft")), id);
			Assert.False(string.IsNullOrWhiteSpace((string?)names.Attribute("tfta")), id);
			Assert.Null(names.Attribute("led"));
			Assert.Null(names.Attribute("teleindicator"));
		}
	}

	[Fact]
	public void PlacesXml_has_prerecorded_messages()
	{
		XDocument doc = XDocument.Load(FindPlacesXml());
		List<XElement> messages = doc.Root!.Elements("messages").Elements("message").ToList();
		Assert.True(messages.Count >= 5);
		foreach (XElement message in messages)
		{
			string id = (string?)message.Attribute("id") ?? "?";
			Assert.False(string.IsNullOrWhiteSpace((string?)message.Attribute("comment")), id);
			XElement? title = message.Element("title");
			XElement? text = message.Element("text");
			Assert.NotNull(title);
			Assert.NotNull(text);
			Assert.False(string.IsNullOrWhiteSpace((string?)title!.Attribute("ca")), id);
			Assert.False(string.IsNullOrWhiteSpace((string?)text!.Attribute("ca")), id);
		}
	}

	[Fact]
	public void Palma_channels_keep_distinct_wording()
	{
		XElement palma = XDocument.Load(FindPlacesXml())
			.Root!.Elements("place")
			.First(p => (string?)p.Attribute("id") == "palma")
			.Element("names")!;

		Assert.Contains("intermodal", (string)palma.Attribute("internal")!, StringComparison.OrdinalIgnoreCase);
		Assert.Equal("Palma", (string?)palma.Attribute("external"));
		Assert.Equal("Palma Int.", (string?)palma.Attribute("tft"));
		Assert.Equal("Palma", (string?)palma.Attribute("tfta"));
	}

	private static string FindPlacesXml()
	{
		string dir = AppContext.BaseDirectory;
		int i = 0;
		while (i < 10)
		{
			string[] candidates =
			[
				Path.Combine(dir, "wwwroot", "catalog", "places.xml"),
				Path.Combine(dir, "Tourmaline26", "wwwroot", "catalog", "places.xml")
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

		throw new FileNotFoundException("No se encontró wwwroot/catalog/places.xml.");
	}
}

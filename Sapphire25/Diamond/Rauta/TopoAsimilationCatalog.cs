using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Diamond.Topo;

namespace Diamond.Rauta
{
	/// <summary>
	/// Catálogo de asimilaciones Onice leídas del XML de topografía (&lt;asimilation&gt;).
	/// </summary>
	public sealed class TopoAsimilationCatalog
	{
		private readonly Dictionary<string, TopoAsimilationTemplate> mcolById;

		public TopoAsimilationCatalog()
		{
			mcolById = new Dictionary<string, TopoAsimilationTemplate>(StringComparer.OrdinalIgnoreCase);
		}

		public IReadOnlyDictionary<string, TopoAsimilationTemplate> ById
		{
			get { return mcolById; }
		}

		public TopoAsimilationTemplate? Find(string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				return null;
			}

			TopoAsimilationTemplate? t;
			if (mcolById.TryGetValue(id, out t))
			{
				return t;
			}

			return null;
		}

		public static TopoAsimilationCatalog LoadFromTopoXml(string path)
		{
			using (FileStream stream = File.OpenRead(path))
			{
				return LoadFromTopoXml(stream);
			}
		}

		public static TopoAsimilationCatalog LoadFromTopoXml(Stream stream)
		{
			XDocument document = XDocument.Load(stream);
			TopoAsimilationCatalog catalog = new TopoAsimilationCatalog();
			XElement? root = document.Root;
			if (root is null)
			{
				return catalog;
			}

			XElement? asimRoot = root.Element("asimilation");
			if (asimRoot is null)
			{
				return catalog;
			}

			foreach (XElement item in asimRoot.Elements("item"))
			{
				TopoAsimilationTemplate template = ReadItem(item);
				if (template.Id.Length > 0)
				{
					catalog.mcolById[template.Id] = template;
				}
			}

			return catalog;
		}

		private static TopoAsimilationTemplate ReadItem(XElement item)
		{
			TopoAsimilationTemplate t = new TopoAsimilationTemplate();
			t.Id = Attr(item, "id");
			t.Name = Attr(item, "name");
			t.OriginName = Attr(item, "originName");
			t.OriginCode = Attr(item, "origin");
			t.Comment = Attr(item, "comment");
			t.Color = Attr(item, "color");

			foreach (XElement trip in item.Elements("trip"))
			{
				TopoAsimilationTrip step = new TopoAsimilationTrip();
				step.StationName = Attr(trip, "stn");
				step.DestCode = Attr(trip, "dest");
				step.RunTime = ParseDuration(Attr(trip, "time"));
				step.Dwell = ParseDuration(Attr(trip, "stop"));
				t.Trips.Add(step);
			}

			if (t.Trips.Count > 0)
			{
				t.DestinationName = t.Name;
				t.DestinationCode = t.Trips[t.Trips.Count - 1].DestCode;
			}

			return t;
		}

		private static TimeSpan ParseDuration(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return TimeSpan.Zero;
			}

			TimeSpan ts;
			if (TimeSpan.TryParseExact(text, new[] { @"hh\:mm\:ss", @"h\:mm\:ss" }, CultureInfo.InvariantCulture, out ts))
			{
				return ts;
			}

			if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out ts))
			{
				return ts;
			}

			return TimeSpan.Zero;
		}

		private static string Attr(XElement el, string name)
		{
			XAttribute? a = el.Attribute(name);
			return a is null ? string.Empty : a.Value;
		}
	}

	public sealed class TopoAsimilationTemplate
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public string OriginName { get; set; } = string.Empty;
		public string OriginCode { get; set; } = string.Empty;
		public string DestinationName { get; set; } = string.Empty;
		public string DestinationCode { get; set; } = string.Empty;
		public string Comment { get; set; } = string.Empty;
		public string Color { get; set; } = string.Empty;
		public List<TopoAsimilationTrip> Trips { get; } = new List<TopoAsimilationTrip>();
	}

	public sealed class TopoAsimilationTrip
	{
		public string StationName { get; set; } = string.Empty;
		public string DestCode { get; set; } = string.Empty;
		public TimeSpan RunTime { get; set; }
		public TimeSpan Dwell { get; set; }
	}
}

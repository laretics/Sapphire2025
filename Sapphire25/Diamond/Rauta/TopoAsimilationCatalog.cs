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
		private string mvarId = string.Empty;
		private string mvarName = string.Empty;
		private string mvarOriginName = string.Empty;
		private string mvarOriginCode = string.Empty;
		private string mvarDestinationName = string.Empty;
		private string mvarDestinationCode = string.Empty;
		private string mvarComment = string.Empty;
		private string mvarColor = string.Empty;
		private readonly List<TopoAsimilationTrip> mcolTrips = new List<TopoAsimilationTrip>();

		public string Id
		{
			get { return mvarId; }
			set { mvarId = value ?? string.Empty; }
		}

		public string Name
		{
			get { return mvarName; }
			set { mvarName = value ?? string.Empty; }
		}

		public string OriginName
		{
			get { return mvarOriginName; }
			set { mvarOriginName = value ?? string.Empty; }
		}

		public string OriginCode
		{
			get { return mvarOriginCode; }
			set { mvarOriginCode = value ?? string.Empty; }
		}

		public string DestinationName
		{
			get { return mvarDestinationName; }
			set { mvarDestinationName = value ?? string.Empty; }
		}

		public string DestinationCode
		{
			get { return mvarDestinationCode; }
			set { mvarDestinationCode = value ?? string.Empty; }
		}

		public string Comment
		{
			get { return mvarComment; }
			set { mvarComment = value ?? string.Empty; }
		}

		public string Color
		{
			get { return mvarColor; }
			set { mvarColor = value ?? string.Empty; }
		}

		public List<TopoAsimilationTrip> Trips
		{
			get { return mcolTrips; }
		}
	}

	public sealed class TopoAsimilationTrip
	{
		private string mvarStationName = string.Empty;
		private string mvarDestCode = string.Empty;
		private TimeSpan mvarRunTime;
		private TimeSpan mvarDwell;

		public string StationName
		{
			get { return mvarStationName; }
			set { mvarStationName = value ?? string.Empty; }
		}

		public string DestCode
		{
			get { return mvarDestCode; }
			set { mvarDestCode = value ?? string.Empty; }
		}

		public TimeSpan RunTime
		{
			get { return mvarRunTime; }
			set { mvarRunTime = value; }
		}

		public TimeSpan Dwell
		{
			get { return mvarDwell; }
			set { mvarDwell = value; }
		}
	}
}

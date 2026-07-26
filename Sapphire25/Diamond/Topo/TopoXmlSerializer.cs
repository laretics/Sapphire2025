using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Diamond.Basis;

namespace Diamond.Topo
{
	/// <summary>
	/// Serialización topográfica en formato canónico o legacy (Onice).
	/// Convención: atributo x = latitud, y = longitud.
	/// No serializa limit, signal ni asimilation.
	/// </summary>
	public static class TopoXmlSerializer
	{
		public static TopoLayout Load(string path)
		{
			if (path is null)
			{
				throw new ArgumentNullException(nameof(path));
			}

			using (FileStream stream = File.OpenRead(path))
			{
				return Load(stream);
			}
		}

		public static TopoLayout Load(Stream stream)
		{
			if (stream is null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			XDocument document = XDocument.Load(stream, LoadOptions.None);
			return ParseDocument(document);
		}

		public static void Save(TopoLayout layout, string path, TopoXmlFormat format)
		{
			if (layout is null)
			{
				throw new ArgumentNullException(nameof(layout));
			}

			if (path is null)
			{
				throw new ArgumentNullException(nameof(path));
			}

			using (FileStream stream = File.Create(path))
			{
				Save(layout, stream, format);
			}
		}

		public static void Save(TopoLayout layout, Stream stream, TopoXmlFormat format)
		{
			if (layout is null)
			{
				throw new ArgumentNullException(nameof(layout));
			}

			if (stream is null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			XDocument document;
			if (format == TopoXmlFormat.Canonical)
			{
				document = BuildCanonicalDocument(layout);
			}
			else if (format == TopoXmlFormat.Legacy)
			{
				document = BuildLegacyDocument(layout);
			}
			else
			{
				throw new ArgumentOutOfRangeException(nameof(format));
			}

			XmlWriterSettings settings = new XmlWriterSettings
			{
				Indent = true,
				IndentChars = "  ",
				Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
				OmitXmlDeclaration = false
			};

			using (XmlWriter writer = XmlWriter.Create(stream, settings))
			{
				document.Save(writer);
			}
		}

		private static TopoLayout ParseDocument(XDocument document)
		{
			XElement? root = document.Root;
			if (root is null || root.Name.LocalName != "layout")
			{
				throw new InvalidDataException("El XML de topografía debe tener raíz <layout>.");
			}

			TopoLayout layout = new TopoLayout();

			XElement? infoElement = root.Element("info");
			if (infoElement is not null)
			{
				ReadInfo(infoElement, layout.Info);
			}

			bool isCanonical = root.Element("stations") is not null;
			if (isCanonical)
			{
				ReadStationsCatalog(root.Element("stations")!, layout);
			}

			XElement? topoElement = root.Element("topo");
			if (topoElement is not null)
			{
				foreach (XElement axisElement in topoElement.Elements("axis"))
				{
					Axis axis = ReadAxis(axisElement, layout, isCanonical);
					axis.Rebuild();
					layout.AddAxis(axis);
				}
			}

			return layout;
		}

		private static void ReadInfo(XElement infoElement, LayoutInfo info)
		{
			info.Name = GetAttr(infoElement, "name");
			info.Description = GetAttr(infoElement, "description");
			info.Comment = GetAttr(infoElement, "comment");
			info.License = GetAttr(infoElement, "license");
			info.Author = GetAttr(infoElement, "author");
			info.FirstDate = GetAttr(infoElement, "firstdate");
			info.LastDate = GetAttr(infoElement, "lastdate");
			info.Version = GetAttr(infoElement, "version");
			info.Bitmap = GetAttr(infoElement, "bitmap");
			info.Id = GetAttr(infoElement, "id");
		}

		private static void ReadStationsCatalog(XElement stationsElement, TopoLayout layout)
		{
			foreach (XElement stationElement in stationsElement.Elements("station"))
			{
				string id = GetAttr(stationElement, "id");
				if (id.Length == 0)
				{
					throw new InvalidDataException("Una estación del catálogo no tiene atributo id.");
				}

				Station station = layout.GetOrAddStation(id);
				station.Name = GetAttr(stationElement, "name");
				station.Avr = GetAttr(stationElement, "avr");

				string xText = GetAttr(stationElement, "x");
				string yText = GetAttr(stationElement, "y");
				if (xText.Length > 0 && yText.Length > 0)
				{
					station.Latitude = ParseDouble(xText, "x");
					station.Longitude = ParseDouble(yText, "y");
				}
			}
		}

		private static Axis ReadAxis(XElement axisElement, TopoLayout layout, bool isCanonical)
		{
			Axis axis = new Axis();
			axis.Id = GetAttr(axisElement, "id");
			axis.Name = GetAttr(axisElement, "name");
			axis.Comment = GetAttr(axisElement, "comment");
			axis.Color = GetAttr(axisElement, "color");
			axis.DarkColor = GetAttr(axisElement, "darkcolor");

			string vmaxText = GetAttr(axisElement, "vmax");
			if (vmaxText.Length > 0)
			{
				int vmax;
				if (int.TryParse(vmaxText, NumberStyles.Integer, CultureInfo.InvariantCulture, out vmax))
				{
					axis.Vmax = vmax;
				}
			}

			XElement? polyElement = axisElement.Element("poly");
			if (polyElement is not null)
			{
				foreach (XElement pointElement in polyElement.Elements("point"))
				{
					axis.AddVertex(ReadPoint(pointElement, layout, isCanonical));
				}
			}

			XElement? limitElement = axisElement.Element("limit");
			if (limitElement is not null)
			{
				ReadLimits(limitElement, axis.FixedLimits);
			}

			return axis;
		}

		private static void ReadLimits(XElement limitElement, SpeedLimitMap map)
		{
			foreach (XElement itemElement in limitElement.Elements("item"))
			{
				string pk0Text = GetAttr(itemElement, "pk0");
				string pkfText = GetAttr(itemElement, "pkf");
				string speedText = GetAttr(itemElement, "speed");
				if (pk0Text.Length == 0 || pkfText.Length == 0 || speedText.Length == 0)
				{
					continue;
				}

				long pk0 = ParseLong(pk0Text, "pk0");
				long pkf = ParseLong(pkfText, "pkf");
				int speed = (int)ParseLong(speedText, "speed");
				map.Add(speed, pk0, pkf);
			}
		}

		private static AxisVertex ReadPoint(XElement pointElement, TopoLayout layout, bool isCanonical)
		{
			double latitude = ParseDoubleRequired(pointElement, "x");
			double longitude = ParseDoubleRequired(pointElement, "y");

			string pkText = GetAttr(pointElement, "pk");
			AxisVertex vertex;
			if (pkText.Length > 0)
			{
				long pk = ParseLong(pkText, "pk");
				vertex = new AxisVertex(latitude, longitude, pk);
			}
			else
			{
				vertex = new AxisVertex(latitude, longitude);
			}

			if (isCanonical)
			{
				string stationRef = GetAttr(pointElement, "station");
				if (stationRef.Length > 0)
				{
					Station station = layout.GetOrAddStation(stationRef);
					vertex.Station = station;
					if (!vertex.IsAnchor)
					{
						// Referencia a estación sin pk: no es ancla de calibración, solo marca.
						// En formato canónico las paradas deberían llevar pk; si falta, no forzamos ancla.
					}

					if (!station.Latitude.HasValue)
					{
						station.Latitude = latitude;
						station.Longitude = longitude;
					}
				}
			}
			else
			{
				// Legacy: id/name/avr embebidos → catálogo por id.
				string stationId = GetAttr(pointElement, "id");
				if (stationId.Length > 0)
				{
					Station station = layout.GetOrAddStation(stationId);
					string name = GetAttr(pointElement, "name");
					if (name.Length > 0)
					{
						station.Name = name;
					}

					string avr = GetAttr(pointElement, "avr");
					if (avr.Length > 0)
					{
						station.Avr = avr;
					}

					if (!station.Latitude.HasValue)
					{
						station.Latitude = latitude;
						station.Longitude = longitude;
					}

					vertex.Station = station;
				}
			}

			return vertex;
		}

		private static XDocument BuildCanonicalDocument(TopoLayout layout)
		{
			EnsureStationsHaveIds(layout);

			XElement infoElement = BuildInfoElement(layout.Info);
			XElement stationsElement = new XElement("stations");

			int stationIndex = 0;
			while (stationIndex < layout.Stations.Count)
			{
				Station station = layout.Stations[stationIndex];
				XElement stationElement = new XElement(
					"station",
					new XAttribute("id", station.Id),
					new XAttribute("name", station.Name),
					new XAttribute("avr", station.Avr));

				if (station.Latitude.HasValue && station.Longitude.HasValue)
				{
					stationElement.Add(new XAttribute("x", FormatDouble(station.Latitude.Value)));
					stationElement.Add(new XAttribute("y", FormatDouble(station.Longitude.Value)));
				}

				stationsElement.Add(stationElement);
				stationIndex++;
			}

			XElement topoElement = new XElement("topo");
			int axisIndex = 0;
			while (axisIndex < layout.Axes.Count)
			{
				topoElement.Add(BuildAxisElement(layout.Axes[axisIndex], TopoXmlFormat.Canonical));
				axisIndex++;
			}

			XElement root = new XElement("layout", infoElement, stationsElement, topoElement);
			return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
		}

		private static XDocument BuildLegacyDocument(TopoLayout layout)
		{
			EnsureStationsHaveIds(layout);

			XElement infoElement = BuildInfoElement(layout.Info);
			XElement topoElement = new XElement("topo");

			int axisIndex = 0;
			while (axisIndex < layout.Axes.Count)
			{
				topoElement.Add(BuildAxisElement(layout.Axes[axisIndex], TopoXmlFormat.Legacy));
				axisIndex++;
			}

			XElement root = new XElement("layout", infoElement, topoElement);
			return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
		}

		private static XElement BuildInfoElement(LayoutInfo info)
		{
			return new XElement(
				"info",
				new XAttribute("name", info.Name),
				new XAttribute("description", info.Description),
				new XAttribute("comment", info.Comment),
				new XAttribute("license", info.License),
				new XAttribute("author", info.Author),
				new XAttribute("firstdate", info.FirstDate),
				new XAttribute("lastdate", info.LastDate),
				new XAttribute("version", info.Version),
				new XAttribute("bitmap", info.Bitmap),
				new XAttribute("id", info.Id));
		}

		private static XElement BuildAxisElement(Axis axis, TopoXmlFormat format)
		{
			XElement axisElement = new XElement(
				"axis",
				new XAttribute("id", axis.Id),
				new XAttribute("name", axis.Name),
				new XAttribute("comment", axis.Comment),
				new XAttribute("vmax", axis.Vmax.ToString(CultureInfo.InvariantCulture)),
				new XAttribute("color", axis.Color),
				new XAttribute("darkcolor", axis.DarkColor));

			XElement polyElement = new XElement("poly");
			int vertexIndex = 0;
			while (vertexIndex < axis.Vertices.Count)
			{
				polyElement.Add(BuildPointElement(axis.Vertices[vertexIndex], format));
				vertexIndex++;
			}

			axisElement.Add(polyElement);
			axisElement.Add(BuildLimitElement(axis.FixedLimits));
			return axisElement;
		}

		private static XElement BuildLimitElement(SpeedLimitMap map)
		{
			XElement limitElement = new XElement("limit");

			foreach (KeyValuePair<int, AxisVectorFlex> pair in map.BySpeed)
			{
				int speed = pair.Key;
				AxisVectorFlex flex = pair.Value;
				int linealIndex = 0;
				while (linealIndex < flex.Lineals.Count)
				{
					Lineal<long, LongAxis> segment = flex.Lineals[linealIndex];
					long pk0 = segment.PK;
					long pkf = segment.PKEnd;
					// Tras Normalize, Length ≥ 0; PKEnd = pk0 + length (extremo exclusivo del modelo).
					limitElement.Add(
						new XElement(
							"item",
							new XAttribute("pk0", pk0.ToString(CultureInfo.InvariantCulture)),
							new XAttribute("pkf", pkf.ToString(CultureInfo.InvariantCulture)),
							new XAttribute("speed", speed.ToString(CultureInfo.InvariantCulture)),
							new XAttribute("par", "0"),
							new XAttribute("comment", string.Empty)));
					linealIndex++;
				}
			}

			return limitElement;
		}

		private static XElement BuildPointElement(AxisVertex vertex, TopoXmlFormat format)
		{
			XElement pointElement = new XElement(
				"point",
				new XAttribute("x", FormatDouble(vertex.Latitude)),
				new XAttribute("y", FormatDouble(vertex.Longitude)));

			if (format == TopoXmlFormat.Canonical)
			{
				if (vertex.IsAnchor && vertex.AnchorPk.HasValue)
				{
					pointElement.Add(new XAttribute("pk", vertex.AnchorPk.Value.ToString(CultureInfo.InvariantCulture)));
				}

				if (vertex.Station is not null && vertex.Station.Id.Length > 0)
				{
					pointElement.Add(new XAttribute("station", vertex.Station.Id));
				}
			}
			else
			{
				// Legacy: duplicar identidad de estación en cada point.
				if (vertex.IsAnchor && vertex.AnchorPk.HasValue)
				{
					if (vertex.Station is not null)
					{
						if (vertex.Station.Name.Length > 0)
						{
							pointElement.Add(new XAttribute("name", vertex.Station.Name));
						}

						if (vertex.Station.Avr.Length > 0)
						{
							pointElement.Add(new XAttribute("avr", vertex.Station.Avr));
						}
					}

					pointElement.Add(new XAttribute("pk", vertex.AnchorPk.Value.ToString(CultureInfo.InvariantCulture)));

					if (vertex.Station is not null && vertex.Station.Id.Length > 0)
					{
						pointElement.Add(new XAttribute("id", vertex.Station.Id));
					}
				}
			}

			return pointElement;
		}

		/// <summary>
		/// Asigna ids sintéticos a estaciones que no lo tengan (p. ej. creadas solo en memoria).
		/// </summary>
		private static void EnsureStationsHaveIds(TopoLayout layout)
		{
			int generated = 0;
			int index = 0;
			while (index < layout.Stations.Count)
			{
				Station station = layout.Stations[index];
				if (station.Id.Length == 0)
				{
					string candidate;
					do
					{
						generated++;
						candidate = "gen-" + generated.ToString(CultureInfo.InvariantCulture);
					}
					while (layout.FindStationById(candidate) is not null);

					station.Id = candidate;
				}

				index++;
			}

			// También estaciones colgando solo de vértices (por si no están en el catálogo).
			int axisIndex = 0;
			while (axisIndex < layout.Axes.Count)
			{
				Axis axis = layout.Axes[axisIndex];
				int vertexIndex = 0;
				while (vertexIndex < axis.Vertices.Count)
				{
					Station? station = axis.Vertices[vertexIndex].Station;
					if (station is not null)
					{
						if (layout.FindStationById(station.Id) is null && station.Id.Length > 0)
						{
							layout.AddStation(station);
						}
						else if (station.Id.Length == 0)
						{
							string candidate;
							do
							{
								generated++;
								candidate = "gen-" + generated.ToString(CultureInfo.InvariantCulture);
							}
							while (layout.FindStationById(candidate) is not null);

							station.Id = candidate;
							if (layout.FindStationById(station.Id) is null)
							{
								layout.AddStation(station);
							}
						}
					}

					vertexIndex++;
				}

				axisIndex++;
			}
		}

		private static string GetAttr(XElement element, string name)
		{
			XAttribute? attribute = element.Attribute(name);
			if (attribute is null)
			{
				return string.Empty;
			}

			return attribute.Value;
		}

		private static double ParseDoubleRequired(XElement element, string attributeName)
		{
			string text = GetAttr(element, attributeName);
			if (text.Length == 0)
			{
				throw new InvalidDataException($"Falta el atributo '{attributeName}' en <{element.Name.LocalName}>.");
			}

			return ParseDouble(text, attributeName);
		}

		private static double ParseDouble(string text, string attributeName)
		{
			double value;
			if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
			{
				throw new InvalidDataException($"Valor numérico no válido en {attributeName}='{text}'.");
			}

			return value;
		}

		private static long ParseLong(string text, string attributeName)
		{
			long value;
			if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
			{
				throw new InvalidDataException($"Valor entero no válido en {attributeName}='{text}'.");
			}

			return value;
		}

		private static string FormatDouble(double value)
		{
			return value.ToString("G17", CultureInfo.InvariantCulture);
		}
	}
}

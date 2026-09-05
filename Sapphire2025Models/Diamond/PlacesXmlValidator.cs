using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Sapphire2025Models.Diamond
{
	/// <summary>
	/// Revisión de formato de places.xml (Tourmaline). Recoge todos los
	/// problemas que puede; el primer error de XML mal formado corta el resto.
	/// </summary>
	public static class PlacesXmlValidator
	{
		public const int MaxXmlBytes = 2 * 1024 * 1024;
		public const int MaxIssues = 80;

		private static readonly string[] PlaceKinds = ["rail", "bus", "technical"];
		private static readonly string[] CorrespondenceEntities = ["ctmr4", "emt"];
		private static readonly string[] ClipRoles = ["enum", "final"];
		private static readonly string[] IconModes = ["prefix", "replace", "none"];
		private static readonly string[] NameChannels = ["internal", "external", "tft", "tfta"];

		public static IReadOnlyList<PlacesXmlIssue> Validate(string? xml)
		{
			List<PlacesXmlIssue> issues = new List<PlacesXmlIssue>();
			if (string.IsNullOrWhiteSpace(xml))
			{
				issues.Add(Issue(0, 0, "El documento está vacío."));
				return issues;
			}

			int byteLength = Encoding.UTF8.GetByteCount(xml);
			if (byteLength > MaxXmlBytes)
			{
				issues.Add(Issue(
					0,
					0,
					string.Format(
						CultureInfo.InvariantCulture,
						"El documento supera el tamaño máximo ({0} bytes).",
						MaxXmlBytes)));
				return issues;
			}

			XDocument doc;
			try
			{
				doc = XDocument.Parse(xml, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
			}
			catch (XmlException ex)
			{
				issues.Add(Issue(
					ex.LineNumber,
					ex.LinePosition,
					"XML mal formado: " + ex.Message));
				return issues;
			}
			catch (Exception ex)
			{
				issues.Add(Issue(0, 0, "XML no válido: " + ex.Message));
				return issues;
			}

			XElement? root = doc.Root;
			if (root is null || !Is(root, "places"))
			{
				issues.Add(Issue(root, "La raíz del documento debe ser <places>."));
				return issues;
			}

			int placeCount = 0;
			Dictionary<string, XElement> placeIds = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, XElement> diamondIds = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, XElement> avrs = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
			Dictionary<int, XElement> sfmCodes = new Dictionary<int, XElement>();
			Dictionary<string, XElement> tibNames = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, XElement> messageIds = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);

			foreach (XElement child in root.Elements())
			{
				if (!CanAdd(issues))
					break;

				if (Is(child, "place"))
				{
					placeCount++;
					ReviewPlace(child, issues, placeIds, diamondIds, avrs, sfmCodes, tibNames);
				}
				else if (Is(child, "messages"))
				{
					ReviewMessages(child, issues, messageIds);
				}
				else
				{
					issues.Add(Issue(
						child,
						"Elemento <" + child.Name.LocalName + "> no reconocido. En <places> solo se admiten <place> y <messages>."));
				}
			}

			if (placeCount == 0)
				issues.Add(Issue(root, "El catálogo no contiene ningún <place>."));

			return issues;
		}

		private static void ReviewPlace(
			XElement place,
			List<PlacesXmlIssue> issues,
			Dictionary<string, XElement> placeIds,
			Dictionary<string, XElement> diamondIds,
			Dictionary<string, XElement> avrs,
			Dictionary<int, XElement> sfmCodes,
			Dictionary<string, XElement> tibNames)
		{
			string id = Attr(place, "id");
			if (id.Length == 0)
			{
				issues.Add(Issue(place, "Hay un <place> sin atributo id."));
				id = "(sin id)";
			}
			else if (placeIds.TryGetValue(id, out XElement? firstPlace))
			{
				issues.Add(Issue(
					place,
					"Hay dos lugares con id \"" + id + "\" (el primero está en la línea "
					+ LineOf(firstPlace).ToString(CultureInfo.InvariantCulture)
					+ "). El id debe ser único."));
			}
			else
				placeIds[id] = place;

			string kind = Attr(place, "kind");
			if (kind.Length > 0 && !Contains(PlaceKinds, kind))
			{
				issues.Add(Issue(
					place.Attribute("kind") ?? (XObject)place,
					"Lugar \"" + id + "\": kind \"" + kind + "\" no es válido. Use rail, bus o technical."));
			}

			bool hasNames = false;
			foreach (XElement child in place.Elements())
			{
				if (!CanAdd(issues))
					return;

				if (Is(child, "keys"))
					ReviewKeys(id, child, issues, diamondIds, avrs, sfmCodes, tibNames);
				else if (Is(child, "names"))
				{
					hasNames = true;
					ReviewNames(id, child, issues);
				}
				else if (Is(child, "announce"))
					ReviewAnnounce(id, child, issues);
				else if (Is(child, "correspondence"))
					ReviewCorrespondence(id, child, issues);
				else
				{
					issues.Add(Issue(
						child,
						"Lugar \"" + id + "\": elemento <" + child.Name.LocalName
						+ "> no reconocido. Se admiten <keys>, <names>, <announce> y <correspondence>."));
				}
			}

			if (!hasNames)
			{
				issues.Add(Issue(
					place,
					"Lugar \"" + id + "\": falta el elemento <names> (internal, external, tft y tfta)."));
			}
		}

		private static void ReviewKeys(
			string placeId,
			XElement keys,
			List<PlacesXmlIssue> issues,
			Dictionary<string, XElement> diamondIds,
			Dictionary<string, XElement> avrs,
			Dictionary<int, XElement> sfmCodes,
			Dictionary<string, XElement> tibNames)
		{
			string avr = Attr(keys, "avr");
			if (avr.Length > 0)
			{
				if (avrs.TryGetValue(avr, out XElement? first))
				{
					issues.Add(Issue(
						keys.Attribute("avr") ?? (XObject)keys,
						"Lugar \"" + placeId + "\": AVR \"" + avr + "\" ya está en la línea "
						+ LineOf(first).ToString(CultureInfo.InvariantCulture) + "."));
				}
				else
					avrs[avr] = keys;
			}

			string sfmRaw = Attr(keys, "sfm");
			if (sfmRaw.Length > 0)
			{
				if (!int.TryParse(sfmRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sfm))
				{
					issues.Add(Issue(
						keys.Attribute("sfm") ?? (XObject)keys,
						"Lugar \"" + placeId + "\": keys/@sfm \"" + sfmRaw + "\" no es un entero."));
				}
				else if (sfmCodes.TryGetValue(sfm, out XElement? firstSfm))
				{
					issues.Add(Issue(
						keys.Attribute("sfm") ?? (XObject)keys,
						"Lugar \"" + placeId + "\": código SFM " + sfmRaw + " ya está en la línea "
						+ LineOf(firstSfm).ToString(CultureInfo.InvariantCulture) + "."));
				}
				else
					sfmCodes[sfm] = keys;
			}

			string tibName = Attr(keys, "tibName");
			if (tibName.Length > 0)
			{
				if (tibNames.TryGetValue(tibName, out XElement? firstTib))
				{
					issues.Add(Issue(
						keys.Attribute("tibName") ?? (XObject)keys,
						"Lugar \"" + placeId + "\": tibName \"" + tibName + "\" ya está en la línea "
						+ LineOf(firstTib).ToString(CultureInfo.InvariantCulture) + "."));
				}
				else
					tibNames[tibName] = keys;
			}

			foreach (XElement child in keys.Elements())
			{
				if (!CanAdd(issues))
					return;

				if (!Is(child, "diamond"))
				{
					issues.Add(Issue(
						child,
						"Lugar \"" + placeId + "\": en <keys> solo se admite <diamond>. Encontrado <"
						+ child.Name.LocalName + ">."));
					continue;
				}

				string diamondId = Attr(child, "id");
				if (diamondId.Length == 0)
				{
					issues.Add(Issue(child, "Lugar \"" + placeId + "\": hay un <diamond> sin atributo id."));
					continue;
				}

				if (diamondIds.TryGetValue(diamondId, out XElement? firstDiamond))
				{
					issues.Add(Issue(
						child,
						"Lugar \"" + placeId + "\": diamond id \"" + diamondId + "\" ya está en la línea "
						+ LineOf(firstDiamond).ToString(CultureInfo.InvariantCulture) + "."));
				}
				else
					diamondIds[diamondId] = child;
			}
		}

		private static void ReviewNames(string placeId, XElement names, List<PlacesXmlIssue> issues)
		{
			if (names.Attribute("led") is not null)
			{
				issues.Add(Issue(
					names.Attribute("led")!,
					"Lugar \"" + placeId + "\": names/@led está obsoleto. Use names/@internal."));
			}

			if (names.Attribute("teleindicator") is not null)
			{
				issues.Add(Issue(
					names.Attribute("teleindicator")!,
					"Lugar \"" + placeId + "\": names/@teleindicator está obsoleto. Use names/@external."));
			}

			foreach (string channel in NameChannels)
			{
				if (string.IsNullOrWhiteSpace((string?)names.Attribute(channel)))
				{
					issues.Add(Issue(
						names,
						"Lugar \"" + placeId + "\": falta names/@" + channel + "."));
				}
			}

			string iconMode = Attr(names, "iconMode");
			if (iconMode.Length > 0 && !Contains(IconModes, iconMode))
			{
				issues.Add(Issue(
					names.Attribute("iconMode") ?? (XObject)names,
					"Lugar \"" + placeId + "\": iconMode \"" + iconMode
					+ "\" no es válido. Use prefix, replace o none."));
			}

			foreach (XElement child in names.Elements())
			{
				if (!CanAdd(issues))
					return;
				issues.Add(Issue(
					child,
					"Lugar \"" + placeId + "\": <names> no admite elementos hijos. Use atributos (canonical, internal, external, tft, tfta)."));
			}
		}

		private static void ReviewAnnounce(string placeId, XElement announce, List<PlacesXmlIssue> issues)
		{
			foreach (XElement child in announce.Elements())
			{
				if (!CanAdd(issues))
					return;

				if (!Is(child, "clip"))
				{
					issues.Add(Issue(
						child,
						"Lugar \"" + placeId + "\": en <announce> solo se admite <clip>. Encontrado <"
						+ child.Name.LocalName + ">."));
					continue;
				}

				string role = Attr(child, "role");
				if (role.Length == 0)
					issues.Add(Issue(child, "Lugar \"" + placeId + "\": un <clip> no tiene atributo role (enum o final)."));
				else if (!Contains(ClipRoles, role))
				{
					issues.Add(Issue(
						child.Attribute("role") ?? (XObject)child,
						"Lugar \"" + placeId + "\": clip/@role \"" + role + "\" no es válido. Use enum o final."));
				}

				if (Attr(child, "file").Length == 0)
					issues.Add(Issue(child, "Lugar \"" + placeId + "\": un <clip> no tiene atributo file."));
			}
		}

		private static void ReviewCorrespondence(string placeId, XElement correspondence, List<PlacesXmlIssue> issues)
		{
			string entity = Attr(correspondence, "entity");
			if (entity.Length > 0 && !Contains(CorrespondenceEntities, entity))
			{
				issues.Add(Issue(
					correspondence.Attribute("entity") ?? (XObject)correspondence,
					"Lugar \"" + placeId + "\": correspondence/@entity \"" + entity
					+ "\" no es válido. Use ctmr4 (TIB) o emt."));
			}

			foreach (XElement child in correspondence.Elements())
			{
				if (!CanAdd(issues))
					return;

				if (!Is(child, "stop"))
				{
					issues.Add(Issue(
						child,
						"Lugar \"" + placeId + "\": en <correspondence> solo se admite <stop>. Encontrado <"
						+ child.Name.LocalName + ">."));
					continue;
				}

				if (Attr(child, "code").Length == 0)
					issues.Add(Issue(child, "Lugar \"" + placeId + "\": hay un <stop> sin atributo code."));

				string bayRaw = Attr(child, "bay");
				if (bayRaw.Length > 0 && !HasPositiveInt(bayRaw))
				{
					issues.Add(Issue(
						child.Attribute("bay") ?? (XObject)child,
						"Lugar \"" + placeId + "\": stop/@bay \"" + bayRaw + "\" no es un entero de dársena."));
				}

				foreach (XElement inner in child.Elements())
				{
					if (!CanAdd(issues))
						return;

					if (Is(inner, "line"))
					{
						if (Attr(inner, "code").Length == 0)
							issues.Add(Issue(inner, "Lugar \"" + placeId + "\": hay un <line> sin atributo code."));
					}
					else if (Is(inner, "dock"))
					{
						if (Attr(inner, "line").Length == 0)
							issues.Add(Issue(inner, "Lugar \"" + placeId + "\": hay un <dock> sin atributo line."));
						if (Attr(inner, "bay").Length == 0 || !HasPositiveInt(Attr(inner, "bay")))
							issues.Add(Issue(inner, "Lugar \"" + placeId + "\": hay un <dock> sin bay entero."));
					}
					else
					{
						issues.Add(Issue(
							inner,
							"Lugar \"" + placeId + "\": en <stop> solo se admiten <line> y <dock>. Encontrado <"
							+ inner.Name.LocalName + ">."));
					}
				}
			}
		}

		private static void ReviewMessages(
			XElement messages,
			List<PlacesXmlIssue> issues,
			Dictionary<string, XElement> messageIds)
		{
			foreach (XElement child in messages.Elements())
			{
				if (!CanAdd(issues))
					return;

				if (!Is(child, "message"))
				{
					issues.Add(Issue(
						child,
						"En <messages> solo se admite <message>. Encontrado <" + child.Name.LocalName + ">."));
					continue;
				}

				string id = Attr(child, "id");
				if (id.Length == 0)
				{
					issues.Add(Issue(child, "Hay un <message> sin atributo id."));
					id = "(sin id)";
				}
				else if (messageIds.TryGetValue(id, out XElement? first))
				{
					issues.Add(Issue(
						child,
						"Hay dos mensajes con id \"" + id + "\" (el primero está en la línea "
						+ LineOf(first).ToString(CultureInfo.InvariantCulture) + ")."));
				}
				else
					messageIds[id] = child;

				string comment = Attr(child, "comment");
				if (comment.Length == 0)
				{
					string nested = NestedText(child, "comment");
					if (nested.Length == 0)
					{
						issues.Add(Issue(
							child,
							"Mensaje \"" + id + "\": falta @comment (etiqueta de lista en cabina)."));
					}
				}

				string importanceRaw = Attr(child, "importance");
				if (importanceRaw.Length > 0
					&& !byte.TryParse(importanceRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
				{
					issues.Add(Issue(
						child.Attribute("importance") ?? (XObject)child,
						"Mensaje \"" + id + "\": importance \"" + importanceRaw + "\" debe ser un entero 0–255."));
				}

				bool hasTitle = HasLanguagePack(child, "title", "titles");
				bool hasText = HasLanguagePack(child, "text", "texts");
				if (!hasTitle && !hasText)
				{
					issues.Add(Issue(
						child,
						"Mensaje \"" + id + "\": hace falta <title> o <text> con ca, es o en."));
				}

				foreach (XElement inner in child.Elements())
				{
					if (Is(inner, "title") || Is(inner, "text") || Is(inner, "comment"))
						continue;
					issues.Add(Issue(
						inner,
						"Mensaje \"" + id + "\": elemento <" + inner.Name.LocalName
						+ "> no reconocido. Se admiten <title>, <text> y <comment>."));
				}
			}
		}

		private static bool HasLanguagePack(XElement parent, string childName, string packedAttribute)
		{
			if (!string.IsNullOrWhiteSpace((string?)parent.Attribute(packedAttribute)))
				return true;

			foreach (XElement child in parent.Elements())
			{
				if (!Is(child, childName))
					continue;
				if (!string.IsNullOrWhiteSpace((string?)child.Attribute("ca"))
					|| !string.IsNullOrWhiteSpace((string?)child.Attribute("es"))
					|| !string.IsNullOrWhiteSpace((string?)child.Attribute("en"))
					|| !string.IsNullOrWhiteSpace(child.Value))
				{
					return true;
				}
			}

			return false;
		}

		private static string NestedText(XElement parent, string childName)
		{
			foreach (XElement child in parent.Elements())
			{
				if (Is(child, childName))
					return child.Value.Trim();
			}

			return string.Empty;
		}

		private static bool HasPositiveInt(string raw)
		{
			foreach (string part in raw.Split(new[] { ',', '/', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
			{
				string digits = new string(part.Where(char.IsDigit).ToArray());
				if (int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0)
					return true;
			}

			return false;
		}

		private static bool CanAdd(List<PlacesXmlIssue> issues) => issues.Count < MaxIssues;

		private static bool Is(XElement el, string name) =>
			string.Equals(el.Name.LocalName, name, StringComparison.OrdinalIgnoreCase);

		private static string Attr(XElement el, string name) =>
			((string?)el.Attribute(name) ?? string.Empty).Trim();

		private static bool Contains(string[] allowed, string value)
		{
			foreach (string item in allowed)
			{
				if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase))
					return true;
			}

			return false;
		}

		private static PlacesXmlIssue Issue(int line, int column, string message) =>
			new PlacesXmlIssue
			{
				Line = line,
				Column = column,
				Message = message
			};

		private static PlacesXmlIssue Issue(XObject? node, string message)
		{
			(int line, int column) = LineCol(node);
			return Issue(line, column, message);
		}

		private static int LineOf(XObject node) => LineCol(node).line;

		private static (int line, int column) LineCol(XObject? node)
		{
			if (node is IXmlLineInfo info && info.HasLineInfo())
				return (info.LineNumber, info.LinePosition);
			return (0, 0);
		}
	}
}

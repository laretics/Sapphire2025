using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Diamond.Project;
using Microsoft.AspNetCore.Hosting;
using Tourmaline26.Logic;

namespace Tourmaline26.Services.Catalog
{
	/// <summary>
	/// Catálogo de lugares (estaciones SFM y destinos de bus) cargado desde
	/// <c>cache/catalog/places.xml</c> si existe, o el empaquetado en wwwroot.
	/// </summary>
	public sealed class PlacesCatalog
	{
		public const string RelativePath = "catalog/places.xml";
		public const string CacheRelativePath = "cache/catalog/places.xml";

		private readonly IWebHostEnvironment mvarEnvironment;
		private readonly ILogger<PlacesCatalog> mvarLogger;

		private IReadOnlyList<Place> mcolPlaces = Array.Empty<Place>();
		private Dictionary<string, Place> mcolById = new(StringComparer.OrdinalIgnoreCase);
		private Dictionary<string, Place> mcolByDiamond = new(StringComparer.OrdinalIgnoreCase);
		private Dictionary<string, Place> mcolByAvr = new(StringComparer.OrdinalIgnoreCase);
		private Dictionary<int, Place> mcolBySfm = new();
		private Dictionary<string, Place> mcolByTibName = new(StringComparer.OrdinalIgnoreCase);
		private string mvarLoadedPath = string.Empty;
		private string mvarContentHash = string.Empty;
		private IReadOnlyList<PassengerInformation> mcolAnnouncements = Array.Empty<PassengerInformation>();

		public PlacesCatalog(IWebHostEnvironment environment, ILogger<PlacesCatalog> logger)
		{
			mvarEnvironment = environment;
			mvarLogger = logger;
			ApplyEmpty();
			string path = ResolvePath();
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				logger.LogWarning("PlacesCatalog: no se encontró places.xml.");
				return;
			}

			LoadFromFile(path);
		}

		public IReadOnlyList<Place> Places => mcolPlaces;

		/// <summary>Anuncios pregrabados leídos de <c>messages/message</c>.</summary>
		public IReadOnlyList<PassengerInformation> Announcements => mcolAnnouncements;

		public string ContentHash => mvarContentHash;

		public string LoadedPath => mvarLoadedPath;

		public string CacheFilePath =>
			Path.Combine(
				mvarEnvironment.ContentRootPath ?? AppContext.BaseDirectory,
				CacheRelativePath);

		/// <summary>
		/// Sustituye el catálogo en memoria y en caché. False si el XML no es válido
		/// (se conserva el catálogo anterior).
		/// </summary>
		public bool TryReplaceWithXml(string xml, out string error)
		{
			error = string.Empty;
			if (string.IsNullOrWhiteSpace(xml))
			{
				error = "Documento vacío.";
				return false;
			}

			XDocument doc;
			try
			{
				doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
			}
			catch (Exception ex)
			{
				error = ex.Message;
				return false;
			}

			if (!TryBuild(doc, out List<Place> places, out Dictionaries maps, out List<PassengerInformation> announcements, out error))
				return false;

			string dest = CacheFilePath;
			string? dir = Path.GetDirectoryName(dest);
			if (!string.IsNullOrEmpty(dir))
				Directory.CreateDirectory(dir);

			byte[] bytes = Encoding.UTF8.GetBytes(xml);
			string temp = dest + ".tmp";
			File.WriteAllBytes(temp, bytes);
			if (File.Exists(dest))
			{
				File.Copy(temp, dest, overwrite: true);
				File.Delete(temp);
			}
			else
				File.Move(temp, dest);

			Assign(places, maps, announcements, dest, Sha256Hex(bytes));
			mvarLogger.LogInformation(
				"PlacesCatalog: actualizado desde Zafiro ({Count} lugares, {Messages} anuncios, hash {Hash}).",
				places.Count,
				announcements.Count,
				mvarContentHash.Length > 12 ? mvarContentHash.Substring(0, 12) : mvarContentHash);
			return true;
		}

		public Place? FindById(string? id) =>
			string.IsNullOrWhiteSpace(id) ? null : mcolById.GetValueOrDefault(id.Trim());

		public Place? FindByDiamondId(string? diamondId) =>
			string.IsNullOrWhiteSpace(diamondId) ? null : mcolByDiamond.GetValueOrDefault(diamondId.Trim());

		public Place? FindByAvr(string? avr) =>
			string.IsNullOrWhiteSpace(avr) ? null : mcolByAvr.GetValueOrDefault(avr.Trim());

		public Place? FindBySfmCode(int code) =>
			mcolBySfm.GetValueOrDefault(code);

		public Place? FindByTibName(string? name) =>
			string.IsNullOrWhiteSpace(name) ? null : mcolByTibName.GetValueOrDefault(name.Trim());

		/// <summary>Resuelve por id Diamond, AVR o nombre de panel.</summary>
		public Place? Find(StationInfo? station)
		{
			if (station is null)
				return null;
			return FindByDiamondId(station.Id)
				?? FindByAvr(station.Avr)
				?? FindByDisplayName(station.Name);
		}

		/// <summary>
		/// Nombre para el canal pedido (LED interior/exterior o TFT).
		/// Si el lugar no está en el catálogo, se devuelve
		/// <paramref name="fallback"/> o el nombre Diamond.
		/// </summary>
		public string NameFor(PlaceNameChannel channel, StationInfo? station, string? fallback = null)
		{
			string raw = station is not null && !string.IsNullOrWhiteSpace(station.Name)
				? station.Name.Trim()
				: (fallback ?? string.Empty).Trim();
			Place? place = Find(station);
			if (place is null && raw.Length > 0)
				place = FindByDisplayName(raw);
			string named = PickName(place, channel);
			if (named.Length > 0)
				return named;
			return raw;
		}

		/// <summary>Nombre para un canal a partir de una cadena ya mostrada.</summary>
		public string NameFor(PlaceNameChannel channel, string? raw)
		{
			string fallback = (raw ?? string.Empty).Trim();
			if (fallback.Length == 0)
				return string.Empty;
			string named = PickName(FindByDisplayName(fallback), channel);
			return named.Length > 0 ? named : fallback;
		}

		private static string PickName(Place? place, PlaceNameChannel channel)
		{
			if (place is null)
				return string.Empty;
			string named = channel switch
			{
				PlaceNameChannel.Internal => FirstNonEmpty(place.Names.Internal, place.Names.Canonical),
				PlaceNameChannel.External => FirstNonEmpty(place.Names.External, place.Names.Canonical),
				PlaceNameChannel.Tft => FirstNonEmpty(place.Names.Tft, place.Names.Canonical),
				PlaceNameChannel.Tfta => FirstNonEmpty(place.Names.Tfta, place.Names.Tft, place.Names.Canonical),
				_ => place.Names.Canonical
			};
			return (named ?? string.Empty).Trim();
		}

		private static string FirstNonEmpty(params string?[] values)
		{
			foreach (string? value in values)
			{
				if (!string.IsNullOrWhiteSpace(value))
					return value.Trim();
			}
			return string.Empty;
		}

		/// <summary>Resuelve un lugar por nombre de panel, AVR o id Diamond.</summary>
		public Place? FindByDisplayName(string? name)
		{
			if (string.IsNullOrWhiteSpace(name))
				return null;

			string trimmed = name.Trim();
			Place? byId = FindById(trimmed) ?? FindByAvr(trimmed) ?? FindByDiamondId(trimmed);
			if (byId is not null)
				return byId;

			string needle = PlaceNameText.Normalize(trimmed);
			if (needle.Length < 2)
				return null;

			Place? best = null;
			int bestScore = 0;
			foreach (Place place in mcolPlaces)
			{
				int score = ScoreName(needle, place.Names.Canonical);
				score = Math.Max(score, ScoreName(needle, place.Names.Tft));
				score = Math.Max(score, ScoreName(needle, place.Names.Tfta));
				score = Math.Max(score, ScoreName(needle, place.Names.Internal));
				score = Math.Max(score, ScoreName(needle, place.Names.External));
				score = Math.Max(score, ScoreName(needle, place.TibName));
				if (score > bestScore)
				{
					bestScore = score;
					best = place;
				}
			}

			return bestScore >= 95 ? best : null;
		}

		private static int ScoreName(string needle, string? candidate)
		{
			string cand = PlaceNameText.Normalize(candidate);
			if (cand.Length == 0)
				return 0;
			if (cand == needle)
				return 100;
			if (PlaceNameText.SameDistinctiveTokens(cand, needle))
				return 95;
			return 0;
		}

		/// <summary>Locución: <paramref name="lastOrAlone"/> true = entonación descendente.</summary>
		public static string AnnounceFile(Place place, bool lastOrAlone) =>
			lastOrAlone ? place.Announce.FinalFile : place.Announce.EnumFile;

		private string ResolvePath()
		{
			string cache = CacheFilePath;
			if (File.Exists(cache))
				return cache;

			string root = mvarEnvironment.WebRootPath ?? AppContext.BaseDirectory;
			string bundled = Path.Combine(root, "catalog", "places.xml");
			if (File.Exists(bundled))
				return bundled;

			if (!string.IsNullOrEmpty(mvarEnvironment.ContentRootPath))
			{
				string alt = Path.Combine(mvarEnvironment.ContentRootPath, "wwwroot", "catalog", "places.xml");
				if (File.Exists(alt))
					return alt;
			}

			return bundled;
		}

		private void LoadFromFile(string path)
		{
			XDocument doc = XDocument.Load(path);
			if (!TryBuild(doc, out List<Place> places, out Dictionaries maps, out List<PassengerInformation> announcements, out string error))
			{
				mvarLogger.LogWarning("PlacesCatalog: {Path} no válido ({Error}).", path, error);
				return;
			}

			byte[] bytes = File.ReadAllBytes(path);
			Assign(places, maps, announcements, path, Sha256Hex(bytes));
			mvarLogger.LogInformation(
				"PlacesCatalog: {Count} lugares, {Messages} anuncios desde {Path}.",
				places.Count,
				announcements.Count,
				path);
		}

		private void ApplyEmpty()
		{
			Assign(
				new List<Place>(),
				new Dictionaries(),
				new List<PassengerInformation>(),
				string.Empty,
				string.Empty);
		}

		private void Assign(
			List<Place> places,
			Dictionaries maps,
			List<PassengerInformation> announcements,
			string path,
			string hash)
		{
			mcolPlaces = places;
			mcolById = maps.ById;
			mcolByDiamond = maps.ByDiamond;
			mcolByAvr = maps.ByAvr;
			mcolBySfm = maps.BySfm;
			mcolByTibName = maps.ByTibName;
			mcolAnnouncements = announcements;
			mvarLoadedPath = path;
			mvarContentHash = hash;
		}

		private static bool TryBuild(
			XDocument doc,
			out List<Place> places,
			out Dictionaries maps,
			out List<PassengerInformation> announcements,
			out string error)
		{
			places = new List<Place>();
			maps = new Dictionaries();
			announcements = new List<PassengerInformation>();
			error = string.Empty;
			if (doc.Root is null
				|| !string.Equals(doc.Root.Name.LocalName, "places", StringComparison.OrdinalIgnoreCase))
			{
				error = "La raíz debe ser <places>.";
				return false;
			}

			foreach (XElement el in doc.Root.Elements("place"))
			{
				Place? place = ReadPlace(el);
				if (place is not null)
					places.Add(place);
			}

			if (places.Count == 0)
			{
				error = "Sin lugares.";
				return false;
			}

			foreach (Place place in places)
			{
				maps.ById[place.Id] = place;
				if (!string.IsNullOrEmpty(place.Avr))
					maps.ByAvr.TryAdd(place.Avr, place);
				if (place.SfmCode is int sfm)
					maps.BySfm.TryAdd(sfm, place);
				if (!string.IsNullOrEmpty(place.TibName))
					maps.ByTibName.TryAdd(place.TibName, place);
				foreach (string diamond in place.DiamondIds)
					maps.ByDiamond.TryAdd(diamond, place);
			}

			foreach (XElement group in doc.Root.Elements("messages"))
			{
				foreach (XElement msg in group.Elements("message"))
				{
					PassengerInformation? info = ReadAnnouncement(msg);
					if (info is not null)
						announcements.Add(info);
				}
			}

			return true;
		}

		internal static string Sha256Hex(byte[] payload)
		{
			byte[] hash = SHA256.HashData(payload);
			StringBuilder sb = new StringBuilder(hash.Length * 2);
			foreach (byte b in hash)
				sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
			return sb.ToString();
		}

		private sealed class Dictionaries
		{
			public Dictionary<string, Place> ById { get; } =
				new Dictionary<string, Place>(StringComparer.OrdinalIgnoreCase);
			public Dictionary<string, Place> ByDiamond { get; } =
				new Dictionary<string, Place>(StringComparer.OrdinalIgnoreCase);
			public Dictionary<string, Place> ByAvr { get; } =
				new Dictionary<string, Place>(StringComparer.OrdinalIgnoreCase);
			public Dictionary<int, Place> BySfm { get; } = new Dictionary<int, Place>();
			public Dictionary<string, Place> ByTibName { get; } =
				new Dictionary<string, Place>(StringComparer.OrdinalIgnoreCase);
		}

		private static PassengerInformation? ReadAnnouncement(XElement el)
		{
			string id = ((string?)el.Attribute("id") ?? string.Empty).Trim();
			string comment = ((string?)el.Attribute("comment") ?? string.Empty).Trim();
			if (comment.Length == 0)
			{
				string? nested = el.Element("comment")?.Value;
				comment = (nested ?? string.Empty).Trim();
			}
			if (comment.Length == 0)
				comment = id.Length > 0 ? id : "Mensaje";

			string icon = ((string?)el.Attribute("icon") ?? string.Empty).Trim();
			byte importance = PassengerInformation.MediumImportance;
			string? importanceRaw = (string?)el.Attribute("importance");
			if (byte.TryParse(importanceRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte parsed))
				importance = parsed;

			string titles = PackLanguages(el, "title", "titles");
			string texts = PackLanguages(el, "text", "texts");
			if (titles.Length == 0 && texts.Length == 0)
				return null;

			return new PassengerInformation
			{
				Comment = comment,
				IconKey = icon,
				Importance = importance,
				LanguageIndex = 0,
				TitleText = titles,
				MessageText = texts
			};
		}

		private static string PackLanguages(XElement parent, string childName, string packedAttribute)
		{
			string packed = ((string?)parent.Attribute(packedAttribute) ?? string.Empty).Trim();
			if (packed.Length > 0)
				return packed;

			string ca = string.Empty;
			string es = string.Empty;
			string en = string.Empty;
			XElement? compact = parent.Element(childName);
			if (compact is not null)
			{
				ca = ((string?)compact.Attribute("ca") ?? string.Empty).Trim();
				es = ((string?)compact.Attribute("es") ?? string.Empty).Trim();
				en = ((string?)compact.Attribute("en") ?? string.Empty).Trim();
			}

			foreach (XElement child in parent.Elements(childName))
			{
				string lang = ((string?)child.Attribute("lang") ?? string.Empty).Trim();
				string value = child.Value.Trim();
				if (value.Length == 0)
					continue;
				if (lang.Equals("ca", StringComparison.OrdinalIgnoreCase))
					ca = value;
				else if (lang.Equals("es", StringComparison.OrdinalIgnoreCase)
					|| lang.Equals("es-es", StringComparison.OrdinalIgnoreCase))
					es = value;
				else if (lang.Equals("en", StringComparison.OrdinalIgnoreCase))
					en = value;
			}

			if (ca.Length == 0 && es.Length == 0 && en.Length == 0)
				return string.Empty;
			return string.Join("|", new[] { ca, es, en });
		}

		private static Place? ReadPlace(XElement el)
		{
			string id = (string?)el.Attribute("id") ?? string.Empty;
			if (id.Length == 0)
				return null;

			XElement? keys = el.Element("keys");
			XElement? names = el.Element("names");
			XElement? announce = el.Element("announce");

			var diamondIds = new List<string>();
			if (keys is not null)
			{
				foreach (XElement d in keys.Elements("diamond"))
				{
					string did = ((string?)d.Attribute("id") ?? string.Empty).Trim();
					if (did.Length > 0)
						diamondIds.Add(did);
				}
			}

			int? sfm = null;
			string? sfmRaw = (string?)keys?.Attribute("sfm");
			if (int.TryParse(sfmRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sfmCode))
				sfm = sfmCode;

			string canonical = (string?)names?.Attribute("canonical") ?? id;
			string internalName = ReadName(names, canonical, "internal", "led");
			string externalName = ReadName(names, canonical, "external", "teleindicator");
			string tft = ReadName(names, canonical, "tft");
			string tfta = ReadName(names, tft, "tfta");
			string icon = ((string?)names?.Attribute("icon") ?? string.Empty).Trim();
			PlaceIconMode iconMode = ParseIconMode((string?)names?.Attribute("iconMode"), icon);

			string enumFile = string.Empty;
			string finalFile = string.Empty;
			if (announce is not null)
			{
				foreach (XElement clip in announce.Elements("clip"))
				{
					string role = ((string?)clip.Attribute("role") ?? string.Empty).Trim();
					string file = ((string?)clip.Attribute("file") ?? string.Empty).Trim();
					if (role.Equals("enum", StringComparison.OrdinalIgnoreCase))
						enumFile = file;
					else if (role.Equals("final", StringComparison.OrdinalIgnoreCase))
						finalFile = file;
				}
			}

			var tibStops = new List<TibStopRef>();
			var emtStops = new List<TibStopRef>();
			string tibEntity = string.Empty;
			foreach (XElement corr in el.Elements("correspondence"))
			{
				string entity = ((string?)corr.Attribute("entity") ?? "ctmr4").Trim();
				List<TibStopRef> parsed = ReadStops(corr);
				if (parsed.Count == 0)
					continue;
				if (entity.Equals("emt", StringComparison.OrdinalIgnoreCase))
					emtStops.AddRange(parsed);
				else
				{
					if (tibEntity.Length == 0)
						tibEntity = entity;
					tibStops.AddRange(parsed);
				}
			}

			return new Place
			{
				Id = id,
				Kind = ((string?)el.Attribute("kind") ?? "rail").Trim(),
				DiamondIds = diamondIds,
				Avr = ((string?)keys?.Attribute("avr") ?? string.Empty).Trim(),
				SfmCode = sfm,
				TibName = ((string?)keys?.Attribute("tibName"))?.Trim() ?? string.Empty,
				Names = new PlaceNames
				{
					Canonical = canonical,
					Internal = internalName,
					External = externalName,
					Tft = tft,
					Tfta = tfta,
					Icon = icon,
					IconMode = iconMode
				},
				Announce = new PlaceAnnounce
				{
					EnumFile = enumFile,
					FinalFile = finalFile
				},
				CorrespondenceEntity = tibEntity,
				CorrespondenceStops = tibStops,
				EmtStops = emtStops
			};
		}

		private static List<TibStopRef> ReadStops(XElement correspondence)
		{
			var stops = new List<TibStopRef>();
			foreach (XElement stopEl in correspondence.Elements("stop"))
			{
				string code = ((string?)stopEl.Attribute("code") ?? string.Empty).Trim();
				if (code.Length == 0)
					continue;
				var lines = new List<string>();
				var lineBays = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
				foreach (XElement lineEl in stopEl.Elements("line"))
				{
					string line = ((string?)lineEl.Attribute("code") ?? string.Empty).Trim();
					if (line.Length == 0)
						continue;
					lines.Add(line);
					int? lineBay = ParseBay((string?)lineEl.Attribute("bay"));
					if (lineBay is int lb)
						lineBays.TryAdd(line, lb);
				}

				foreach (XElement dockEl in stopEl.Elements("dock"))
				{
					string line = ((string?)dockEl.Attribute("line") ?? string.Empty).Trim();
					int? dockBay = ParseBay((string?)dockEl.Attribute("bay"));
					if (line.Length == 0 || dockBay is not int db)
						continue;
					lineBays.TryAdd(line, db);
				}

				stops.Add(new TibStopRef
				{
					Code = code,
					Name = ((string?)stopEl.Attribute("name"))?.Trim() ?? string.Empty,
					Bay = ParseBay((string?)stopEl.Attribute("bay")),
					Lines = lines,
					LineBays = lineBays
				});
			}
			return stops;
		}

		private static string ReadName(XElement? names, string fallback, params string[] attributes)
		{
			if (names is not null)
			{
				foreach (string attr in attributes)
				{
					string? value = (string?)names.Attribute(attr);
					if (!string.IsNullOrWhiteSpace(value))
						return value.Trim();
				}
			}
			return fallback;
		}

		private static int? ParseBay(string? raw)
		{
			if (string.IsNullOrWhiteSpace(raw))
				return null;
			foreach (string part in raw.Split(new[] { ',', '/', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
			{
				string digits = new string(part.Where(char.IsDigit).ToArray());
				if (int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bay) && bay > 0)
					return bay;
			}
			return null;
		}

		private static PlaceIconMode ParseIconMode(string? raw, string icon)
		{
			if (string.IsNullOrWhiteSpace(icon))
				return PlaceIconMode.None;

			string mode = (raw ?? string.Empty).Trim();
			if (mode.Equals("replace", StringComparison.OrdinalIgnoreCase))
				return PlaceIconMode.Replace;
			if (mode.Equals("none", StringComparison.OrdinalIgnoreCase))
				return PlaceIconMode.None;
			return PlaceIconMode.Prefix;
		}
	}

	public sealed class Place
	{
		public string Id { get; init; } = string.Empty;
		public string Kind { get; init; } = "rail";
		public IReadOnlyList<string> DiamondIds { get; init; } = Array.Empty<string>();
		public string Avr { get; init; } = string.Empty;
		public int? SfmCode { get; init; }
		public string TibName { get; init; } = string.Empty;
		public PlaceNames Names { get; init; } = new();
		public PlaceAnnounce Announce { get; init; } = new();
		public string CorrespondenceEntity { get; init; } = string.Empty;
		/// <summary>Paradas TIB (entity ctmr4).</summary>
		public IReadOnlyList<TibStopRef> CorrespondenceStops { get; init; } = Array.Empty<TibStopRef>();
		/// <summary>Paradas EMT Palma (entity emt).</summary>
		public IReadOnlyList<TibStopRef> EmtStops { get; init; } = Array.Empty<TibStopRef>();
		public bool HasCorrespondence => CorrespondenceStops.Count > 0 || EmtStops.Count > 0;
		public bool HasTibCorrespondence => CorrespondenceStops.Count > 0;
		public bool HasEmtCorrespondence => EmtStops.Count > 0;
	}

	public enum PlaceNameChannel
	{
		Canonical = 0,
		/// <summary>Teleindicador interior.</summary>
		Internal = 1,
		/// <summary>Teleindicador exterior / frontal.</summary>
		External = 2,
		/// <summary>Destino del tren y próxima parada en TFT.</summary>
		Tft = 3,
		/// <summary>Lista de próximas estaciones (tft abreviado).</summary>
		Tfta = 4
	}

	public enum PlaceIconMode
	{
		None = 0,
		Prefix = 1,
		Replace = 2
	}

	public sealed class PlaceNames
	{
		public string Canonical { get; init; } = string.Empty;
		public string Internal { get; init; } = string.Empty;
		public string External { get; init; } = string.Empty;
		public string Tft { get; init; } = string.Empty;
		public string Tfta { get; init; } = string.Empty;
		/// <summary>Clave GenIco para el TFT (vacío = sólo texto).</summary>
		public string Icon { get; init; } = string.Empty;
		public PlaceIconMode IconMode { get; init; }
	}

	public sealed class PlaceAnnounce
	{
		/// <summary>Enumeración, cualquier posición menos la última.</summary>
		public string EnumFile { get; init; } = string.Empty;
		/// <summary>Última de la enumeración, o pronunciada sola.</summary>
		public string FinalFile { get; init; } = string.Empty;
	}

	public sealed class TibStopRef
	{
		public string Code { get; init; } = string.Empty;
		public string Name { get; init; } = string.Empty;
		/// <summary>Dársena por defecto de la parada (Manacor 1/2/3, etc.).</summary>
		public int? Bay { get; init; }
		/// <summary>Vacío = todas las líneas de la parada.</summary>
		public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();
		/// <summary>Dársena por código de línea (Palma Intermodal).</summary>
		public IReadOnlyDictionary<string, int> LineBays { get; init; } =
			new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		public int? BayFor(string? lineCode)
		{
			if (!string.IsNullOrWhiteSpace(lineCode))
			{
				if (LineBays.TryGetValue(lineCode.Trim(), out int bay))
					return bay;
				string compact = lineCode.Trim();
				if (compact.StartsWith("L", StringComparison.OrdinalIgnoreCase) && compact.Length > 1)
					compact = compact[1..];
				if (LineBays.TryGetValue(compact, out bay))
					return bay;
			}

			return Bay;
		}
	}
}

using System.Globalization;
using System.Xml.Linq;
using Diamond.Project;
using Microsoft.AspNetCore.Hosting;

namespace Tourmaline26.Services.Catalog
{
	/// <summary>
	/// Catálogo de lugares (estaciones SFM y destinos de bus) cargado desde
	/// <c>wwwroot/catalog/places.xml</c>.
	/// </summary>
	public sealed class PlacesCatalog
	{
		public const string RelativePath = "catalog/places.xml";

		private readonly IReadOnlyList<Place> mcolPlaces;
		private readonly Dictionary<string, Place> mcolById;
		private readonly Dictionary<string, Place> mcolByDiamond;
		private readonly Dictionary<string, Place> mcolByAvr;
		private readonly Dictionary<int, Place> mcolBySfm;
		private readonly Dictionary<string, Place> mcolByTibName;

		public PlacesCatalog(IWebHostEnvironment environment, ILogger<PlacesCatalog> logger)
		{
			string root = environment.WebRootPath ?? AppContext.BaseDirectory;
			string path = Path.Combine(root, "catalog", "places.xml");
			if (!File.Exists(path) && !string.IsNullOrEmpty(environment.ContentRootPath))
			{
				string alt = Path.Combine(environment.ContentRootPath, "wwwroot", "catalog", "places.xml");
				if (File.Exists(alt))
					path = alt;
			}
			if (!File.Exists(path))
			{
				logger.LogWarning("PlacesCatalog: no se encontró {Path}.", path);
				mcolPlaces = Array.Empty<Place>();
				mcolById = new Dictionary<string, Place>(StringComparer.OrdinalIgnoreCase);
				mcolByDiamond = new Dictionary<string, Place>(StringComparer.OrdinalIgnoreCase);
				mcolByAvr = new Dictionary<string, Place>(StringComparer.OrdinalIgnoreCase);
				mcolBySfm = new Dictionary<int, Place>();
				mcolByTibName = new Dictionary<string, Place>(StringComparer.OrdinalIgnoreCase);
				return;
			}

			XDocument doc = XDocument.Load(path);
			var places = new List<Place>();
			foreach (XElement el in doc.Root?.Elements("place") ?? Enumerable.Empty<XElement>())
			{
				Place? place = ReadPlace(el);
				if (place is not null)
					places.Add(place);
			}

			mcolPlaces = places;
			mcolById = new Dictionary<string, Place>(StringComparer.OrdinalIgnoreCase);
			mcolByDiamond = new Dictionary<string, Place>(StringComparer.OrdinalIgnoreCase);
			mcolByAvr = new Dictionary<string, Place>(StringComparer.OrdinalIgnoreCase);
			mcolBySfm = new Dictionary<int, Place>();
			mcolByTibName = new Dictionary<string, Place>(StringComparer.OrdinalIgnoreCase);

			foreach (Place place in places)
			{
				mcolById[place.Id] = place;
				if (!string.IsNullOrEmpty(place.Avr))
					mcolByAvr.TryAdd(place.Avr, place);
				if (place.SfmCode is int sfm)
					mcolBySfm.TryAdd(sfm, place);
				if (!string.IsNullOrEmpty(place.TibName))
					mcolByTibName.TryAdd(place.TibName, place);
				foreach (string diamond in place.DiamondIds)
					mcolByDiamond.TryAdd(diamond, place);
			}

			logger.LogInformation("PlacesCatalog: {Count} lugares desde {Path}.", places.Count, path);
		}

		public IReadOnlyList<Place> Places => mcolPlaces;

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
		/// Nombre para LED o TFT. Si el lugar no está en el catálogo, se devuelve
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

		/// <summary>Nombre para LED o TFT a partir de una cadena ya mostrada.</summary>
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
				PlaceNameChannel.Led => place.Names.Led,
				PlaceNameChannel.Teleindicator => place.Names.Teleindicator,
				PlaceNameChannel.Tft => place.Names.Tft,
				_ => place.Names.Canonical
			};
			return (named ?? string.Empty).Trim();
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
				score = Math.Max(score, ScoreName(needle, place.Names.Led));
				score = Math.Max(score, ScoreName(needle, place.Names.Teleindicator));
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
			string led = (string?)names?.Attribute("led") ?? canonical;
			string tele = (string?)names?.Attribute("teleindicator") ?? canonical;
			string tft = (string?)names?.Attribute("tft") ?? canonical;
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
					Led = led,
					Teleindicator = tele,
					Tft = tft,
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
	}

	public enum PlaceNameChannel
	{
		Canonical = 0,
		Led = 1,
		Teleindicator = 2,
		Tft = 3
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
		public string Led { get; init; } = string.Empty;
		public string Teleindicator { get; init; } = string.Empty;
		public string Tft { get; init; } = string.Empty;
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

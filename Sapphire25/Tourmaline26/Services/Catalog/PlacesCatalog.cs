using System.Globalization;
using System.Xml.Linq;
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
				score = Math.Max(score, ScoreName(needle, place.Names.Teleindicator));
				score = Math.Max(score, ScoreName(needle, place.TibName));
				if (score > bestScore)
				{
					bestScore = score;
					best = place;
				}
			}

			return bestScore >= 50 ? best : null;
		}

		private static int ScoreName(string needle, string? candidate)
		{
			string cand = PlaceNameText.Normalize(candidate);
			if (cand.Length == 0)
				return 0;
			if (cand == needle)
				return 100;
			if (cand.StartsWith(needle, StringComparison.Ordinal) || needle.StartsWith(cand, StringComparison.Ordinal))
				return 80;
			if (needle.Length >= 4 && cand.Contains(needle, StringComparison.Ordinal))
				return 60;
			if (cand.Length >= 4 && needle.Contains(cand, StringComparison.Ordinal))
				return 50;
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
					Tft = tft
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
				foreach (XElement lineEl in stopEl.Elements("line"))
				{
					string line = ((string?)lineEl.Attribute("code") ?? string.Empty).Trim();
					if (line.Length > 0)
						lines.Add(line);
				}
				stops.Add(new TibStopRef
				{
					Code = code,
					Name = ((string?)stopEl.Attribute("name"))?.Trim() ?? string.Empty,
					Lines = lines
				});
			}
			return stops;
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

	public sealed class PlaceNames
	{
		public string Canonical { get; init; } = string.Empty;
		public string Led { get; init; } = string.Empty;
		public string Teleindicator { get; init; } = string.Empty;
		public string Tft { get; init; } = string.Empty;
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
		/// <summary>Vacío = todas las líneas de la parada.</summary>
		public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();
	}
}

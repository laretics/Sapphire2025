using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Diamond.Controls.Rendering
{
	/// <summary>
	/// Registro local de emisiones (JSONL) para verificación offline y como
	/// respaldo cuando no hay API de almacén. La BD Zafiro es la fuente de verdad
	/// en producción (vía host / API).
	/// </summary>
	public static class CirculationEmissionRegistry
	{
		private static readonly object Sync = new object();
		private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
		{
			WriteIndented = false,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};

		public static string? RegistryPathOverride { get; set; }

		public static string ResolveRegistryPath()
		{
			if (!string.IsNullOrWhiteSpace(RegistryPathOverride))
			{
				return RegistryPathOverride;
			}

			string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			return Path.Combine(root, "Zafiro", "CirculationEmissions", "emissions.jsonl");
		}

		public static void Append(CirculationEmissionInfo emission)
		{
			if (emission is null)
			{
				return;
			}

			string path = ResolveRegistryPath();
			string? dir = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}

			string line = JsonSerializer.Serialize(emission, JsonOpts);
			lock (Sync)
			{
				File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
			}
		}

		public static CirculationEmissionInfo? FindBySeal(string? sealCode)
		{
			string seal = NormalizeSeal(sealCode);
			if (seal.Length == 0)
			{
				return null;
			}

			string path = ResolveRegistryPath();
			if (!File.Exists(path))
			{
				return null;
			}

			CirculationEmissionInfo? last = null;
			lock (Sync)
			{
				foreach (string line in File.ReadLines(path, Encoding.UTF8))
				{
					if (string.IsNullOrWhiteSpace(line))
					{
						continue;
					}

					try
					{
						CirculationEmissionInfo? e = JsonSerializer.Deserialize<CirculationEmissionInfo>(line, JsonOpts);
						if (e is null)
						{
							continue;
						}

						if (string.Equals(NormalizeSeal(e.SealCode), seal, StringComparison.OrdinalIgnoreCase))
						{
							last = e;
						}
					}
					catch
					{
						// línea corrupta: ignorar
					}
				}
			}

			return last;
		}

		public static IReadOnlyList<CirculationEmissionInfo> ListRecent(int max = 50)
		{
			if (max < 1)
			{
				max = 1;
			}

			string path = ResolveRegistryPath();
			if (!File.Exists(path))
			{
				return Array.Empty<CirculationEmissionInfo>();
			}

			List<CirculationEmissionInfo> list = new List<CirculationEmissionInfo>();
			lock (Sync)
			{
				foreach (string line in File.ReadLines(path, Encoding.UTF8))
				{
					if (string.IsNullOrWhiteSpace(line))
					{
						continue;
					}

					try
					{
						CirculationEmissionInfo? e = JsonSerializer.Deserialize<CirculationEmissionInfo>(line, JsonOpts);
						if (e is not null)
						{
							list.Add(e);
						}
					}
					catch
					{
					}
				}
			}

			if (list.Count <= max)
			{
				return list;
			}

			return list.GetRange(list.Count - max, max);
		}

		/// <summary>
		/// Verifica un sello: criptografía (payload+código) y/o registro local.
		/// <paramref name="knownDocumentSeal"/> permite validar el SEL impreso en la ficha abierta.
		/// </summary>
		public static CirculationSealVerifyResult Verify(
			string? sealOrQrText,
			string? payloadHint = null,
			string? knownDocumentSeal = null,
			string? knownDocumentPayload = null)
		{
			CirculationSealVerifyResult result = new CirculationSealVerifyResult();
			string seal;
			string payload;

			string raw = SanitizePastedText(sealOrQrText);
			if (CirculationSheetQr.TryParseQrPayload(raw, out seal, out payload)
				&& (payload.Length > 0 || seal.Length > 0))
			{
				// QR o "SEL xxx" parseado
			}
			else
			{
				seal = NormalizeSeal(raw);
				payload = payloadHint ?? string.Empty;
			}

			seal = NormalizeSeal(seal);
			result.SealCode = seal;
			result.Payload = payload;

			if (seal.Length == 0)
			{
				result.Message = "Indica un código SEL (12 hex) o pega el texto del QR (ZAFSEL:v1:…).";
				return result;
			}

			// 1) Coincide con el documento actualmente abierto en pantalla.
			string known = NormalizeSeal(knownDocumentSeal);
			if (known.Length > 0
				&& string.Equals(known, seal, StringComparison.OrdinalIgnoreCase))
			{
				result.Ok = true;
				result.CryptographicMatch = true;
				if (!string.IsNullOrEmpty(knownDocumentPayload))
				{
					result.Payload = knownDocumentPayload;
					result.CryptographicMatch = CirculationSheetAuthenticity.VerifySealCode(
						knownDocumentPayload, seal);
				}

				result.Message = "Sello correcto: coincide con el documento abierto en pantalla.";
				// Seguir para enriquecer con registro si existe.
			}

			CirculationEmissionInfo? reg = FindBySeal(seal);
			if (reg is not null)
			{
				result.FoundInRegistry = true;
				result.Emission = reg;
				if (string.IsNullOrEmpty(payload))
				{
					payload = reg.Payload;
					result.Payload = payload;
				}
			}

			if (!string.IsNullOrEmpty(payload))
			{
				bool crypto = CirculationSheetAuthenticity.VerifySealCode(payload, seal);
				if (crypto)
				{
					result.CryptographicMatch = true;
				}
			}

			if (result.Ok && result.FoundInRegistry)
			{
				result.Message = "Sello correcto y registrado ("
					+ reg!.Channel + " · "
					+ reg.EmittedAtUtc.ToString("u", CultureInfo.InvariantCulture) + ").";
				return result;
			}

			if (result.Ok)
			{
				return result;
			}

			if (result.CryptographicMatch && result.FoundInRegistry)
			{
				result.Ok = true;
				result.Message = "Sello auténtico y registrado ("
					+ reg!.Channel + " · "
					+ reg.EmittedAtUtc.ToString("u", CultureInfo.InvariantCulture) + ").";
				return result;
			}

			if (result.CryptographicMatch)
			{
				result.Ok = true;
				result.Message = "Sello criptográficamente válido"
					+ (result.FoundInRegistry ? " y registrado." : " (no en registro local).");
				return result;
			}

			if (result.FoundInRegistry)
			{
				result.Ok = true;
				result.Message = "Sello encontrado en el registro de emisiones ("
					+ reg!.DocumentKind + " · " + reg.PlanOrTrain + ").";
				return result;
			}

			result.Ok = false;
			result.Message = "Sello no reconocido. Copia solo el código de 12 caracteres "
				+ "(p. ej. a1b2c3d4e5f6) o el pie «SEL …» del documento, o el texto del QR. "
				+ "Si el documento se abrió en otra sesión/estación, puede no estar en el registro local.";
			return result;
		}

		/// <summary>Normaliza texto pegado (SEL, espacios, puntos medios, mayúsculas).</summary>
		public static string NormalizeSeal(string? s)
		{
			if (string.IsNullOrWhiteSpace(s))
			{
				return string.Empty;
			}

			string t = SanitizePastedText(s);
			// Quitar prefijo SEL repetido
			while (t.StartsWith(CirculationSheetAuthenticity.SealPrefix, StringComparison.OrdinalIgnoreCase))
			{
				t = t.Substring(CirculationSheetAuthenticity.SealPrefix.Length).Trim();
			}

			// Si pegaron "SEL a1b2 · Zafiro…" quedarse con el primer token hex.
			int sp = t.IndexOfAny(new[] { ' ', '·', '|', '\t', ',' });
			if (sp > 0)
			{
				t = t.Substring(0, sp).Trim();
			}

			// Solo hex
			StringBuilder hex = new StringBuilder(t.Length);
			int i = 0;
			while (i < t.Length)
			{
				char c = t[i];
				if ((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))
				{
					hex.Append(char.ToLowerInvariant(c));
				}

				i++;
			}

			return hex.ToString();
		}

		private static string SanitizePastedText(string? s)
		{
			if (string.IsNullOrWhiteSpace(s))
			{
				return string.Empty;
			}

			string t = s.Trim();
			// Normalizar espacios raros / BOM
			t = t.Replace('\u00a0', ' ').Replace('\u2007', ' ').Replace('\ufeff', ' ').Trim();
			return t;
		}
	}
}

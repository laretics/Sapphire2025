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
		/// </summary>
		public static CirculationSealVerifyResult Verify(string? sealOrQrText, string? payloadHint = null)
		{
			CirculationSealVerifyResult result = new CirculationSealVerifyResult();
			string seal;
			string payload;

			if (CirculationSheetQr.TryParseQrPayload(sealOrQrText, out seal, out payload)
				&& payload.Length > 0)
			{
				// QR trae payload
			}
			else
			{
				seal = NormalizeSeal(sealOrQrText);
				payload = payloadHint ?? string.Empty;
			}

			result.SealCode = seal;
			result.Payload = payload;

			if (seal.Length == 0)
			{
				result.Message = "Indica un código SEL o pega el texto del QR.";
				return result;
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
				result.CryptographicMatch = CirculationSheetAuthenticity.VerifySealCode(payload, seal);
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
				result.Message = CirculationSheetAuthenticity.HasSigningKey || CirculationSheetAuthenticity.UsesCertificateSeal
					? "Sello criptográficamente válido (no hallado en el registro local de esta estación)."
					: "Huella coherente con el payload (sin clave de emisor: no prueba origen).";
				return result;
			}

			if (result.FoundInRegistry)
			{
				// Registro local dice que se emitió; payload pegado no coincide o no hay.
				result.Ok = true;
				result.Message = "Sello encontrado en el registro de emisiones de esta estación ("
					+ reg!.DocumentKind + " · " + reg.PlanOrTrain + ").";
				return result;
			}

			result.Ok = false;
			result.Message = "Sello no válido o no registrado en esta estación.";
			return result;
		}

		private static string NormalizeSeal(string? s)
		{
			if (string.IsNullOrWhiteSpace(s))
			{
				return string.Empty;
			}

			string t = s.Trim();
			if (t.StartsWith(CirculationSheetAuthenticity.SealPrefix, StringComparison.OrdinalIgnoreCase))
			{
				t = t.Substring(CirculationSheetAuthenticity.SealPrefix.Length).Trim();
			}

			return t;
		}
	}
}

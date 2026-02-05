using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Sapphire2026Telegram.Semantics
{
    internal class NlpProcessor
    {
		private static readonly HashSet<string> SpanishStopWords = new()
		{
			"de","del","puedes",
			"el", "la","las", "de", "que", "y", "a", "en", "un", "ser", "se", "no", "haber",
			"por", "con", "su", "para", "como", "estar", "tener", "le", "lo","los", "todo",
			"pero", "más", "hacer", "o", "poder", "decir", "este", "ir", "otro", "ese",
			"si", "me", "ya", "ver", "porque", "dar", "cuando", "él", "muy", "sin",
			"vez", "mucho", "saber", "qué", "sobre", "mi", "alguno", "mismo", "yo",
			"también", "hasta", "año", "dos", "querer", "entre", "así", "primero",
			"desde", "grande", "eso", "ni", "nos", "llegar", "pasar", "tiempo", "ella",
			"sí", "día", "uno", "bien", "poco", "deber", "entonces", "poner", "cosa",
			"tanto", "hombre", "parecer", "nuestro", "tan", "donde", "ahora", "parte",
			"después", "vida", "quedar", "siempre", "creer", "hablar", "llevar", "dejar",
			"nada", "cada", "seguir", "menos", "nuevo", "encontrar", "algo", "solo",
			"decir", "estos", "trabajar", "salir", "cual", "sea"
		};
		private static readonly Dictionary<string, string> CommandAliases = new()
		{
            // Emparejamiento
            { "emparejar", "pair" },
			{ "vincular", "pair" },
			{ "conectar", "pair" },
			{ "enlazar", "pair" },

			//Partes de incidencias
			{"parte","incidence" },
			{"avería","incidence" },
			{"incidencia","incidence" },
			{"fallo","incidence" },
			{"problema","incidence" },
			{"error","incidence" },
            
            // Estado de trenes
            { "estado", "status" },
			{ "información", "info" },
			{ "datos", "info" },
            
            // Ayuda
            { "ayuda", "help" },
			{ "ayúdame", "help" },
			{ "comandos", "help" }
		};

		// Diccionario para conversión de números en español
		// Añadir al diccionario de números españoles (después de las decenas):
		private static readonly Dictionary<string, int> SpanishNumbers = new()
{
    // Unidades
    { "cero", 0 }, { "uno", 1 }, { "una", 1 }, { "dos", 2 }, { "tres", 3 },
	{ "cuatro", 4 }, { "cinco", 5 }, { "seis", 6 }, { "siete", 7 },
	{ "ocho", 8 }, { "nueve", 9 }, { "diez", 10 },
    
    // 11-15
    { "once", 11 }, { "doce", 12 }, { "trece", 13 }, { "catorce", 14 },
	{ "quince", 15 },
    
    // 16-19
    { "dieciseis", 16 }, { "dieciséis", 16 },
	{ "diecisiete", 17 },
	{ "dieciocho", 18 },
	{ "diecinueve", 19 },
    
    // Decenas
    { "veinte", 20 }, { "veintiuno", 21 }, { "veintidos", 22 }, { "veintidós", 22 },
	{ "veintitres", 23 }, { "veintitrés", 23 }, { "veinticuatro", 24 },
	{ "veinticinco", 25 }, { "veintiseis", 26 }, { "veintiséis", 26 },
	{ "veintisiete", 27 }, { "veintiocho", 28 }, { "veintinueve", 29 },

	{ "treinta", 30 }, { "cuarenta", 40 }, { "cincuenta", 50 },
	{ "sesenta", 60 }, { "setenta", 70 }, { "ochenta", 80 }, { "noventa", 90 },
    
    // Números compuestos 31-99 (los más comunes para matrículas)
    { "treintayuno", 31 }, { "treintaydos", 32 }, { "treintaytres", 33 }, { "treintaycuatro", 34 },
	{ "treintaycinco", 35 }, { "treintayseis", 36 }, { "treintaysiete", 37 }, { "treintayocho", 38 }, { "treintaynueve", 39 },

	{ "cuarentayuno", 41 }, { "cuarentaydos", 42 }, { "cuarentaytres", 43 }, { "cuarentaycuatro", 44 },
	{ "cuarentaycinco", 45 }, { "cuarentayseis", 46 }, { "cuarentaysiete", 47 }, { "cuarentayocho", 48 }, { "cuarentaynueve", 49 },

	{ "cincuentayuno", 51 }, { "cincuentaydos", 52 }, { "cincuentaytres", 53 }, { "cincuentaycuatro", 54 },
	{ "cincuentaycinco", 55 }, { "cincuentayseis", 56 }, { "cincuentaysiete", 57 }, { "cincuentayocho", 58 }, { "cincuentaynueve", 59 },

	{ "sesentayuno", 61 }, { "sesentaydos", 62 }, { "sesentaytres", 63 }, { "sesentaycuatro", 64 },
	{ "sesentaycinco", 65 }, { "sesentayseis", 66 }, { "sesentaysiete", 67 }, { "sesentayocho", 68 }, { "sesentaynueve", 69 },

	{ "setentayuno", 71 }, { "setentaydos", 72 }, { "setentaytres", 73 }, { "setentaycuatro", 74 },
	{ "setentaycinco", 75 }, { "setentayseis", 76 }, { "setentaysiete", 77 }, { "setentayocho", 78 }, { "setentaynueve", 79 },

	{ "ochentayuno", 81 }, { "ochentaydos", 82 }, { "ochentaytres", 83 }, { "ochentaycuatro", 84 },
	{ "ochentaycinco", 85 }, { "ochentayseis", 86 }, { "ochentaysiete", 87 }, { "ochentayocho", 88 }, { "ochentaynueve", 89 },

	{ "noventayuno", 91 }, { "noventaydos", 92 }, { "noventaytres", 93 }, { "noventaycuatro", 94 },
	{ "noventaycinco", 95 }, { "noventayseis", 96 }, { "noventaysiete", 97 }, { "noventayocho", 98 }, { "noventaynueve", 99 },
    
    // Centenas
    { "cien", 100 }, { "ciento", 100 },
	{ "doscientos", 200 }, { "doscientas", 200 },
	{ "trescientos", 300 }, { "trescientas", 300 },
	{ "cuatrocientos", 400 }, { "cuatrocientas", 400 },
	{ "quinientos", 500 }, { "quinientas", 500 },
	{ "seiscientos", 600 }, { "seiscientas", 600 },
	{ "setecientos", 700 }, { "setecientas", 700 },
	{ "ochocientos", 800 }, { "ochocientas", 800 },
	{ "novecientos", 900 }, { "novecientas", 900 },
    
    // Miles
    { "mil", 1000 }
};

		#region numerals
		/// <summary>
		/// Verifica si una palabra es un número en español
		/// </summary>
		private bool IsSpanishNumber(string word)
		{
			return SpanishNumbers.ContainsKey(word);
		}

		/// <summary>
		/// Verifica si todas las palabras representan números menores a 100
		/// </summary>
		private bool AllAreLessThan100(List<string> words)
		{
			return words.All(w => SpanishNumbers.TryGetValue(w, out int val) && val < 100);
		}

		/// <summary>
		/// Parsea una secuencia de palabras numéricas a un valor entero
		/// </summary>
		private int ParseSpanishNumber(List<string> words)
		{
			int result = 0;
			int currentValue = 0;

			foreach (var word in words)
			{
				if (!SpanishNumbers.TryGetValue(word, out int value))
					continue;

				if (value == 1000)
				{
					// "mil" multiplica el valor anterior o es 1000
					if (currentValue == 0)
						currentValue = 1000;
					else
						currentValue *= 1000;

					result += currentValue;
					currentValue = 0;
				}
				else if (value == 100)
				{
					// "ciento" o "cien"
					if (currentValue == 0)
						currentValue = 100;
					else
						currentValue *= 100;
				}
				else if (value >= 100)
				{
					// Centenas (doscientos, trescientos, etc.)
					currentValue += value;
				}
				else
				{
					// Unidades y decenas
					currentValue += value;
				}
			}

			result += currentValue;
			return result;
		}


		/// <summary>
		/// Convierte una secuencia de palabras numéricas en español a un número.
		/// Maneja casos especiales de matrículas:
		/// - "ochentayuno catorce" → ["ochentayuno", "catorce"] → "8114"
		/// - "ochomil ciento catorce" → ["ocho", "mil", "ciento", "catorce"] → "8114"
		/// </summary>
		private string ConvertSpanishNumberSequence(List<string> words)
		{
			// Primero: agrupar decenas + unidades (ej: "ochenta uno" → 81)
			var consolidatedNumbers = new List<int>();
			int i = 0;

			while (i < words.Count)
			{
				if (!SpanishNumbers.TryGetValue(words[i], out int currentValue))
				{
					i++;
					continue;
				}

				// Si es una decena (30, 40, 50, etc.) y la siguiente es unidad (1-9)
				if (currentValue >= 30 && currentValue <= 90 && currentValue % 10 == 0)
				{
					// Mirar si el siguiente es una unidad (1-9)
					if (i + 1 < words.Count && SpanishNumbers.TryGetValue(words[i + 1], out int nextValue))
					{
						if (nextValue >= 1 && nextValue <= 9)
						{
							// Sumar: "ochenta uno" → 80 + 1 = 81
							consolidatedNumbers.Add(currentValue + nextValue);
							i += 2; // Saltar ambos números
							continue;
						}
					}
				}

				// Si no se pudo consolidar, añadir el número tal cual
				consolidatedNumbers.Add(currentValue);
				i++;
			}

			// Caso especial: secuencia de números pequeños para matrículas
			// Si todos son < 100, concatenar dígitos
			if (consolidatedNumbers.Count >= 2 && consolidatedNumbers.All(n => n < 100))
			{
				var digits = new StringBuilder();
				foreach (var num in consolidatedNumbers)
				{
					digits.Append(num);
				}
				return digits.ToString();
			}

			// Caso normal: número completo (sumar todo)
			return consolidatedNumbers.Sum().ToString();
		}

		/// <summary>
		/// Convierte números hablados en español a sus equivalentes numéricos.
		/// Ejemplo: "ochentayuno catorce" → "81 14" → "8114"
		/// Ejemplo: "ochomil ciento catorce" → "8000 100 14" → "8114"
		/// </summary>
		public string ConvertSpokenNumbersToDigits(string text)
		{
			// Normalizar "y" entre números: "ochenta y uno" → "ochentayuno"
			text = Regex.Replace(text, @"(\w+)\s+y\s+(\w+)", m =>
			{
				string word1 = m.Groups[1].Value;
				string word2 = m.Groups[2].Value;

				// Solo unir si ambas palabras son números
				if (IsSpanishNumber(word1) && IsSpanishNumber(word2))
					return word1 + " " + word2; // Mantener separados para procesamiento

				return m.Value; // Mantener original si no son números
			});

			// Dividir en tokens para procesar
			var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			var result = new StringBuilder();
			var currentNumber = new List<string>();

			foreach (var word in words)
			{
				if (IsSpanishNumber(word))
				{
					currentNumber.Add(word);
				}
				else
				{
					// Si había un número acumulado, procesarlo
					if (currentNumber.Count > 0)
					{
						string numericValue = ConvertSpanishNumberSequence(currentNumber);
						result.Append(numericValue).Append(" ");
						currentNumber.Clear();
					}

					// Añadir la palabra no-numérica
					result.Append(word).Append(" ");
				}
			}

			// Procesar último número pendiente
			if (currentNumber.Count > 0)
			{
				string numericValue = ConvertSpanishNumberSequence(currentNumber);
				result.Append(numericValue);
			}

			return result.ToString().Trim();
		}

		/// <summary>
		/// Extrae y convierte números de matrícula del texto.
		/// Detecta patrones como "matricula 8114" o "ut ochentayuno catorce"
		/// </summary>
		public string? ExtractVehicleNumber(string rawText)
		{
			// Normalizar texto
			string normalized = Normalize(rawText);

			// Buscar patrones de identificación de vehículo
			// Patrón 1: "matricula XXXX" o "ut XXXX" (ya en dígitos)
			var digitMatch = Regex.Match(normalized, @"(?:matricula|ut|unidad|vehiculo|tren|coche|motor|remolque|vagon|emu)\s+(\d{1,4})\b");
			if (digitMatch.Success)
				return digitMatch.Groups[1].Value;

			// Patrón 2: números hablados después de palabra clave
			var spokenMatch = Regex.Match(normalized, @"(?:matricula|ut|unidad|vehiculo|tren|coche|motor|remolque|vagon|emu)\s+(.+?)(?:\s|$)");
			if (spokenMatch.Success)
			{
				string numberPart = spokenMatch.Groups[1].Value;
				string converted = ConvertSpokenNumbersToDigits(numberPart);

				// Extraer solo dígitos del resultado
				var digitsOnly = Regex.Match(converted, @"\d{1,4}");
				if (digitsOnly.Success)
					return digitsOnly.Value;
			}

			// Patrón 3: buscar cualquier número de 1-4 cifras en el texto
			var anyNumberMatch = Regex.Match(normalized, @"\b\d{1,4}\b");
			if (anyNumberMatch.Success)
				return anyNumberMatch.Value;

			return null;
		}

		/// <summary>
		/// Consolida números contiguos en contexto de vehículos en un solo número.
		/// Ejemplo: "ut 81 14" → "ut 8114"
		/// </summary>
		private string ConsolidateVehicleNumbers(string text)
		{
			// Patrón: palabra clave de vehículo seguida de 2 números de 2 cifras cada uno
			// "ut 81 14" → "ut 8114"
			text = Regex.Replace(text,
				@"\b(matricula|ut|unidad|vehiculo|tren|coche|motor|remolque|vagon|emu)\s+(\d{1,2})\s+(\d{1,2})\b",
				m => $"{m.Groups[1].Value} {m.Groups[2].Value}{m.Groups[3].Value}",
				RegexOptions.IgnoreCase);

			// También manejar caso de 3 o 4 números separados que sumen máximo 4 dígitos
			// "ut 8 1 1 4" → "ut 8114"
			text = Regex.Replace(text,
				@"\b(matricula|ut|unidad|vehiculo|tren|coche|motor|remolque|vagon|emu)\s+(\d)\s+(\d)\s+(\d)\s+(\d)\b",
				m => $"{m.Groups[1].Value} {m.Groups[2].Value}{m.Groups[3].Value}{m.Groups[4].Value}{m.Groups[5].Value}",
				RegexOptions.IgnoreCase);

			return text;
		}

		#endregion numerals


		/// <summary>
		/// Elimina diacríticos (acentos) de una cadena
		/// </summary>
		private string RemoveDiacritics(string text)
		{
			var normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
			var stringBuilder = new System.Text.StringBuilder();

			foreach (var c in normalizedString)
			{
				var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
				if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
				{
					stringBuilder.Append(c);
				}
			}

			return stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC);
		}

		/// <summary>
		/// Normaliza el texto: minúsculas, elimina acentos, limpia caracteres especiales
		/// </summary>
		private string Normalize(string text)
		{
			// Convertir a minúsculas
			text = text.ToLowerInvariant();

			// Eliminar acentos (opcional, depende de tu caso de uso)
			text = RemoveDiacritics(text);

			// Eliminar caracteres especiales excepto espacios, números y letras
			text = Regex.Replace(text, @"[^\w\s]", " ");

			// Normalizar espacios múltiples
			text = Regex.Replace(text, @"\s+", " ").Trim();

			return text;
		}

		/// <summary>
		/// Tokeniza el texto en palabras individuales
		/// </summary>
		private string[] Tokenize(string text)
		{
			// Tokenización simple por espacios
			// Podrías usar regex más sofisticados si necesitas manejar casos especiales
			return text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		}

		/// <summary>
		/// Elimina palabras vacías (stopwords) en español
		/// </summary>
		private string[] RemoveStopWords(string[] tokens)
		{
			return tokens.Where(t => !SpanishStopWords.Contains(t)).ToArray();
		}

		/// <summary>
		/// Elimina stopwords y palabras de contexto ya procesadas
		/// </summary>
		private string[] RemoveStopWordsAndContextKeywords(string[] tokens)
		{
			var contextKeywords = new HashSet<string>
	{
		"matricula", "ut", "unidad", "vehiculo", "tren", "coche",
		"motor", "remolque", "vagon", "emu", "quiero", "abrir"
	};

			return tokens.Where(t =>
				!SpanishStopWords.Contains(t) &&
				!contextKeywords.Contains(t))
				.ToArray();
		}
		/// <summary>
		/// Aplica aliases para normalizar comandos (lematización básica)
		/// </summary>
		private string[] ApplyAliases(string[] tokens)
		{
			return tokens.Select(t => CommandAliases.TryGetValue(t, out var alias) ? alias : t)
						.ToArray();
		}

		/// <summary>
		/// Detecta patrones específicos como fechas, horas, números, etc.
		/// </summary>
		public Dictionary<string, string> ExtractPatterns(string rawText)
		{
			var patterns = new Dictionary<string, string>();

			// Patrón de fecha (dd/mm/yyyy o dd-mm-yyyy)
			var dateMatch = Regex.Match(rawText, @"\b(\d{1,2})[/-](\d{1,2})[/-](\d{4})\b");
			if (dateMatch.Success)
				patterns["date"] = dateMatch.Value;

			// Patrón de hora (HH:mm)
			var timeMatch = Regex.Match(rawText, @"\b(\d{1,2}):(\d{2})\b");
			if (timeMatch.Success)
				patterns["time"] = timeMatch.Value;

			// Patrón de código (6 dígitos para emparejamiento)
			var codeMatch = Regex.Match(rawText, @"\b\d{6}\b");
			if (codeMatch.Success)
				patterns["code"] = codeMatch.Value;

			// Patrón de GUID
			var guidMatch = Regex.Match(rawText, @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b");
			if (guidMatch.Success)
				patterns["guid"] = guidMatch.Value;

			return patterns;
		}

		/// <summary>
		/// Procesa el texto crudo y devuelve tokens limpios y normalizados
		/// </summary>
		/// <param name="rawText">Texto original del usuario</param>
		/// <returns>Array de tokens procesados</returns>
		public string[] Process(string rawText)
		{
			if (string.IsNullOrWhiteSpace(rawText))
				return Array.Empty<string>();

			// 1. Normalización
			string normalized = Normalize(rawText);

			// 2. Convierte los números hablados en la cadena a números "numéricos"
			normalized = ConvertSpokenNumbersToDigits(normalized);

			// 3. Convertir números y matrículas de vehículos en unidades.
			normalized = ConsolidateVehicleNumbers(normalized);

			// 4. Tokenización
			string[] tokens = Tokenize(normalized);

			// 5. Aplicamos los aliases antes de eliminar las stopwords de post-proceso.
			tokens = ApplyAliases(tokens);

			// 6. Eliminación de stopwords
			tokens = RemoveStopWordsAndContextKeywords(tokens);

			// 7. Eliminación de duplicados
			tokens = tokens.Distinct().ToArray();

			return tokens;
		}
	}
}

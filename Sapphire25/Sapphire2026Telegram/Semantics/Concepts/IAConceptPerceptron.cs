using System.Text.RegularExpressions;

namespace Sapphire2025Server.Telegram.Semantics.Concepts
{
	public class IAConceptPerceptron
	{
		protected List<GeneralConcept> mcolTematics;
		// Stopwords en español
		private static readonly HashSet<string> mcolStopwords = new HashSet<string>
		{			"de","la","que","el","en","y","a","los","del","se","las","por","un","para","con","no","una","su","al","lo",
			"como","más","pero","sus","le","ya","o","este","sí","porque","esta","entre","cuando","muy","sin","sobre",
			"también","me","hasta","hay","donde","quien","desde","todo","nos","durante","todos","uno","les","ni",
			"contra","otros","ese","eso","ante","ellos","e","esto","mí","antes","algunos","qué","unos","yo","otro",
			"otras","otra","él","tanto","esa","estos","mucho","quienes","nada","muchos","cual","poco","ella","estar",
			"estas","algunas","algo","nosotros","mi","mis","tú","te","ti","tu","tus","ellas","nosotras","vosotros",
			"vosotras","os"
		};
		// Lemas para palabras clave del dominio ferroviario
		private static readonly Dictionary<string, string> mcolLemmas = new Dictionary<string, string>
		{
			{"trenes", "tren"},
			{"averías", "avería"},
			{"incidencias", "incidencia"},
			{"partes", "parte"},
			{"anotaciones", "anotación"},
			{"estados", "estado"},
			{"talleres", "taller"},
			{"reparaciones", "reparación"},
			{"fallos", "fallo"},
			{"problemas", "problema"},
			{"usuarios", "usuario"}
            // Añade más según el vocabulario del taller
        };
		// Verbos de acción relevantes para el taller
		private static readonly string[] mcolVerbosAccion = { "consultar", "abrir", "añadir", "anotar", "reportar", "ver", "crear" };

		// Stemming básico para verbos de acción
		private static string StemVerb(string word)
		{
			foreach (var verbo in mcolVerbosAccion)
			{
				if (word.StartsWith(verbo))
					return verbo;
			}
			// Sufijos comunes de conjugación
			return Regex.Replace(word, @"(ar|er|ir|ando|iendo|ado|ido|aré|are|eré|ere|iré|ire|aría|aria|ería|eria|iría|iria|aba|ía|ia|aste|iste|aron|ieron|amos|imos|áis|éis|ís|ais|eis|is)$", "");
		}

		// Preprocesa el texto: elimina stopwords, lematiza y aplica stemming
		private List<string> Preprocess(string text)
		{
			var words = Regex.Split(text.ToLowerInvariant(), @"\W+")
				.Where(w => !string.IsNullOrWhiteSpace(w));

			var result = new List<string>();

			foreach (var word in words)
			{
				if (mcolStopwords.Contains(word))
					continue;

				if (mcolLemmas.TryGetValue(word, out var lemma))
				{
					result.Add(lemma);
					continue;
				}

				var stemmed = StemVerb(word);
				result.Add(stemmed);
			}

			return result;
		}
    
		public IAConceptPerceptron()
		{
			mcolTematics = new List<GeneralConcept>();
		}
		public void addConcept(GeneralConcept rhs)
		{
			mcolTematics.Add(rhs);
		}

		internal Dictionary<string, string> ExtractEntities(string text)
		{
			var entities = new Dictionary<string, string>();
			// Ejemplo: tren número
			var trenMatch = Regex.Match(text, @"tren\s*(\d+)", RegexOptions.IgnoreCase);
			if (trenMatch.Success)
				entities["tren"] = trenMatch.Groups[1].Value;

			// Ejemplo: parte/incidencia/avería número
			var parteMatch = Regex.Match(text, @"(parte|incidencia|avería)\s*(\d+)", RegexOptions.IgnoreCase);
			if (parteMatch.Success)
				entities["parte"] = parteMatch.Groups[2].Value;

			// Puedes añadir más patrones según lo que necesites extraer

			return entities;
		}

		public async Task<GeneralConcept?> Concept(string rhs, byte trigger=0)
		{
			List<string> entrada = Preprocess(rhs);
			GeneralConcept? candidato=null;
			byte maxCandidato = 0;
			foreach(GeneralConcept tematica in mcolTematics)
			{
				byte auxPuntos = await tematica.match(entrada);
				if(null==candidato || auxPuntos>maxCandidato)
				{
					maxCandidato = auxPuntos;
					candidato = tematica;					
				}
			}
			if (maxCandidato > trigger)
				return candidato;

			return null;		
		}
	}
}

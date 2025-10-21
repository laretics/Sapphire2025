namespace Sapphire2025Server.Telegram.Semantics.Concepts
{
	public class IAConceptPerceptron
	{
		protected List<GeneralConcept> mcolTematics;

		public IAConceptPerceptron()
		{
			mcolTematics = new List<GeneralConcept>();
		}
		public void addConcept(GeneralConcept rhs)
		{
			mcolTematics.Add(rhs);
		}
		public static string[] auxNormalize(string rhs)
		{
			// Lista de artículos y palabras sin significado (puedes ampliarla)
			string[] stopwords = new[] { "EL", "LA", "LOS", "LAS", "UN", "UNA", "UNOS", "UNAS", "DE", "DEL", "A", "EN", "Y", "O", "POR", "CON", "SIN", "AL" };

			// 1. Convertir a mayúsculas
			string texto = rhs.ToUpperInvariant();

			// 2. Eliminar signos de puntuación
			texto = new string(texto.Where(c => !char.IsPunctuation(c)).ToArray());

			// 3. Eliminar espacios extra y dividir en palabras
			var palabras = texto.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

			// 4. Eliminar stopwords
			var palabrasFiltradas = palabras.Where(p => !stopwords.Contains(p));

			// 5. Unir todo sin espacios
			return palabrasFiltradas.ToArray();
		}
		public async Task<GeneralConcept?> Concept(string rhs, byte trigger=128)
		{
			string[] entrada = auxNormalize(rhs);
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

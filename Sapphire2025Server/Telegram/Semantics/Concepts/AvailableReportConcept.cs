namespace Sapphire2025Server.Telegram.Semantics.Concepts
{
	public class AvailableReportConcept:VerbConcept
	{
		public TrainConcept? ToTrain { get; set; } = null;
		public override async Task<bool> match(string[] text)
		{
			// Lista de palabras clave a buscar (en mayúsculas y sin tildes)
			var keywords = new HashSet<string> { "DISPONIBLE", "DISPONIBILIDAD", "DISPONIBLES", "TRENES", "DISPOSICION" };

			// Normaliza las palabras del array: mayúsculas y sin tildes
			bool found = text
				.Select(w => SemanticAnalyzer.RemoveDiacritics(w.ToUpperInvariant()))
				.Any(w => keywords.Contains(w));
			if (found)
			{
				TrainConcept auxTrain = new TrainConcept();
				if (await auxTrain.match(text))
				{
					ToTrain = auxTrain;
					target = ToTrain;
				}					
			}
			return found;
		}
	}
}

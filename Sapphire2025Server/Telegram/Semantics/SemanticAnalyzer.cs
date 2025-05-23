namespace Sapphire2025Server.Telegram.Semantics
{
	//Este objeto admite una entrada de texto y la convierte en una cadena de tokens o conceptos
	//con los que se puede inferir una orden o una pregunta en telegram.
	public class SemanticAnalyzer
	{
		public Concept? Concept { get; set; } //Aquí se extrae el concepto pedido.
		public List<Concept> availableConcepts { get; private set; } //Lista de conceptos que se pueden reconocer en el texto
		public string text 
		{
			get
			{
				if (null == Concept)
					return "No he podido reconocer el texto";
				else
					return Concept.description;				
			}
				set
			{
				//Aquí es donde se hace el análisis de la cadena de texto
				//Primero buscamos un verbo en la cadena de texto
				string[] origen = normalize(value);
				VerbConcept? verbo = null; //Verbo encontrado en la cadena de texto
				Concept auxPalabra; //Palabra encontrada en la cadena de texto
				foreach (string palabra in origen)
				{
					foreach(Concept ppp in availableConcepts)
					{
						if (ppp.match(palabra)) //Hemos encontrado una palabra clave
						{

						}
					}
				}
			}
		}

		private string[] normalize(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
				return Array.Empty<string>();

			// Reemplaza retornos de carro, saltos de línea y tabulaciones por espacio
			var cleaned = text.Replace("\r", " ")
							  .Replace("\n", " ")
							  .Replace("\t", " ");

			// Elimina signos de puntuación usando LINQ
			cleaned = new string(cleaned
				.Where(c => !char.IsPunctuation(c))
				.ToArray());

			// Divide por espacios, elimina vacíos y pasa a mayúsculas
			return cleaned
				.Split(' ', StringSplitOptions.RemoveEmptyEntries)
				.Select(w => w.ToUpperInvariant())
				.ToArray();
		}
	}
}

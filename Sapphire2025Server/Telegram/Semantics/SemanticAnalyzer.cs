using Google.Protobuf.WellKnownTypes;
using Org.BouncyCastle.Pqc.Crypto.Picnic;
using Sapphire2025Server.Telegram.Semantics.Concepts;
using System.Reflection.Metadata.Ecma335;

namespace Sapphire2025Server.Telegram.Semantics
{
	//Este objeto admite una entrada de texto y la convierte en una cadena de tokens o conceptos
	//con los que se puede inferir una orden o una pregunta en telegram.
	public class SemanticAnalyzer
	{
		public VerbConcept? Concept { get; set; } //Aquí se extrae el concepto pedido.
		public List<GeneralConcept> availableConcepts { get; set; } //Lista de conceptos que se pueden reconocer en el texto
		//public Response response
		//{
		//	get
		//	{
		//		if (null == Concept)
		//			return new NullConceptErrorResponse();
		//		else
		//			return Concept.response;
		//	}
		//}
		//public async Task<List<VerbConcept>> setQuestion(string rhs)
		//{
		//	//Aquí es donde se hace el análisis de la cadena de texto
		//	//Primero buscamos un verbo en la cadena de texto
		//	string[] origen = normalize(rhs);
		//	List<VerbConcept> conceptos = new List<VerbConcept>();
		//	foreach (VerbConcept ppp in availableConcepts)
		//	{
		//		if (await ppp.match(origen)) //Hemos encontrado una palabra clave
		//			conceptos.Add(ppp);
		//	}
		//	return conceptos;
		//}
		//public async Task<List<GeneralConcept>> setQuestionForObjects(string rhs)
		//{
		//	string[] origen = normalize(rhs);
		//	List<GeneralConcept> conceptos = new List<GeneralConcept>();
		//	foreach (GeneralConcept ppp in availableConcepts)
		//	{
		//		if (await ppp.match(origen)) //Hemos encontrado un objeto
		//			conceptos.Add(ppp);
		//	}
		//	return conceptos;
		//}

		private string[] normalize(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
				return Array.Empty<string>();

			HashSet<string> prohibidas = new HashSet<string>(new[]
			{
			"EL", "LA", "LOS", "LAS", "EN", "ENTRE", "Y", "O", "UN", "UNA", "UNOS", "UNAS",
			"A", "HACIA", "POR", "PARA", "DE", "DEL", "AL", "CON", "SIN", "SOBRE", "E", "U"
			});

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
				.Where(w => !prohibidas.Contains(w)) // Filtra las palabras prohibidas
				.ToArray();			
		}
		// Función auxiliar para eliminar tildes/diacríticos
		internal static string RemoveDiacritics(string text)
		{
			var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
			var chars = normalized.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark);
			return new string(chars.ToArray()).Normalize(System.Text.NormalizationForm.FormC);
		}
	}
}

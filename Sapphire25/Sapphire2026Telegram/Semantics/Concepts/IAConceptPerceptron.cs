using System.Text.RegularExpressions;
using Telegram.Bot.Types;

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

		public async Task<GeneralConcept?> Concept(string[] tokens, byte trigger=0)
		{
			GeneralConcept? candidato=null;
			byte maxCandidato = 0;
			foreach(GeneralConcept tematica in mcolTematics)
			{
				byte auxPuntos = await tematica.match(tokens);
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

		/// <summary>
		/// Obtiene el grado de coincidencia de un concepto con respecto a un vector de tokens
		/// </summary>
		/// <param name="concept">Concepto a evaluar</param>
		/// <param name="rhs">Entrada preprocesada</param>
		/// <returns></returns>
		private byte Match(GeneralConcept concept, string[] rhs)
		{
			if (concept.mcolTokens.Length < 1) return 0; //Para evitar dividir por cero.
			int instancias = 0;
			foreach (string token in rhs)
			{
				if (concept.mcolTokens.Contains(token))
					instancias++;
			}
			float resultado = (instancias * 255) / concept.mcolTokens.Length;
			return (byte)resultado;
		}
	}
}

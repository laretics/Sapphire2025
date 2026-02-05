using Microsoft.EntityFrameworkCore;

namespace Sapphire2025Server.Telegram.Semantics.Concepts
{
	/// <summary>
	/// Un concepto es un término que puede reconocer el bot de telegram en un texto escrito por el usuario. Además, se puede desarrollar una frase a partir de un concepto.
	/// </summary>
	public class GeneralConcept
	{
		protected string[] mcolTokens; //Colección de tokens que va a detectar el algoritmo.
		internal IConfiguration mvarConfig;

		public GeneralConcept(string name, string tokens, IConfiguration config)
		{
			mvarConfig = config;
			//Montamos la nube de tokens:
			mcolTokens = tokens.ToUpper().Split(',');
			this.name = name;
		}
		public string name { get; private set; } //Nombre del concepto		
		public virtual async Task<byte> match(string[] text) //Obtiene el nivel de coincidencia del texto que se pasa.
		{
			if (mcolTokens.Length < 1) return 0; //Caso imposible.
			int instancias = 0;
			foreach(string palabra in text)
			{
				if (mcolTokens.Contains(palabra)) 
					instancias++;
			}
			float resultado = (instancias * 255) / mcolTokens.Length;
			return (byte)resultado;			
		}


		
	}
}

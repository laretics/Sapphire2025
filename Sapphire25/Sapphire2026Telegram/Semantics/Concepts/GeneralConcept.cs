using Microsoft.EntityFrameworkCore;

namespace Sapphire2025Server.Telegram.Semantics.Concepts
{
	/// <summary>
	/// Un concepto es un término que puede reconocer el bot de telegram en un texto escrito por el usuario.
	/// Este tipo de objeto se puede articular con elementos semánticos hasta tener toda la información requerida.
	/// </summary>
	internal abstract class GeneralConcept
	{
		internal string[] mcolTokens; //Colección de tokens que va a detectar el algoritmo.
		internal IConfiguration mvarConfig;
		protected GeneralConcept(string[] tokens, IConfiguration config)
		{
			mvarConfig = config;
			//Montamos la nube de tokens:
			mcolTokens = tokens;
			for (int i = 0; i < tokens.Length; i++)
			{
				mcolTokens[i] = mcolTokens[i].ToUpper();
			}

		}		
	}
}

using Microsoft.EntityFrameworkCore;

namespace Sapphire2025Server.Telegram.Semantics.Concepts
{
	/// <summary>
	/// Un concepto es un término que puede reconocer el bot de telegram en un texto escrito por el usuario.
	/// Este tipo de objeto se puede articular con elementos semánticos hasta tener toda la información requerida.
	/// </summary>
	internal abstract class GeneralConcept
	{
		internal List<string>  mcolTokens; //Colección de tokens que va a detectar el algoritmo.
		internal IConfiguration mvarConfig;
		protected GeneralConcept(IConfiguration config)
		{
			mvarConfig = config;
			//Montamos la nube de tokens:
			mcolTokens = new List<string>();
		}
		protected void AddTokens(string[] tokens)
		{
			foreach (string token in tokens)
				mcolTokens.Add(token.ToUpper().Trim());
		}
	}
}

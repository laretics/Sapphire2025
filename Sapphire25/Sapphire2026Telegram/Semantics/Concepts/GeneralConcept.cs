using Microsoft.EntityFrameworkCore;

namespace Sapphire2026Telegram.Semantics.Concepts
{
	/// <summary>
	/// Un concepto es un término que puede reconocer el bot de telegram en un texto escrito por el usuario.
	/// Este tipo de objeto se puede articular con elementos semánticos hasta tener toda la información requerida.
	/// </summary>
	internal abstract class GeneralConcept
	{
		internal IConfiguration mvarConfig;
		internal IServiceProvider mvarServiceProvider;
		protected GeneralConcept(IConfiguration config, IServiceProvider provider)
		{
			mvarConfig = config;
			mvarServiceProvider = provider;
			//Montamos la nube de tokens:	
		}
		/// <summary>
		/// Añade aclaraciones al concepto para poder obtener los parámetros por parte del usuario.
		/// </summary>
		/// <param name="text"></param>
		internal async virtual Task AddText(string text)
		{
			//Por defecto NO añadimos nada al concepto.
		}
	}
}

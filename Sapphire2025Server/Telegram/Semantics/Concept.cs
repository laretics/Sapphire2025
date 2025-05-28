using Sapphire2025Server.Telegram.Semantics.Responses;

namespace Sapphire2025Server.Telegram.Semantics
{
	/// <summary>
	/// Un concepto es un término que puede reconocer el bot de telegram en un texto escrito por el usuario. Además, se puede desarrollar una frase a partir de un concepto.
	/// </summary>
	public abstract class Concept
	{
		public virtual string description { get => "Concepto"; } //Descripción del concepto en lenguaje natural.
		public string name { get; private set; } //Nombre del concepto

		public virtual async Task<bool> match(string[] text) //Obtiene una coincidencia en el texto
		{
			return false;			
		}

		
	}
}

namespace Sapphire2025Server.Telegram.Semantics
{
	/// <summary>
	/// Un concepto es un término que puede reconocer el bot de telegram en un texto escrito por el usuario. Además, se puede desarrollar una frase a partir de un concepto.
	/// </summary>
	public abstract class Concept
	{
		public string wordCloud { get; private set; } //Palabras que se pueden usar para reconocer el concepto
		public virtual string description { get => "Concepto"; } //Descripción del concepto en lenguaje natural.
		public string name { get; private set; } //Nombre del concepto

		public virtual bool match(string text) //Obtiene una coincidencia en el texto
		{
			return false;			
		}
	}
}

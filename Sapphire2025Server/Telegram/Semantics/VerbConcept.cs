using Sapphire2025Server.Telegram.Semantics.Responses;

namespace Sapphire2025Server.Telegram.Semantics
{
	/// <summary>
	/// Un verbo es un concepto que establece una acción sobre otro concepto
	/// </summary>
	public class VerbConcept : Concept
	{
		public Concept? target { get; set; } //Concepto sobre el que se aplica la acción
		public virtual Response response { get => new NonImplementedResponse(); } //Devuelve una respuesta al usuario
	}
}

using Sapphire2025Models;

namespace Sapphire2025Models.Authentication
{
	//Petición al sistema para ejecutar un determinado comando o bien obtener un valor actual.
	public class CommandModel:BasicRequestModel
	{
		public string? CommandId { get; set; }
		public string? Parameter { get; set; }
	}
}

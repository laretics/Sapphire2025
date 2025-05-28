namespace Sapphire2025Server.Telegram.Semantics.Responses
{
	public class NoTrainSelectedResponse:Response
	{

		protected override byte maxResponses => 4;
		protected override string internalResponse(byte id)
		{
			switch(id)
			{
				case 0:
					return "No has seleccionado ningún tren. Por favor, indica a qué unidad te refieres.";
				case 1:
					return "Para poder seguir es necesario introducir el identificador de un tren o unidad tren.";
				case 2:
					return "¿Puedes especificar a qué tren nos estamos refiriendo?";
				default:
					return "Por favor, selecciona un número de tren para poder continuar.";
			}
		}

	}
}

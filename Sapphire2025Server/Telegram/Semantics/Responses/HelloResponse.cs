namespace Sapphire2025Server.Telegram.Semantics.Responses
{
	public class HelloResponse:Response
	{
		public HelloResponse()
		{
		}
		protected override string internalResponse(byte id)
		{
			switch(id)
			{
				case 0:
					return "Hola, soy Zafiro, el bot de Telegram de Sapphire 2025. Estoy aquí para ayudarte con tus tareas y responder a tus preguntas. ¿En qué puedo ayudarte hoy?";
				case 1:
					return "Hola. Bienvenid@. ¿En qué puedo ayudarte hoy?";
				case 2:
					return "¿Qué tal? Soy el bot de Zafiro y estoy aquí para ayudarte. ¿Qué necesitas saber?";
				default:
					return "¡Hola! Dime en qué puedo ayudarte.";
			}		
		}
		protected override byte maxResponses { get => 4; } //Número máximo de respuestas que puede devolver el objeto. Por defecto es 1, pero se puede sobreescribir en las clases hijas.
	}
}

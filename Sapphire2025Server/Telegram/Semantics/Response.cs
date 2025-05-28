namespace Sapphire2025Server.Telegram.Semantics
{
	/// <summary>
	/// Una respuesta es un objeto semántico que contiene información para responder al usuario
	/// sobre una pregunta o una orden.
	/// Está diseñado para que alterne entre los diferentes mensajes de forma que proporcione
	/// la ilusión de estar hablando con un ser humano.
	/// </summary>
	public abstract class Response
	{
		protected static Random generador = new Random(); //Generador de números aleatorios para las respuestas.
		public string text
		{
			get
			{
				byte indice = (byte)generador.Next(0, maxResponses);
				return internalResponse(indice);
			}

		} //Texto de la respuesta, si no se ha definido, se devuelve una cadena vacía.

		protected abstract string internalResponse(byte id);
		protected virtual byte maxResponses { get => 1; } //Número máximo de respuestas que puede devolver el objeto. Por defecto es 1, pero se puede sobreescribir en las clases hijas.

	}
}

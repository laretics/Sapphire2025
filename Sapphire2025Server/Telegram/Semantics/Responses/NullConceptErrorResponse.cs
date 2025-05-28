using System.Text;

namespace Sapphire2025Server.Telegram.Semantics.Responses
{
	public class NullConceptErrorResponse:Response
	{
		protected override byte maxResponses => 5;
		protected override string internalResponse(byte id)
		{
			StringBuilder salida = new StringBuilder();
			byte auxId = (byte)(generador.Next(0, 4));
			switch (id)
			{
				case 0:
					salida.Append ("No entiendo lo que dices."); break;
				case 1:
					salida.Append ("Este texto no tiene sentido para mí.");break;
				case 2:
					salida.Append ("No puedo procesar lo que has escrito."); break;
				case 3:
					salida.Append ("¿Qué has quedido decir?"); break;
				default:
					salida.Append ("No he entendido lo que me has pedido."); break;
			}
			switch(auxId)
			{
				case 0:
					salida.Append(" ¿Podrías reformularlo?"); break;
				case 1:
					salida.Append(" Por favor repite la orden."); break;
				case 2:
					salida.Append(" Escribe un texto que pueda entender."); break;
				default:
					salida.Append(" ¿Puedes intentar decirme lo mismo con otras palabras?."); break;
			}
			return salida.ToString();
		}
	}
}

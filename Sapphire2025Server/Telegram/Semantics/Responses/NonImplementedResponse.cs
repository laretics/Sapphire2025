namespace Sapphire2025Server.Telegram.Semantics.Responses
{
	public class NonImplementedResponse:Response
	{
		protected override string internalResponse(byte id)
		{
			return "[Respuesta no implementada]";
		}
	}
}

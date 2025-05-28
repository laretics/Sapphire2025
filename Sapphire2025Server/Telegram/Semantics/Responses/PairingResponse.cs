using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;

namespace Sapphire2025Server.Telegram.Semantics.Responses
{
	public class PairingResponse:Response
	{
		private byte responsePhase = 0; 
		public PairingResponse(byte phase){responsePhase = phase;}

		protected override byte maxResponses => 3;
		protected override string internalResponse(byte id)
		{
			switch(responsePhase)
			{
				case 0: //Inicial
					return responseInitial(id);
				case 1: //ID de usuario
					return responseIdUsuario(id);
				case 2: //Password
					return responsePassword(id);
				case 3: //Error de usuario no encontrado
					return responseErrorDatabaseUser(id);
				default: //No debería llegar aquí
					return "[Error desconocido]";

			}
		}
		private string responseInitial(byte id)
		{
			switch (id)
			{
				case 0:
					return "Hola, soy el bot de Zafiro. Para emparejar este chat de Telegram con tu cuenta de Zafiro, por favor introduce tu ID de usuario de Zafiro.";
				case 1:
					return "Bienvenido; soy el bot de Zafiro. Antes de continuar hay que emparejar el chat de Telegram con tu cuenta de usuario de Zafiro. Debes introducir tu ID de usuario del programa.";
				default:
					return "Saludos; soy el bot de Zafiro. Para utilizar tu cuenta de usuario con el programa y estar al día de las notificaciones, es necesario relacionar o emparejar tu cuenta con este chat. Por favor, introduce tu Id de Zafiro.";
			}
		}
		private string responseIdUsuario(byte id)
		{
			switch(id)
			{
				case 0:
					return "Introduce tu número de usuario.";
				case 1:
					return "Teclea tu número o código de usuario.";
				default:
					return "Introduce el número de carnet ferroviario o el número de usuario que utilizas para iniciar sesión en Zafiro.";
			}
		}
		private string responsePassword(byte id)
		{
			switch (id)
			{
				case 0:
					return "Ahora introduce tu contraseña de Zafiro.";
				case 1:
					return "Por favor, introduce la contraseña de tu cuenta de usuario de Zafiro.";
				default:
					return "Teclea la clave de acceso a tu cuenta de usuario de Zafiro.";
			}
		}
		private string responseErrorDatabaseUser(byte id)
		{
			switch(id)
			{
				case 0:
					return "No figura ningún usuario con estas credenciales en la base de datos.";
				case 1:
					return "El número de usuario o la contraseña no son correctos. Vuelve a intentarlo.";
				default:
					return "Los datos de sesión que has tecleado no son correctos. Hay que intentar el emparejamiento desde cero.";
			}
		}

	}
}

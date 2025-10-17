namespace Sapphire2025Server.Telegram.Semantics.Responses
{
	public class NoPermissionResponse:Response
	{
		public NoPermissionResponse(PermissionType type)
		{
			mvarType = type;
		}
		public enum PermissionType
		{
			None = 0, //Error genérico
			ZafiroDisabled = 1, //El check de Telegram está deshabilitado para este usuario.
			ZafiroScript = 2 //Existe algún punto en el script del usuario que impide el permiso.			
		}
		private PermissionType mvarType = PermissionType.None; //Tipo de permiso que se ha denegado.
		protected override string internalResponse(byte id)
		{
			switch (mvarType)
			{
				case PermissionType.ZafiroDisabled:
					return "Tienes Zafiro deshabilitado en tu cuenta de usuario. Debes entrar en la sección \"YO\" y conectarlo.";
				case PermissionType.ZafiroScript:
					return "No tienes permisos definidos en el script de configuración de la sección \"YO\" de la web de Zafiro. Inicia sesión y cambia este ajuste.";
				default:
					return "No tienes permisos para acceder a Zafiro a través de Telegram. Inicia sesión y cambia este ajuste en la sección \"YO\" de la web de Zafiro.";
			}
		}
	}
}

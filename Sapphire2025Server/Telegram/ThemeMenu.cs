
namespace Sapphire2025Server.Telegram
{
	/// <summary>
	/// Este tema es el menú inicial de opciones
	/// Se llega a él con un usuario emparejado, autenticado y con los permisos en orden.
	/// </summary>
	internal class ThemeMenu:BotTheme
	{
		internal ThemeMenu(BotTask parent) : base(parent){}
		internal async override Task<string> textFromBot()
		{
			return "De momento sólo vamos a poner este texto.";
		}

	}
}

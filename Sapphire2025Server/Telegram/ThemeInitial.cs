using Microsoft.EntityFrameworkCore;


namespace Sapphire2025Server.Telegram
{
	/// <summary>
	/// Tema inicial de una conversación con el bot de Zafiro.
	/// </summary>
	internal class ThemeInitial:BotTheme
	{
		internal ThemeInitial(BotTask parent) : base(parent){}
		internal override async Task InitializeAsync()
		{
			child = new ThemePermissions(mvarParent);
			await child.InitializeAsync();


		}
		internal override async Task<string> textFromBot()
		{
			return "Hola, ¿En qué puedo ayudarte?";
		}
		internal override async Task textToBot(string text)
		{


		}
	}
}

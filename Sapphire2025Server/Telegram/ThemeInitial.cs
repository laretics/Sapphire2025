using Microsoft.EntityFrameworkCore;
using Sapphire2025Server.Telegram.Semantics;
using Sapphire2025Server.Telegram.Semantics.Responses;


namespace Sapphire2025Server.Telegram
{
	/// <summary>
	/// Tema inicial de una conversación con el bot de Zafiro.
	/// </summary>
	internal class ThemeInitial:BotTheme
	{
		internal override async Task InitializeAsync()
		{
			child = new ThemePermissions(mvarParent);
			await child.InitializeAsync();


		}
		public ThemeInitial(BotTask parent) : base(parent){}
		internal override async Task<Response> ResponseFromBot()
		{
			return new HelloResponse();
		}
		internal override async Task textToBot(string text)
		{


		}
	}
}

namespace Sapphire2025Server.Telegram
{
	/// <summary>
	/// Tema de una conversación.
	/// </summary>
	internal abstract class BotTheme
	{
		internal BotTheme(BotTask parent, IConfiguration config)
		{
			mvarParent = parent;
			mvarConfig = config; 
		}
		internal BotTask mvarParent { get; set; } //Contenedor con la info del chat 
		internal IConfiguration mvarConfig { get; set; } //Para acceso a DB.
		internal BotTheme? child { get; set; } //Tema hijo (Las conversaciones funcionan como pilas)

		internal virtual async Task<string> textFromBot()
		{
			return string.Empty;
		}
		internal virtual async Task textToBot(string text)
		{

		}
		public async Task<string> fromBot()
		{
			if (null == child)
				return await this.textFromBot();			
			else
				return await child.textFromBot();
		}
		public async Task ToBot(string text)
		{
			if(null == child)
				await this.textToBot(text);
			else
				await child.textToBot(text);
		}

	}
}

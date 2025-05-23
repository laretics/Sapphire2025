namespace Sapphire2025Server.Telegram
{
	/// <summary>
	/// Tema de una conversación.
	/// </summary>
	internal abstract class BotTheme
	{
		internal BotTheme(BotTask parent)
		{
			mvarParent = parent;
		}
		private bool mvarEnd = false; //Esta conversación puede haber terminado o seguir activa
		internal void endTheme() {mvarEnd = true;} //Terminamos el diálogo.
		internal bool isEnded { get => mvarEnd; }
		internal BotTask mvarParent { get; set; } //Contenedor con la info del chat 
		internal BotTheme? child { get; set; } //Tema hijo (Las conversaciones funcionan como pilas)
		internal virtual async Task InitializeAsync(){}
		internal virtual async Task<string> textFromBot() {return "[Respuesta no implementada]";}
		internal virtual async Task textToBot(string text){}
		public async Task<string> fromBot()
		{			
			if (null == child||child.isEnded)
			{
				child = null;
				return await this.textFromBot();
			}				
			else
				return await child.textFromBot();
		}
		public async Task ToBot(string? text)
		{
			string auxTexto = string.Empty;
			if (null != text)
				auxTexto = text;

			if (null == child||child.isEnded)
			{
				child = null;
				await this.textToBot(auxTexto);
			}				
			else
				await child.textToBot(auxTexto);
		}

	}
}

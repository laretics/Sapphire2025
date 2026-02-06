using Sapphire2025Server.Telegram.Semantics;
using Sapphire2025Server.Telegram.Semantics.Concepts;
using Telegram.Bot;

namespace Sapphire2025Server.Telegram
{
	/// <summary>
	/// Tema de una conversación.
	/// Las conversaciones son elementos apilables que tienen la capacidad de recibir texto del usuario y contestar con otro texto.
	/// También pueden realizar una acción sobre el servidor o la base de datos.
	/// </summary>
	internal abstract class BotTheme
	{
		private bool mvarEnded = false; //Esta conversación puede haber terminado o seguir activa
		internal void endTheme() {mvarEnded = true;} //Terminamos el diálogo.
		internal bool isEnded { get => mvarEnded; }
		internal BotTheme(BotTask parent)
		{
			mvarEnded = false;
			mvarParent = parent;
			if (null == mvarParent)
				throw new ArgumentNullException(nameof(parent), "El tema no puede ser nulo");
		}
		internal BotTask mvarParent { get; set; } //Contenedor con la info del chat 
		internal BotTheme? child { get; set; } //Tema hijo (Las conversaciones funcionan como pilas)
		internal virtual async Task InitializeAsync(){}
		internal virtual async Task InternalResponseFromBot(ITelegramBotClient client) {}
		internal virtual async Task InternalTextToBot(string text){}
		public async Task ResponseFromBot(ITelegramBotClient client)
		{
			if (null != child && child.isEnded)
				child = null;

			if (null == child)
				await InternalResponseFromBot(client);
			else
				await child.InternalResponseFromBot(client);			
		}
		public async Task TextToBot(string text)
		{
			if (null != child && child.isEnded)
				child = null;
			string auxTexto = string.Empty;

			if (null == child)
				await this.InternalTextToBot(text);
			else
				await child.InternalTextToBot(text);
		}

		

	}
}

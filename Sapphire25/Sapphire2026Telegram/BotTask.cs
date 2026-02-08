using Microsoft.EntityFrameworkCore;
using Sapphire2026.Data;
using Sapphire2026.Data.Models;
using Sapphire2026Telegram.Semantics.Conversations;
using Sapphire2026Telegram.Semantics;
using Sapphire2026Telegram.Operative;

namespace Sapphire2026Telegram
{
	/// <summary>
	/// Esta clase es un contenedor para una conversación entre el bot de telegram y un usuario.
	/// </summary>
	internal class BotTask
	{
		private UserContext mvarUser;
		private bool mvarFirstMessage;
		internal IConfiguration mvarConfig;
		internal UserContext user { get => mvarUser; set => mvarUser = value; }

		internal BotTheme theme { get; set; } //Tema de la conversación actual.
		internal BotSoul parent { get; set; } // Alma de bot que posee todas las tareas
		internal BotTask(long chatId, BotSoul parent, IConfiguration config)
		{
			//Tenemos que recuperar el usuario de la base de datos.
			this.parent = parent;
			mvarUser = new UserContext(chatId,config);
			mvarConfig = config;
			mvarFirstMessage = true;
			theme = new PairingTheme(this);
		}
		internal async Task Initialize()
		{
			await user.Init();
			await theme.Preprocess();
		}

		/// <summary>
		/// Verifica el estado de emparejamiento y resetea el tema si es necesario
		/// </summary>
		private async Task VerifyPairing()
		{
			await mvarUser.Init();

			//Si ya no está emparejado, eliminamos el resto de la conversación.
			if(!mvarUser.Paired)
				theme.child = null;
		}

		/// <summary>
		/// Obtiene el texto que el bot va a enviar al usuario.
		/// </summary>
		public async Task ResponseFromBot()
		{
			// Verificar estado de emparejamiento antes de responder
			await VerifyPairing();
			await theme.ResponseFromBot(parent.mvarBot);			
		}
		/// <summary>
		/// Envía al bot el texto que ha escrito el usuario.
		/// </summary>
		/// <param name="text"></param>
		/// <returns></returns>
		public async Task TextToBot(string? text)
		{
			if (string.IsNullOrWhiteSpace(text))
				return;
			await theme.TextToBot(text);
			mvarFirstMessage = false;
			await theme.Preprocess(); //Tras la respuesta, preparamos el tema que contestará a continuación.
		}
	}
}

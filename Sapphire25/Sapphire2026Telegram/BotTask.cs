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
		private readonly NlpProcessor mvarNLPProcessor;
		internal UserContext user { get => mvarUser; }

		internal BotTheme theme { get; set; } //Tema de la conversación actual.
		internal BotSoul parent { get; set; } // Alma de bot que posee todas las tareas
		internal BotTask(long chatId, BotSoul parent, IConfiguration config)
		{
			//Tenemos que recuperar el usuario de la base de datos.
			this.parent = parent;
			mvarUser = new UserContext(chatId,config);
			mvarConfig = config;
			mvarFirstMessage = true;
			mvarNLPProcessor = new NlpProcessor();
			theme = new InitialTheme(this);
		}
		internal async Task Init()
		{
			await mvarUser.Init();
		}

		/// <summary>
		/// Asignación del ID del usuario una vez tenemos el emparejado.
		/// </summary>
		/// <param name="userId"></param>
		/// <returns></returns>
		internal async Task<bool> PairUser(Guid userId, long telegramChatId)
		{
			bool correcto = false;
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				Sapphire2026.Data.Models.User? auxUser = await almacen.Users.Where(x => x.guid == userId).FirstOrDefaultAsync();
				if (null != auxUser)
				{
					auxUser.TelegramEnabled = true;
					auxUser.TelegramId = telegramChatId;
					correcto= await almacen.SaveChangesAsync() > 0;
				}
			}
			if(correcto)
			{
				mvarUser = new UserContext(telegramChatId, mvarConfig);
				await mvarUser.Init();
				return true;
			}
			return false;
		}

		/// <summary>
		/// Obtiene el texto que el bot va a enviar al usuario.
		/// </summary>
		public async Task ResponseFromBot()
		{
			//Capturamos el pairing.
			if(null==user)
				theme.child = new PairingTheme(this);

			//Aquí cargaré el usuario (si es que no lo tenía todavía)
			await theme.ResponseFromBot(parent.mvarBot);
		}
		/// <summary>
		/// Envía al bot el texto que ha escrito el usuario.
		/// </summary>
		/// <param name="text"></param>
		/// <returns></returns>
		public async Task TextToBot(string? text)
		{
			if(!mvarFirstMessage) //Ignora el primer mensaje para preparar respuesta.
			{
				if (string.IsNullOrWhiteSpace(text))
					return;

				string[] tokens = mvarNLPProcessor.Process(text);

				await theme.TextToBot(text);
			}			
			mvarFirstMessage = false;
		}

	}
}

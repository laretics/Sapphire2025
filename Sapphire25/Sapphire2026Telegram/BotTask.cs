using Microsoft.EntityFrameworkCore;
using Sapphire2026.Data;
using Sapphire2026.Data.Models;
using Sapphire2025Server.Telegram.Semantics.Conversations;
using Sapphire2026Telegram.Semantics;
using Sapphire2026Telegram.Operative;

namespace Sapphire2025Server.Telegram
{
	/// <summary>
	/// Esta clase es un contenedor para una conversación entre el bot de telegram y un usuario.
	/// </summary>
	internal class BotTask
	{
		private UserContext mvarUser;
		internal long mvarTelegramId;
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
			mvarConfig = config;
			mvarFirstMessage = true;
			mvarTelegramId = chatId;
			mvarNLPProcessor = new NlpProcessor();
			theme = new InitialTheme(this);
		}

		/// <summary>
		/// Asignación del ID del usuario una vez tenemos el emparejado.
		/// </summary>
		/// <param name="userId"></param>
		/// <returns></returns>
		internal async Task<bool> PairUser(Guid userId)
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				Sapphire2026.Data.Models.User? auxUser = await almacen.Users.Where(x => x.Id == userId.ToString()).FirstOrDefaultAsync();
				if(null!=auxUser)
				{
					mvarUser = auxUser;
					auxUser.TelegramEnabled = true;
					auxUser.TelegramId = mvarTelegramId;
					return await almacen.SaveChangesAsync() > 0;
				}				
			}
			return false;
		}
		internal async Task<bool> FetchUser()
		{
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				Sapphire2026.Data.Models.User? auxUser = await almacen.Users.Where(x => x.TelegramId == mvarTelegramId).FirstOrDefaultAsync();
				mvarUser = auxUser;
			}
			return (null != mvarUser);
		}

		/// <summary>
		/// Obtiene el texto que el bot va a enviar al usuario.
		/// </summary>
		public async Task ResponseFromBot()
		{
			//Capturamos el pairing.
			if(null==user)
			{
				if (!await FetchUser())
					theme.child = new PairingTheme(this);
			}

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

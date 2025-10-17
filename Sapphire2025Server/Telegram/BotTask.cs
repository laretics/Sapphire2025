using Microsoft.EntityFrameworkCore;
using Sapphire2025Server.Models;
using Sapphire2025Server.Telegram.Semantics;
using Sapphire2025Server.Telegram.Semantics.Conversations;

namespace Sapphire2025Server.Telegram
{
	/// <summary>
	/// Esta clase es un contenedor para una conversación entre el bot de telegram y un usuario.
	/// </summary>
	internal class BotTask
	{
		private User? mvarUser;
		private long mvarTelegramId;
		internal User? user { get => mvarUser;} //Referencia al usuario que tiene esta conversación
		internal BotTheme theme { get; set; } //Tema de la conversación actual.
		internal static IConfiguration config { get; set; } //Para acceso a las DB
		internal BotTask(long chatId)
		{
			//Tenemos que recuperar el usuario de la base de datos.
			mvarTelegramId = chatId;
		}
		internal async Task InitializeAsync()
		{
			//Tenemos que cargar al usuario en este momento... no podemos quedarnos
			//con la última versión porque no podremos conocer los cambios en su consola
			//de telegram ni su estado de activación real.
			using (DataStorage almacen = new DataStorage(config))
			{
				mvarUser = await almacen.Users.Where(x => x.TelegramId == mvarTelegramId).FirstOrDefaultAsync();
			}
			theme = new ThemeInitial(this);
			await theme.InitializeAsync();
		}

		/// <summary>
		/// Obtiene el texto que el bot va a enviar al usuario.
		/// </summary>
		public async Task<Response> fromBot()
		{return await theme.fromBot();}
		/// <summary>
		/// Envía al bot el texto que ha escrito el usuario.
		/// </summary>
		/// <param name="text"></param>
		/// <returns></returns>
		public async Task toBot(string? text)
		{await theme.ToBot(text);}

	}
}

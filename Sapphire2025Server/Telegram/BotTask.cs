using Sapphire2025Server.Models;

namespace Sapphire2025Server.Telegram
{
	/// <summary>
	/// Esta clase es un contenedor para una conversación entre el bot de telegram y un usuario.
	/// </summary>
	internal class BotTask
	{
		internal BotTask(long chatId)
		{
			user = new User(); //Creamos un nuevo usuario al iniciar la conversación.
							   //Luego, este usuario se cargará con un valor real desde la base de datos.
							   
			user.TelegramId = chatId;
		}
		internal async Task InitializeAsync()
		{
			theme = new ThemeInitial(this);
			await theme.InitializeAsync();
		}
		internal User user { get; set; } //Referencia al usuario que tiene esta conversación
		internal BotTheme theme { get; set; } //Tema de la conversación actual.
		internal IConfiguration config { get; set; } //Para acceso a las DB

		/// <summary>
		/// Obtiene el texto que el bot va a enviar al usuario.
		/// </summary>
		public async Task<string> fromBot()
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

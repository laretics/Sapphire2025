using Microsoft.AspNetCore.Identity;

namespace Sapphire2025Server.Telegram
{
	public class TelegramInfo
	{
		private static string Token = "7400996890:AAFvz2zdrBaAiBpG9010TexLMAkjuCny-CA";
		//private TelegramBotClient mvarClient;
		internal IServiceScopeFactory mvarScopeFactory; //Necesario para cargar UserManager
		//internal UserManager<SFMUser> userManager; //Lo necesito para verificar passwords
		//internal Transactions.TransactionManager mcolTransactions; //Gestor de transacciones de Telegram.
	}
}

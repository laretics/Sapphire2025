using Microsoft.AspNetCore.Identity;
using ZafiroGmao.Data.Models;
using ZafiroGmao.Data;

namespace ZafiroGmao.Telegram.Transactions
{
	public class Help:Transaction
	{
		public Help(long chatId):base(chatId) { }
		public override async Task<string> initialMessage()
		{
			return "Por el momento, las opciones disponibles son abrir partes de averías y consultar la disponibilidad del material. ¿Desea saber algo más?";
		}
		public override async Task<string> processMessage(string rhs)
		{
			if (rhs.ToUpper().Contains("NO"))
			{
				mvarIsEnded = true;
				return "Ok.";
			}
			else
			{
				mvarIsEnded = true;
				return "En breve podré ofrecer más información.";				
			}

		}

		public override string ToString() //Descripción de este diálogo
		{
			return "Registro de nuevo usuario.";
		}

	}
}

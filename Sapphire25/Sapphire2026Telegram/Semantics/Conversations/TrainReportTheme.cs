using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;

namespace Sapphire2026Telegram.Semantics.Conversations
{
	internal class TrainReportTheme:BotTheme
	{
		internal TrainReportTheme(BotTask parent) : base(parent) { }
		internal async override Task InternalResponseFromBot(ITelegramBotClient client)
		{
			ImageResponse respuestaGrafica = new ImageResponse();





			TextResponse respuesta = new TextResponse();
			respuesta.addText("Ahora estaría mostrando un informe de estado de los trenes en la base de datos.");
			await respuesta.Send(client, mvarParent.userContext);
			this.endTheme();
		}

	}
}

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
			respuesta.addCatalog("tg.report.placeholder");
			await respuesta.Send(client, mvarParent.userContext);
			this.endTheme();
		}

	}
}

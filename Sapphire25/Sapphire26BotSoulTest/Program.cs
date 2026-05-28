using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sapphire2026Telegram;

IConfiguration auxConfig = new ConfigurationBuilder()
	.AddJsonFile("appsettings.json", optional: true)
	.Build();

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
ILogger<BotSoul> auxLogger = loggerFactory.CreateLogger<BotSoul>();

BotSoul mvarBotSoul = new BotSoul(auxLogger, auxConfig, Guid.Parse("a6fa037e-bc0f-4799-9678-ad5024c910b9"));

bool active = true;
while(active)
{
	string? auxEntrada = Console.ReadLine();
	if("exit"==auxEntrada)
		active= false;
	else
	{
		if(null!=auxEntrada)
		{
			await mvarBotSoul.HandleDummyConsoleMessage(auxEntrada);
			Console.WriteLine(BotSoul.DummyResponse);
		}
	}
}


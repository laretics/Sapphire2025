using Sapphire2026Telegram;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Sapphire26BotSoulTest
{
	internal static class BotSoulHost
	{
		internal static BotSoul? mvarInstance{ get; private set; }
		public static void Initialize (ILogger<Worker> logger, IConfiguration config, BotSoul soul)
		{
			if (null == mvarInstance)
				mvarInstance = new BotSoul(logger, config);	
		}
	}
}

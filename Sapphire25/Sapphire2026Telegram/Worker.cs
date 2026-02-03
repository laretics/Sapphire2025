namespace Sapphire2026Telegram
{
	public class Worker : BackgroundService
	{
		private readonly ILogger<Worker> mvarLogger;

		public Worker(ILogger<Worker> logger)
		{
			mvarLogger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				if (mvarLogger.IsEnabled(LogLevel.Information))
				{
					mvarLogger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
				}
				await Task.Delay(1000, stoppingToken);
			}
		}
	}
}

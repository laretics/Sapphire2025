using Tourmaline26.Logic;

namespace Tourmaline26.Services.PassengerAudio
{
	public sealed class NullAnnouncementSink : IAnnouncementSink
	{
		private readonly ILogger<NullAnnouncementSink> mvarLogger;

		public NullAnnouncementSink(ILogger<NullAnnouncementSink> logger)
		{
			mvarLogger = logger;
		}

		public PassengerAudioSinkKind Kind => PassengerAudioSinkKind.None;

		public Task Play(
			ComposedAnnouncement announcement,
			PassengerAudioOptions options,
			CancellationToken cancellationToken)
		{
			mvarLogger.LogInformation(
				"Megafonía Sink=None; no se reproduce ({Duration:g}, {Parts} partes).",
				announcement.Duration,
				announcement.Parts.Count);
			return Task.CompletedTask;
		}
	}
}

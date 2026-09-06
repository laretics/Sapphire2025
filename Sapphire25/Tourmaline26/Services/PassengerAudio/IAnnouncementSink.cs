using Tourmaline26.Logic;

namespace Tourmaline26.Services.PassengerAudio
{
	public interface IAnnouncementSink
	{
		PassengerAudioSinkKind Kind { get; }
		Task Play(ComposedAnnouncement announcement, PassengerAudioOptions options, CancellationToken cancellationToken);
	}
}

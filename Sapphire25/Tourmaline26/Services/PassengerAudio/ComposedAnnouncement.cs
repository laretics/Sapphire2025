namespace Tourmaline26.Services.PassengerAudio
{
	/// <summary>WAV PCM listo para un sink, con duración exacta de muestras.</summary>
	public sealed class ComposedAnnouncement
	{
		public byte[] WavFile { get; }
		public TimeSpan Duration { get; }
		public int SampleRate { get; }
		public short Channels { get; }
		public short BitsPerSample { get; }
		public IReadOnlyList<string> Parts { get; }
		public WavPcm Pcm { get; }

		public ComposedAnnouncement(WavPcm pcm, IReadOnlyList<string> parts)
		{
			Pcm = pcm;
			WavFile = pcm.ToWavFile();
			Duration = pcm.Duration;
			SampleRate = pcm.SampleRate;
			Channels = pcm.Channels;
			BitsPerSample = pcm.BitsPerSample;
			Parts = parts;
		}
	}
}

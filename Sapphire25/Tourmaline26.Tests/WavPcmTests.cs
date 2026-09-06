using System.Buffers.Binary;
using Tourmaline26.Logic;
using Tourmaline26.Services.PassengerAudio;

namespace Tourmaline26.Tests;

public sealed class WavPcmTests
{
	[Fact]
	public void Concat_three_clips_duration_is_sum_plus_gaps()
	{
		WavPcm a = Tone(22050, 1, TimeSpan.FromMilliseconds(200), 440);
		WavPcm b = Tone(22050, 1, TimeSpan.FromMilliseconds(200), 554);
		WavPcm c = Tone(22050, 1, TimeSpan.FromMilliseconds(200), 659);
		TimeSpan gap = TimeSpan.FromMilliseconds(100);

		WavPcm composed = WavPcm.Concat([a, b, c], gap);
		byte[] wav = composed.ToWavFile();
		WavPcm roundTrip = WavPcm.Load(wav);

		Assert.Equal(22050, composed.SampleRate);
		Assert.Equal(1, composed.Channels);
		Assert.Equal(16, composed.BitsPerSample);
		Assert.Equal(composed.Pcm.Length + 44, wav.Length);
		Assert.InRange(composed.Duration.TotalMilliseconds, 795, 805);
		Assert.Equal(composed.Pcm.Length, roundTrip.Pcm.Length);
		Assert.Equal(composed.Duration, roundTrip.Duration);
	}

	[Fact]
	public void Concat_does_not_glue_riff_headers()
	{
		WavPcm a = Tone(8000, 1, TimeSpan.FromMilliseconds(50), 440);
		WavPcm b = Tone(8000, 1, TimeSpan.FromMilliseconds(50), 880);
		WavPcm composed = WavPcm.Concat([a, b], TimeSpan.Zero);

		int riffCount = 0;
		byte[] wav = composed.ToWavFile();
		for (int i = 0; i <= wav.Length - 4; i++)
		{
			if (wav[i] == (byte)'R' && wav[i + 1] == (byte)'I'
				&& wav[i + 2] == (byte)'F' && wav[i + 3] == (byte)'F')
				riffCount++;
		}

		Assert.Equal(1, riffCount);
		Assert.Equal(a.Pcm.Length + b.Pcm.Length, composed.Pcm.Length);
	}

	[Fact]
	public void ToMono8k16_keeps_wall_clock_duration()
	{
		WavPcm source = Tone(44100, 2, TimeSpan.FromSeconds(1), 440);
		WavPcm converted = source.ToMono8k16();

		Assert.Equal(8000, converted.SampleRate);
		Assert.Equal(1, converted.Channels);
		Assert.Equal(16, converted.BitsPerSample);
		Assert.InRange(converted.SampleCount, 7990, 8010);
		Assert.InRange(converted.Duration.TotalMilliseconds, 990, 1010);
	}

	[Fact]
	public void Sink_kind_parses_from_appsettings_names()
	{
		Assert.Equal(PassengerAudioSinkKind.Analog, new PassengerAudioOptions { Sink = "Analog" }.SinkKind);
		Assert.Equal(PassengerAudioSinkKind.Sip, new PassengerAudioOptions { Sink = "SIP" }.SinkKind);
		Assert.Equal(PassengerAudioSinkKind.None, new PassengerAudioOptions { Sink = "None" }.SinkKind);
	}

	private static WavPcm Tone(int sampleRate, short channels, TimeSpan duration, double hz)
	{
		int frames = Math.Max(1, (int)Math.Round(sampleRate * duration.TotalSeconds));
		byte[] pcm = new byte[frames * channels * 2];
		for (int i = 0; i < frames; i++)
		{
			short sample = (short)(Math.Sin(2 * Math.PI * hz * i / sampleRate) * 8000);
			for (int ch = 0; ch < channels; ch++)
				BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan((i * channels + ch) * 2, 2), sample);
		}

		return new WavPcm(sampleRate, channels, 16, pcm);
	}
}

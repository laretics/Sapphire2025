using System.Buffers.Binary;
using System.Text;

namespace Tourmaline26.Services.PassengerAudio
{
	/// <summary>
	/// WAV PCM lineal. Concatenar archivos (headers RIFF) no vale: se concatenan
	/// las muestras y se escribe un solo header. Duración = muestras / sampleRate.
	/// </summary>
	public sealed class WavPcm
	{
		public const short PcmFormatTag = 1;

		public int SampleRate { get; }
		public short Channels { get; }
		public short BitsPerSample { get; }
		public byte[] Pcm { get; }

		public int BytesPerSample => BitsPerSample / 8;
		public int BlockAlign => BytesPerSample * Channels;
		public int BytesPerSecond => SampleRate * BlockAlign;
		public int SampleCount => BlockAlign == 0 ? 0 : Pcm.Length / BlockAlign;
		public TimeSpan Duration => BytesPerSecond <= 0
			? TimeSpan.Zero
			: TimeSpan.FromSeconds(Pcm.Length / (double)BytesPerSecond);

		public WavPcm(int sampleRate, short channels, short bitsPerSample, byte[] pcm)
		{
			if (sampleRate <= 0)
				throw new ArgumentOutOfRangeException(nameof(sampleRate));
			if (channels is not 1 and not 2)
				throw new ArgumentOutOfRangeException(nameof(channels), "Solo mono o estéreo.");
			if (bitsPerSample is not 8 and not 16)
				throw new ArgumentOutOfRangeException(nameof(bitsPerSample), "Solo PCM 8 o 16 bit.");
			ArgumentNullException.ThrowIfNull(pcm);

			int align = (bitsPerSample / 8) * channels;
			if (align > 0 && pcm.Length % align != 0)
				throw new ArgumentException("El PCM no está alineado al bloque.", nameof(pcm));

			SampleRate = sampleRate;
			Channels = channels;
			BitsPerSample = bitsPerSample;
			Pcm = pcm;
		}

		public static WavPcm Load(string path) => Load(File.ReadAllBytes(path));

		public static WavPcm Load(ReadOnlySpan<byte> wav)
		{
			if (wav.Length < 44)
				throw new InvalidDataException("WAV demasiado corto.");
			if (!FourCcEquals(wav, 0, "RIFF") || !FourCcEquals(wav, 8, "WAVE"))
				throw new InvalidDataException("No es un RIFF/WAVE.");

			int offset = 12;
			int fmtOffset = -1;
			int fmtSize = 0;
			int dataOffset = -1;
			int dataSize = 0;

			while (offset + 8 <= wav.Length)
			{
				string id = Encoding.ASCII.GetString(wav.Slice(offset, 4));
				int size = BinaryPrimitives.ReadInt32LittleEndian(wav.Slice(offset + 4, 4));
				if (size < 0)
					throw new InvalidDataException("Chunk WAV corrupto.");
				int payload = offset + 8;
				if (id == "fmt ")
				{
					fmtOffset = payload;
					fmtSize = size;
				}
				else if (id == "data")
				{
					dataOffset = payload;
					dataSize = size;
					break;
				}

				offset = payload + size;
				if ((size & 1) == 1)
					offset++;
			}

			if (fmtOffset < 0 || fmtSize < 16)
				throw new InvalidDataException("Falta el chunk fmt.");
			if (dataOffset < 0)
				throw new InvalidDataException("Falta el chunk data.");
			if (dataOffset + dataSize > wav.Length)
				dataSize = wav.Length - dataOffset;

			short formatTag = BinaryPrimitives.ReadInt16LittleEndian(wav.Slice(fmtOffset, 2));
			if (formatTag != PcmFormatTag)
				throw new InvalidDataException($"WAV no PCM (format {formatTag}). Recodificad a PCM lineal 16 bit.");

			short channels = BinaryPrimitives.ReadInt16LittleEndian(wav.Slice(fmtOffset + 2, 2));
			int sampleRate = BinaryPrimitives.ReadInt32LittleEndian(wav.Slice(fmtOffset + 4, 4));
			short bits = BinaryPrimitives.ReadInt16LittleEndian(wav.Slice(fmtOffset + 14, 2));

			byte[] pcm = wav.Slice(dataOffset, dataSize).ToArray();
			return new WavPcm(sampleRate, channels, bits, pcm);
		}

		public static WavPcm Silence(WavPcm format, TimeSpan duration)
		{
			if (duration <= TimeSpan.Zero)
				return new WavPcm(format.SampleRate, format.Channels, format.BitsPerSample, Array.Empty<byte>());

			int bytes = (int)Math.Round(duration.TotalSeconds * format.BytesPerSecond);
			int align = format.BlockAlign;
			if (align > 0)
				bytes -= bytes % align;
			if (bytes < 0)
				bytes = 0;

			byte[] pcm = new byte[bytes];
			if (format.BitsPerSample == 8)
				Array.Fill(pcm, (byte)128);
			return new WavPcm(format.SampleRate, format.Channels, format.BitsPerSample, pcm);
		}

		public static WavPcm Concat(IReadOnlyList<WavPcm> clips, TimeSpan gap)
		{
			ArgumentNullException.ThrowIfNull(clips);
			if (clips.Count == 0)
				throw new ArgumentException("No hay clips que concatenar.", nameof(clips));

			WavPcm format = clips[0];
			var parts = new List<byte[]>(clips.Count * 2);
			int total = 0;
			WavPcm? silence = gap > TimeSpan.Zero ? Silence(format, gap) : null;

			for (int i = 0; i < clips.Count; i++)
			{
				WavPcm clip = clips[i].ToFormat(format.SampleRate, format.Channels, format.BitsPerSample);
				parts.Add(clip.Pcm);
				total += clip.Pcm.Length;
				if (silence is not null && i < clips.Count - 1)
				{
					parts.Add(silence.Pcm);
					total += silence.Pcm.Length;
				}
			}

			byte[] pcm = new byte[total];
			int offset = 0;
			foreach (byte[] part in parts)
			{
				Buffer.BlockCopy(part, 0, pcm, offset, part.Length);
				offset += part.Length;
			}

			return new WavPcm(format.SampleRate, format.Channels, format.BitsPerSample, pcm);
		}

		public WavPcm ToMono8k16() => ToFormat(8000, 1, 16);

		public WavPcm ToFormat(int sampleRate, short channels, short bitsPerSample)
		{
			if (sampleRate == SampleRate && channels == Channels && bitsPerSample == BitsPerSample)
				return this;

			short[] source = ToInt16MonoOrStereo();
			short srcCh = Channels;
			int srcRate = SampleRate;
			int srcFrames = source.Length / srcCh;

			double ratio = sampleRate / (double)srcRate;
			int dstFrames = srcFrames == 0 ? 0 : Math.Max(1, (int)Math.Round(srcFrames * ratio));
			short[] dest = new short[dstFrames * channels];

			for (int i = 0; i < dstFrames; i++)
			{
				double srcPos = dstFrames == 1 ? 0 : i * (srcFrames - 1) / (double)(dstFrames - 1);
				int i0 = (int)srcPos;
				int i1 = Math.Min(i0 + 1, srcFrames - 1);
				double t = srcPos - i0;

				for (int ch = 0; ch < channels; ch++)
				{
					short s0 = SampleChannel(source, srcCh, i0, ch);
					short s1 = SampleChannel(source, srcCh, i1, ch);
					dest[i * channels + ch] = (short)Math.Clamp(
						Math.Round(s0 + (s1 - s0) * t),
						short.MinValue,
						short.MaxValue);
				}
			}

			if (bitsPerSample == 8)
			{
				byte[] u8 = new byte[dest.Length];
				for (int i = 0; i < dest.Length; i++)
					u8[i] = (byte)Math.Clamp((dest[i] / 256) + 128, 0, 255);
				return new WavPcm(sampleRate, channels, 8, u8);
			}

			byte[] pcm16 = new byte[dest.Length * 2];
			for (int i = 0; i < dest.Length; i++)
				BinaryPrimitives.WriteInt16LittleEndian(pcm16.AsSpan(i * 2, 2), dest[i]);
			return new WavPcm(sampleRate, channels, 16, pcm16);
		}

		public byte[] ToWavFile()
		{
			int dataSize = Pcm.Length;
			int fileSize = 36 + dataSize;
			byte[] wav = new byte[8 + fileSize];
			Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
			BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(4, 4), fileSize);
			Encoding.ASCII.GetBytes("WAVE").CopyTo(wav, 8);
			Encoding.ASCII.GetBytes("fmt ").CopyTo(wav, 12);
			BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(16, 4), 16);
			BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(20, 2), PcmFormatTag);
			BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(22, 2), Channels);
			BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(24, 4), SampleRate);
			BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(28, 4), BytesPerSecond);
			BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(32, 2), (short)BlockAlign);
			BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(34, 2), BitsPerSample);
			Encoding.ASCII.GetBytes("data").CopyTo(wav, 36);
			BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(40, 4), dataSize);
			Buffer.BlockCopy(Pcm, 0, wav, 44, dataSize);
			return wav;
		}

		private short[] ToInt16MonoOrStereo()
		{
			int frames = SampleCount;
			short[] samples = new short[frames * Channels];
			if (BitsPerSample == 16)
			{
				for (int i = 0; i < samples.Length; i++)
					samples[i] = BinaryPrimitives.ReadInt16LittleEndian(Pcm.AsSpan(i * 2, 2));
				return samples;
			}

			for (int i = 0; i < samples.Length; i++)
				samples[i] = (short)((Pcm[i] - 128) * 256);
			return samples;
		}

		private static short SampleChannel(short[] source, short srcChannels, int frame, int destChannel)
		{
			if (srcChannels == 1)
				return source[frame];
			if (destChannel >= srcChannels)
				return (short)((source[frame * srcChannels] + source[frame * srcChannels + 1]) / 2);
			return source[frame * srcChannels + destChannel];
		}

		private static bool FourCcEquals(ReadOnlySpan<byte> data, int offset, string four)
		{
			return data[offset] == four[0]
				&& data[offset + 1] == four[1]
				&& data[offset + 2] == four[2]
				&& data[offset + 3] == four[3];
		}
	}
}

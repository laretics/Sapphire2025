using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using NAudio.Wave;
using Tourmaline26.Logic;

namespace Tourmaline26.Services.PassengerAudio
{
	/// <summary>
	/// Reproduce el WAV en una salida WaveOut del servidor (DAC USB → PRIORITY IN
	/// del Optimus, o altavoces del PC en desarrollo).
	/// </summary>
	public sealed class AnalogAnnouncementSink : IAnnouncementSink
	{
		private readonly ILogger<AnalogAnnouncementSink> mvarLogger;

		public AnalogAnnouncementSink(ILogger<AnalogAnnouncementSink> logger)
		{
			mvarLogger = logger;
		}

		public PassengerAudioSinkKind Kind => PassengerAudioSinkKind.Analog;

		public Task Play(
			ComposedAnnouncement announcement,
			PassengerAudioOptions options,
			CancellationToken cancellationToken)
		{
			if (!OperatingSystem.IsWindows())
				throw new PlatformNotSupportedException("La salida analógica usa WaveOut y solo corre en Windows.");

			return PlayWindows(announcement, options, cancellationToken);
		}

		[SupportedOSPlatform("windows")]
		private async Task PlayWindows(
			ComposedAnnouncement announcement,
			PassengerAudioOptions options,
			CancellationToken cancellationToken)
		{
			int device = options.Analog.DeviceNumber;
			int latency = options.Analog.LatencyMilliseconds;
			if (latency < 50)
				latency = 50;

			mvarLogger.LogInformation(
				"Megafonía analógica: dispositivo {Device}, {Duration:g}, {Rate} Hz {Bits} bit {Ch} ch.",
				device,
				announcement.Duration,
				announcement.SampleRate,
				announcement.BitsPerSample,
				announcement.Channels);

			var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			using var reader = new WaveFileReader(new MemoryStream(announcement.WavFile, writable: false));
			using var output = new WaveOutEvent
			{
				DeviceNumber = device,
				DesiredLatency = latency
			};
			output.PlaybackStopped += (_, args) =>
			{
				if (args.Exception is not null)
					stopped.TrySetException(args.Exception);
				else
					stopped.TrySetResult();
			};
			output.Init(reader);
			output.Play();

			await using (cancellationToken.Register(() =>
			{
				try { output.Stop(); }
				catch (Exception ex) { mvarLogger.LogDebug(ex, "Stop analógico al cancelar."); }
			}))
			{
				await stopped.Task.WaitAsync(cancellationToken);
			}
		}

		public static IReadOnlyList<AnalogDeviceInfo> ListDevices()
		{
			if (!OperatingSystem.IsWindows())
				return Array.Empty<AnalogDeviceInfo>();

			var list = new List<AnalogDeviceInfo>
			{
				new AnalogDeviceInfo(-1, "Predeterminado (WAVE_MAPPER)", 2)
			};
			int count = Native.waveOutGetNumDevs();
			for (int i = 0; i < count; i++)
			{
				var caps = new Native.WAVEOUTCAPS();
				int size = Marshal.SizeOf<Native.WAVEOUTCAPS>();
				if (Native.waveOutGetDevCaps((IntPtr)i, ref caps, size) == 0)
					list.Add(new AnalogDeviceInfo(i, caps.szPname ?? $"Dispositivo {i}", caps.wChannels));
				else
					list.Add(new AnalogDeviceInfo(i, $"Dispositivo {i}", 0));
			}
			return list;
		}

		static class Native
		{
			[DllImport("winmm.dll", CharSet = CharSet.Auto)]
			internal static extern int waveOutGetNumDevs();

			[DllImport("winmm.dll", CharSet = CharSet.Auto)]
			internal static extern int waveOutGetDevCaps(IntPtr deviceId, ref WAVEOUTCAPS caps, int capsSize);

			[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
			internal struct WAVEOUTCAPS
			{
				public ushort wMid;
				public ushort wPid;
				public uint vDriverVersion;
				[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
				public string szPname;
				public uint dwFormats;
				public ushort wChannels;
				public ushort wReserved1;
				public uint dwSupport;
			}
		}
	}

	public sealed record AnalogDeviceInfo(int Number, string Name, int Channels);
}

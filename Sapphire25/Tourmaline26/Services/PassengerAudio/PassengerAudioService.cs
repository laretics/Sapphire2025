using Tourmaline26.Logic;

namespace Tourmaline26.Services.PassengerAudio
{
	/// <summary>
	/// Compone locuciones PCM y las lanza por el sink configurado (analógico o SIP).
	/// Un anuncio a la vez. Respeta <see cref="FeatureSwitches.SoundEnabled"/>.
	/// </summary>
	public sealed class PassengerAudioService
	{
		private readonly IConfiguration mvarConfiguration;
		private readonly IWebHostEnvironment mvarEnvironment;
		private readonly TourmalineService mvarTourmaline;
		private readonly ILogger<PassengerAudioService> mvarLogger;
		private readonly ILoggerFactory mvarLoggerFactory;
		private readonly SemaphoreSlim mvarGate = new(1, 1);

		private string mvarLastError = string.Empty;
		private TimeSpan mvarLastDuration = TimeSpan.Zero;

		public PassengerAudioService(
			IConfiguration configuration,
			IWebHostEnvironment environment,
			TourmalineService tourmaline,
			ILogger<PassengerAudioService> logger,
			ILoggerFactory loggerFactory)
		{
			mvarConfiguration = configuration;
			mvarEnvironment = environment;
			mvarTourmaline = tourmaline;
			mvarLogger = logger;
			mvarLoggerFactory = loggerFactory;
		}

		public bool IsPlaying { get; private set; }
		public TimeSpan LastDuration => mvarLastDuration;
		public string LastError => mvarLastError;

		public PassengerAudioOptions Options()
		{
			PassengerAudioOptions? loaded = mvarConfiguration
				.GetSection("PassengerAudio")
				.Get<PassengerAudioOptions>();
			return loaded ?? new PassengerAudioOptions();
		}

		public object Status()
		{
			PassengerAudioOptions options = Options();
			return new
			{
				sink = options.SinkKind.ToString(),
				playing = IsPlaying,
				soundEnabled = mvarTourmaline.SessionConfig.MainSwitches.SoundEnabled,
				lastDurationMs = mvarLastDuration.TotalMilliseconds,
				lastError = mvarLastError,
				finishBeforeStopSeconds = options.FinishBeforeStopSeconds,
				setupMarginMilliseconds = options.SetupMarginMilliseconds,
				analogDevices = AnalogAnnouncementSink.ListDevices()
			};
		}

		public IReadOnlyList<AnalogDeviceInfo> ListAnalogDevices() => AnalogAnnouncementSink.ListDevices();

		public ComposedAnnouncement Compose(IReadOnlyList<string> files)
		{
			PassengerAudioOptions options = Options();
			if (files is null || files.Count == 0)
				throw new ArgumentException("No hay clips que componer.", nameof(files));

			TimeSpan gap = TimeSpan.FromMilliseconds(Math.Max(0, options.GapMilliseconds));
			var clips = new List<WavPcm>(files.Count);
			var resolved = new List<string>(files.Count);
			foreach (string file in files)
			{
				string path = ResolvePath(file, options);
				if (!File.Exists(path))
					throw new FileNotFoundException($"Clip de megafonía no encontrado: {path}", path);
				clips.Add(WavPcm.Load(path));
				resolved.Add(path);
			}

			WavPcm pcm = WavPcm.Concat(clips, gap);
			var composed = new ComposedAnnouncement(pcm, resolved);
			mvarLogger.LogInformation(
				"Locución compuesta: {Parts} clips, {Duration:g}, {Bytes} bytes WAV.",
				resolved.Count,
				composed.Duration,
				composed.WavFile.Length);
			return composed;
		}

		public async Task<ComposedAnnouncement> PlayFilesAsync(
			IReadOnlyList<string> files,
			CancellationToken cancellationToken = default)
		{
			ComposedAnnouncement composed = Compose(files);
			await PlayAsync(composed, cancellationToken);
			return composed;
		}

		public Task<ComposedAnnouncement> PlayTestAsync(CancellationToken cancellationToken = default)
		{
			PassengerAudioOptions options = Options();
			if (options.TestFiles.Count == 0)
				throw new InvalidOperationException("PassengerAudio:TestFiles está vacío.");
			return PlayFilesAsync(options.TestFiles, cancellationToken);
		}

		public async Task PlayAsync(ComposedAnnouncement announcement, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(announcement);
			PassengerAudioOptions options = Options();

			if (!mvarTourmaline.SessionConfig.MainSwitches.SoundEnabled)
			{
				mvarLogger.LogInformation("Megafonía silenciada (SoundEnabled=false). Duración {Duration:g}.", announcement.Duration);
				mvarLastDuration = announcement.Duration;
				return;
			}

			if (!await mvarGate.WaitAsync(0, cancellationToken))
				throw new InvalidOperationException("Ya hay un anuncio en curso.");

			IsPlaying = true;
			mvarLastError = string.Empty;
			mvarLastDuration = announcement.Duration;
			mvarTourmaline.SessionConfig.SpeakersAnnouncing = true;
			mvarTourmaline.RaiseHMIUpdate();
			try
			{
				IAnnouncementSink sink = CreateSink(options.SinkKind);
				await sink.Play(announcement, options, cancellationToken);
			}
			catch (Exception ex)
			{
				mvarLastError = ex.Message;
				mvarLogger.LogError(ex, "Fallo al reproducir locución por {Sink}.", options.SinkKind);
				throw;
			}
			finally
			{
				IsPlaying = false;
				mvarTourmaline.SessionConfig.SpeakersAnnouncing = false;
				mvarTourmaline.RaiseHMIUpdate();
				mvarGate.Release();
			}
		}

		private IAnnouncementSink CreateSink(PassengerAudioSinkKind kind) => kind switch
		{
			PassengerAudioSinkKind.Analog => new AnalogAnnouncementSink(
				mvarLoggerFactory.CreateLogger<AnalogAnnouncementSink>()),
			PassengerAudioSinkKind.Sip => new SipAnnouncementSink(
				mvarLoggerFactory.CreateLogger<SipAnnouncementSink>()),
			_ => new NullAnnouncementSink(
				mvarLoggerFactory.CreateLogger<NullAnnouncementSink>())
		};

		private string ResolvePath(string file, PassengerAudioOptions options)
		{
			string trimmed = (file ?? string.Empty).Trim().Replace('/', Path.DirectorySeparatorChar);
			if (trimmed.Length == 0)
				throw new ArgumentException("Ruta de clip vacía.");
			if (Path.IsPathRooted(trimmed) && File.Exists(trimmed))
				return trimmed;

			string web = mvarEnvironment.WebRootPath ?? AppContext.BaseDirectory;
			string content = mvarEnvironment.ContentRootPath ?? AppContext.BaseDirectory;
			string clipRoot = (options.ClipRoot ?? string.Empty).Trim().Replace('/', Path.DirectorySeparatorChar);

			string[] candidates =
			[
				Path.Combine(web, trimmed),
				Path.Combine(content, trimmed),
				Path.Combine(web, clipRoot, Path.GetFileName(trimmed)),
				Path.Combine(content, "wwwroot", trimmed),
				Path.Combine(content, "wwwroot", clipRoot, Path.GetFileName(trimmed))
			];
			foreach (string candidate in candidates)
			{
				if (File.Exists(candidate))
					return candidate;
			}

			return Path.Combine(web, trimmed);
		}
	}
}

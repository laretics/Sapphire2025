using SIPSorcery.Media;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;
using Tourmaline26.Logic;

namespace Tourmaline26.Services.PassengerAudio
{
	/// <summary>
	/// INVITE RFC 3261 al destino configurado y envío RTP G.711 (PCMA/PCMU)
	/// a partir del PCM 8 kHz. Una llamada por anuncio; BYE al terminar.
	/// </summary>
	public sealed class SipAnnouncementSink : IAnnouncementSink
	{
		private readonly ILogger<SipAnnouncementSink> mvarLogger;

		public SipAnnouncementSink(ILogger<SipAnnouncementSink> logger)
		{
			mvarLogger = logger;
		}

		public PassengerAudioSinkKind Kind => PassengerAudioSinkKind.Sip;

		public async Task Play(
			ComposedAnnouncement announcement,
			PassengerAudioOptions options,
			CancellationToken cancellationToken)
		{
			SipAudioOptions sip = options.Sip;
			string destination = (sip.Destination ?? string.Empty).Trim();
			if (destination.Length == 0)
				throw new InvalidOperationException("PassengerAudio:Sip:Destination está vacío.");

			WavPcm pcm8k = announcement.Pcm.ToMono8k16();
			int timeoutMs = sip.InviteTimeoutMilliseconds;
			if (timeoutMs < 500)
				timeoutMs = 500;

			bool preferPcma = sip.PreferredCodec.Equals("PCMA", StringComparison.OrdinalIgnoreCase)
				|| sip.PreferredCodec.Equals("alaw", StringComparison.OrdinalIgnoreCase)
				|| sip.PreferredCodec.Equals("A-law", StringComparison.OrdinalIgnoreCase);

			mvarLogger.LogInformation(
				"Megafonía SIP: INVITE {Destination} códec {Codec}, {Duration:g}.",
				destination,
				preferPcma ? "PCMA" : "PCMU",
				pcm8k.Duration);

			SIPTransport? transport = null;
			SIPUserAgent? userAgent = null;
			try
			{
				transport = new SIPTransport();
				if (sip.LocalPort > 0)
				{
					transport.AddSIPChannel(new SIPUDPChannel(
						new System.Net.IPEndPoint(System.Net.IPAddress.Any, sip.LocalPort)));
				}

				userAgent = new SIPUserAgent(transport, outboundProxy: null);

				var extras = new AudioExtrasSource(new AudioEncoder());
				extras.RestrictFormats(format =>
					preferPcma
						? format.Codec == AudioCodecsEnum.PCMA || format.Codec == AudioCodecsEnum.PCMU
						: format.Codec == AudioCodecsEnum.PCMU || format.Codec == AudioCodecsEnum.PCMA);
				extras.SetSource(AudioSourcesEnum.None);
				var media = new VoIPMediaSession(new MediaEndPoints { AudioSource = extras });
				media.AcceptRtpFromAny = true;

				string? username = string.IsNullOrWhiteSpace(sip.Username) ? null : sip.Username;
				string? password = string.IsNullOrWhiteSpace(sip.Password) ? null : sip.Password;
				int ringTimeoutSec = Math.Max(1, (int)Math.Ceiling(timeoutMs / 1000.0));

				Task<bool> callTask = userAgent.Call(destination, username, password, media, ringTimeoutSec);
				bool connected = await callTask.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs + 500), cancellationToken);
				if (!connected)
					throw new InvalidOperationException($"INVITE a {destination} rechazado o sin respuesta.");

				await extras.StartAudio();
				using var pcmStream = new MemoryStream(pcm8k.Pcm, writable: false);
				await extras
					.SendAudioFromStream(pcmStream, AudioSamplingRatesEnum.Rate8KHz)
					.WaitAsync(cancellationToken);

				await Task.Delay(120, cancellationToken);
			}
			finally
			{
				try
				{
					if (userAgent is { IsCallActive: true })
						userAgent.Hangup();
				}
				catch (Exception ex)
				{
					mvarLogger.LogDebug(ex, "BYE SIP.");
				}

				try { transport?.Shutdown(); }
				catch (Exception ex) { mvarLogger.LogDebug(ex, "Shutdown SIP transport."); }
			}
		}
	}
}

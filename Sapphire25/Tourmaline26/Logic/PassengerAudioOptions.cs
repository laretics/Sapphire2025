namespace Tourmaline26.Logic
{
	/// <summary>
	/// Megafonía: compositor PCM + sink (analógico en desarrollo, SIP en el tren).
	/// Sección <c>PassengerAudio</c> de appsettings.
	/// </summary>
	public sealed class PassengerAudioOptions
	{
		/// <summary>None | Analog | Sip.</summary>
		public string Sink { get; set; } = "Analog";

		/// <summary>Raíz de clips, relativa a wwwroot (p. ej. catalog/announce).</summary>
		public string ClipRoot { get; set; } = "catalog/announce";

		/// <summary>Silencio entre clips concatenados.</summary>
		public int GapMilliseconds { get; set; } = 120;

		/// <summary>
		/// El anuncio debe terminar estos segundos antes de la parada.
		/// Lo usará el planner; el compositor ya expone <c>Duration</c>.
		/// </summary>
		public int FinishBeforeStopSeconds { get; set; } = 8;

		/// <summary>Margen extra (INVITE, DAC, relé de prioridad).</summary>
		public int SetupMarginMilliseconds { get; set; } = 250;

		/// <summary>Lista de clips para <c>/api/pa/test</c> y el botón de cabina.</summary>
		public List<string> TestFiles { get; set; } = new();

		public AnalogAudioOptions Analog { get; set; } = new();
		public SipAudioOptions Sip { get; set; } = new();

		public PassengerAudioSinkKind SinkKind
		{
			get
			{
				string raw = (Sink ?? string.Empty).Trim();
				if (raw.Equals("Analog", StringComparison.OrdinalIgnoreCase)
					|| raw.Equals("Analogue", StringComparison.OrdinalIgnoreCase)
					|| raw.Equals("Local", StringComparison.OrdinalIgnoreCase))
					return PassengerAudioSinkKind.Analog;
				if (raw.Equals("Sip", StringComparison.OrdinalIgnoreCase)
					|| raw.Equals("VoIP", StringComparison.OrdinalIgnoreCase))
					return PassengerAudioSinkKind.Sip;
				return PassengerAudioSinkKind.None;
			}
		}
	}

	public enum PassengerAudioSinkKind
	{
		None = 0,
		Analog = 1,
		Sip = 2
	}

	public sealed class AnalogAudioOptions
	{
		/// <summary>Índice NAudio/WaveOut. -1 = dispositivo por defecto del sistema.</summary>
		public int DeviceNumber { get; set; } = -1;

		public int LatencyMilliseconds { get; set; } = 100;
	}

	public sealed class SipAudioOptions
	{
		/// <summary>Destino P2P, p. ej. <c>sip:pa@172.16.20.40:5060</c>.</summary>
		public string Destination { get; set; } = string.Empty;

		/// <summary>Digest en el INVITE. Vacío = sin autenticación.</summary>
		public string Username { get; set; } = string.Empty;

		public string Password { get; set; } = string.Empty;

		/// <summary>PCMA (A-law, Europa) o PCMU (μ-law).</summary>
		public string PreferredCodec { get; set; } = "PCMA";

		public int InviteTimeoutMilliseconds { get; set; } = 2000;

		/// <summary>0 = el stack elige un puerto UDP local.</summary>
		public int LocalPort { get; set; } = 0;
	}
}

using MessagePack.Formatters;
using Org.BouncyCastle.Asn1.Ocsp;
using System.Text.Json;

namespace Tourmaline26.Services.TourmalineExperience
{
	/// <summary>
	/// Cliente de TourmalineExperience
	/// Este cliente conecta con el servidor de imágenes de la simulación
	/// y es capaz de enviar la información necesaria para modificar los
	/// parámetros en tiempo real y para recibir datos sobre la propia
	/// simulación.
	/// </summary>
	public class TourmalineExperienceService
	{
		private readonly HttpClient mvarHttpClient;
		private ILogger<TourmalineExperienceService> mvarLogger;
		private readonly string mvarUrl;

		public TourmalineExperienceService(
			HttpClient httpClient,
			ILogger<TourmalineExperienceService> logger,
			IConfiguration config)
		{
			this.mvarHttpClient = httpClient;
			mvarHttpClient.Timeout = TimeSpan.FromSeconds(2);
			this.mvarLogger = logger;

			mvarUrl = config.GetSection("SystemConfiguration")["TExperienceUrl"] ?? "";
			if(mvarUrl.Length<1)
				mvarLogger.LogError("Url for Tourmaline Experience is missing from appsettings.json");
		}

		/// <summary>
		/// True si hay URL de Experience configurada.
		/// </summary>
		public bool IsConfigured => !string.IsNullOrWhiteSpace(mvarUrl);

		/// <summary>
		/// Inicia la simulación. No lanza: si Experience no responde, devuelve false.
		/// </summary>
		public async Task<bool> Launch(LaunchRequest request)
		{
			if (!IsConfigured)
			{
				mvarLogger.LogDebug("Tourmaline Experience no configurado; Launch omitido.");
				return false;
			}

			try
			{
				using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
				string json = JsonSerializer.Serialize(request);
				using StringContent content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
				string auxWholeUrl = mvarUrl + "/launch";
				mvarLogger.LogDebug("Requesting to {0}", auxWholeUrl);
				HttpResponseMessage auxResponse = await mvarHttpClient.PostAsync(auxWholeUrl, content, cts.Token);
				auxResponse.EnsureSuccessStatusCode();
				return true;
			}
			catch (TaskCanceledException)
			{
				mvarLogger.LogWarning("Tourmaline Experience: timeout en Launch (simulador no disponible).");
				return false;
			}
			catch (HttpRequestException ex)
			{
				mvarLogger.LogWarning(ex, "Tourmaline Experience: sin conexión en Launch.");
				return false;
			}
			catch (Exception ex)
			{
				mvarLogger.LogWarning(ex, "Tourmaline Experience: error en Launch.");
				return false;
			}
		}

		/// <summary>
		/// Detiene la simulación. No lanza: timeout o red caída → false (el HMI sigue).
		/// </summary>
		public async Task<bool> Stop()
		{
			if (!IsConfigured)
			{
				return false;
			}

			try
			{
				using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
				HttpResponseMessage auxResponse = await mvarHttpClient.PostAsync(
					mvarUrl + "/stop",
					content: null,
					cts.Token);
				auxResponse.EnsureSuccessStatusCode();
				return true;
			}
			catch (TaskCanceledException)
			{
				mvarLogger.LogWarning("Tourmaline Experience: timeout en Stop (simulador no disponible).");
				return false;
			}
			catch (HttpRequestException ex)
			{
				mvarLogger.LogWarning(ex, "Tourmaline Experience: sin conexión en Stop.");
				return false;
			}
			catch (Exception ex)
			{
				mvarLogger.LogWarning(ex, "Tourmaline Experience: error en Stop.");
				return false;
			}
		}

		public async Task<TourmalineTelemetryResponse> GetTelemetry()
		{
			TourmalineCommand parentCommand = new TourmalineCommand();
			TourmalineTrainCommand command = new TourmalineTrainCommand();
			parentCommand.Type = TourmalineCommandType.Simulation;
			parentCommand.Data = command;
			return await PostCommand(parentCommand);
		}

		/// <summary>
		/// Cambia la velocidad del tren
		/// </summary>
		/// <param name="speed">Nueva Velocidad</param>
		/// <returns>Info de Telemetría</returns>
		public async Task<TourmalineTelemetryResponse?> SetSpeed(int speed)
		{
			TourmalineCommand parentCommand = new TourmalineCommand();
			TourmalineTrainCommand command = new TourmalineTrainCommand();
			parentCommand.Type = TourmalineCommandType.Simulation;
			parentCommand.Data = command;
			command.objectiveSpeed = speed;
			try
			{
                return await PostCommand(parentCommand);
            }
			catch
			{
				return null;
			}			
		}

		public async Task<TourmalineTelemetryResponse?> SetCamera(TourmalineCameraOrder order, bool side, bool orbit = false)
		{
			TourmalineCommand parentCommand = new TourmalineCommand();
			TourmalineCameraCommand command = new TourmalineCameraCommand();
			parentCommand.Type = TourmalineCommandType.Camera;
			parentCommand.Data = command;
			command.Order = order;
			command.Side = side;
			command.Orbit = orbit;
			return await PostCommand(parentCommand);
		}
		public enum SampleWeatherType:byte
		{
			Sunny=0,
			LightCloudy=1,
			ModerateCloudy=2,
			Overcast=3,
			LightRain=4,
			ModerateRain=5,
			HeavyRain=6,
			Snow=7,
			Foggy=8
		}
		public async Task<TourmalineTelemetryResponse?> SetWeather(SampleWeatherType weather)
		{
			TourmalineCommand parentCommand = new TourmalineCommand();
			TourmalineWeatherCommand command = new TourmalineWeatherCommand();
			parentCommand.Type = TourmalineCommandType.Weather;
			parentCommand.Data = command;
			switch (weather)
			{
				case SampleWeatherType.LightCloudy: //Ligeramente nublado
					command.clouds = 0.2f;
					command.visibility = 10000;
					command.precipitation = 0.0f;
					command.liquidity = 1.0f;
					break;
				case SampleWeatherType.ModerateCloudy: //Nublado
					command.clouds = 0.5f;
					command.visibility = 8000;
					command.precipitation = 0.0f;
					command.liquidity = 1.0f;
					break;
				case SampleWeatherType.Overcast: //Cubierto
					command.clouds = 0.9f;
					command.visibility = 6000;
					command.precipitation = 0.0f;
					command.liquidity = 1.0f;
					break;
				case SampleWeatherType.LightRain: //Lluvia ligera
					command.clouds = 0.6f;
					command.visibility = 6000;
					command.precipitation = 0.1f;
					command.liquidity = 1.0f;
					break;
				case SampleWeatherType.ModerateRain: //Lluvia
					command.clouds = 1.0f;
					command.visibility = 5000;
					command.precipitation = 0.4f;
					command.liquidity = 1.0f;
					break;
				case SampleWeatherType.HeavyRain: //Aguacero
					command.clouds = 1.0f;
					command.visibility = 3000;
					command.precipitation = 0.7f;
					command.liquidity = 1.0f;
					break;
				case SampleWeatherType.Snow: //Nevada
					command.clouds = 1.0f;
					command.visibility = 2000;
					command.precipitation = 0.5f;
					command.liquidity = 0.5f;
					break;
				case SampleWeatherType.Foggy: //Niebla
					command.clouds = 1.0f;
					command.visibility = 400;
					command.precipitation = 0.0f;
					command.liquidity = 0.0f;
					break;
				default: //Soleado
					command.clouds = 0.0f;
					command.visibility = 10000;
					command.precipitation = 0.0f;
					command.liquidity= 1.0f;
					break;
			}
			return await PostCommand(parentCommand);
		}

		private async Task<TourmalineTelemetryResponse?> PostCommand(TourmalineCommand cmd)
		{
			try
			{
				using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
				string json = JsonSerializer.Serialize(cmd);
				using StringContent content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
				string auxWholeUrl = mvarUrl + "/command";
				mvarLogger.LogDebug("Requesting to {0}", auxWholeUrl);
				HttpResponseMessage? auxResponse = await mvarHttpClient.PostAsync(auxWholeUrl, content, cts.Token);
				auxResponse.EnsureSuccessStatusCode();
				using (var stream = await auxResponse.Content.ReadAsStreamAsync())
				{
                    TourmalineResponse? auxObject = await JsonSerializer.DeserializeAsync<TourmalineResponse>(stream);
                    if (auxObject != null && !string.IsNullOrEmpty(auxObject.response))
                    {
                        var salida = JsonSerializer.Deserialize<TourmalineTelemetryResponse>(auxObject.response);
                        return salida;
                    }
                    return null;
                }
			}
			catch (TaskCanceledException)
			{
				mvarLogger.LogError("Tourmaline Experience Timeout");
				throw new TimeoutException("Timeout while trying to launch simulation process");
			}
			catch (Exception ex)
			{
				mvarLogger.LogError("Tourmaline Experience Error: {0}", ex.Message);
				throw;
			}
		}

	}
}

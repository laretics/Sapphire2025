namespace Tourmaline26.Components.Services
{
	using Microsoft.AspNetCore.Components;
	using System.Text.Json;
	using Tourmaline26.Logic;

	public class MediaMTXService : IHostedService, IDisposable
    {        
        private readonly ILogger<MediaMTXService> mvarLogger;
        private readonly IConfiguration _configuration;
        private readonly TourmalineService mvarTourmalineService;
        private IHttpClientFactory mvarHttpClientFactory;
        private HttpClient mvarCliente;

        public MediaMTXService(IHttpClientFactory httpClientFactory, ILogger<MediaMTXService> logger, IConfiguration configuration, TourmalineService tourmaline)
        {
            mvarHttpClientFactory = httpClientFactory;
			_configuration = configuration;
			mvarTourmalineService = tourmaline;
			mvarLogger = logger;
		}

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            //await InitCameras(cancellationToken);
            mvarCliente = mvarHttpClientFactory.CreateClient("CameraService");
        }

        private async Task<bool> InitCameras(CancellationToken cancellationToken)
        {
			int cuenta = mvarTourmalineService.SystemConfig.Cameras.Count;
			mvarLogger.LogInformation("Eliminando cámaras configuradas en MediaMTX para iniciar nueva configuración...");
			await DeleteAllCamerasAsync(cancellationToken);
			mvarLogger.LogInformation("Iniciando configuración de {0} cámaras en MediaMTX...",
				cuenta);

			// Esperar un poco a que MediaMTX esté completamente levantado
			await Task.Delay(3000, cancellationToken);
			cuenta = 0;
			foreach (CameraInfo auxCamera in mvarTourmalineService.SystemConfig.Cameras)
			{
				if (await AddCameraAsync(auxCamera, cancellationToken)) cuenta++;
			}
			mvarLogger.LogInformation("Configuración de {0} cámaras finalizada.", cuenta);
            return cuenta==mvarTourmalineService.SystemConfig.Cameras.Count;
		}
        /// <summary>
        /// Añade una cámara a la api de MediaMTX
        /// </summary>
        /// <param name="cam"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<bool> AddCameraAsync(CameraInfo cam, CancellationToken cancellationToken)
        {
            int maxRetries = 5;
            int delayMs = 2000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var payload = new
                    {
                        source = string.Format("rtsp://{0}:554/v2", cam.Address.ToString()),
                        sourceOnDemand = true,                    // bool está bien
                        rtspTransport = "tcp",                    // ← debe ser string
                        sourceOnDemandStartTimeout = "15s",       // ← como string con "s"
                        sourceOnDemandCloseAfter = "30s"          // ← como string con "s"
                    };

                    var response = await mvarCliente.PostAsJsonAsync(
                        $"http://127.0.0.1:9997/v3/config/paths/add/cc{cam.Id}",
                        payload,
                        cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        mvarLogger.LogInformation("Cámara añadida correctamente: {Name} en coche {Coach}", 
                            cam.Name,cam.CoachId);
                        return true;
                    }
                    else
                    {
                        var error = await response.Content.ReadAsStringAsync(cancellationToken);
                        mvarLogger.LogWarning("Error al añadir {Name} en coche {Coach}: {Status} - {Error}",
                            cam.Name, cam.CoachId, response.StatusCode, error);
                    }
                }
                catch (Exception ex)
                {
                    mvarLogger.LogWarning("Intento {Attempt}/{Max} fallido para {Name} en coche {Coach}: {Message}",
                        attempt, maxRetries, cam.Name,cam.CoachId, ex.Message);
                }

                if (attempt < maxRetries)
                    await Task.Delay(delayMs, cancellationToken);
            }
            mvarLogger.LogError("No se pudo añadir la cámara {Name} en coche {Coach} después de {Max} intentos", 
                cam.Name,cam.CoachId, maxRetries);
            return false;
        }
        /// <summary>
        /// Elimina todas las cámaras de la api de MediaMTX
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task DeleteAllCamerasAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                mvarLogger.LogInformation("Eliminando todas las cámaras configuradas...");

                var response = await mvarCliente.GetAsync("http://127.0.0.1:9997/v3/config/paths/list", cancellationToken);
                if (!response.IsSuccessStatusCode) return;

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var root = JsonDocument.Parse(json).RootElement;
                var items = root.GetProperty("items");

                int count = 0;
                foreach (var item in items.EnumerateArray())
                {
                    string name = item.GetProperty("name").GetString() ?? "";
                    if (string.IsNullOrEmpty(name) || name == "all_others") continue;

                    await DeleteCameraAsync(name, cancellationToken);
                    count++;
                }

                mvarLogger.LogInformation("Se eliminaron {Count} cámaras.", count);
            }
            catch (Exception ex)
            {
                mvarLogger.LogError(ex, "Error al eliminar todas las cámaras");
            }
        }
        /// <summary>
        /// Elimina una cámara concreta de la api de MediaMTX
        /// </summary>
        /// <param name="cameraName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task DeleteCameraAsync(string cameraName, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await mvarCliente.DeleteAsync(
                    $"http://127.0.0.1:9997/v3/config/paths/delete/{cameraName}",
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                    mvarLogger.LogInformation("Cámara eliminada: {Name}", cameraName);
                else
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                    mvarLogger.LogWarning("Error eliminando {Name}: {Error}", cameraName, error);
                }
            }
            catch (Exception ex)
            {
                mvarLogger.LogError(ex, "Excepción al eliminar cámara {Name}", cameraName);
            }
        }
        /// <summary>
        /// Verifica si MediaMTX está activo y su API es accesible
        /// </summary>
        public async Task<bool> IsMediaMTXRunningAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Intentamos obtener la lista de paths (la API más básica)
                var response = await mvarCliente.GetAsync(
                    "http://127.0.0.1:9997/v3/config/paths/list",
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    mvarLogger.LogInformation("✅ MediaMTX está activo y API respondiendo correctamente");
                    return true;
                }
                else
                {
                    mvarLogger.LogWarning("MediaMTX respondió pero con código: {Status}", response.StatusCode);
                    return false;
                }
            }
            catch (HttpRequestException)
            {
                mvarLogger.LogWarning("❌ No se puede conectar con MediaMTX (API en puerto 9997). ¿Está el servicio corriendo?");
                return false;
            }
            catch (Exception ex)
            {
                mvarLogger.LogError(ex, "Error inesperado al verificar estado de MediaMTX");
                return false;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            mvarLogger.LogInformation("Deteniendo servicio de configuración de MediaMTX");
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }
}

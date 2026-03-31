namespace Tourmaline26.Components.Services
{
    using Microsoft.AspNetCore.Components;
    using System.Text.Json;
    using Tourmaline26.Components.Services.Logic;

    public class MediaMTXService : IHostedService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<MediaMTXService> mvarLogger;
        private readonly IConfiguration _configuration;
        private readonly TourmalineService mvarTourmalineService;

        public MediaMTXService(HttpClient httpClient, ILogger<MediaMTXService> logger, IConfiguration configuration, TourmalineService tourmaline)
        {
            _httpClient = httpClient;
            mvarLogger = logger;
            _configuration = configuration;
            mvarTourmalineService = tourmaline;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            int auxContador = 0;
            mvarLogger.LogInformation("Iniciando configuración de {0} cámaras en MediaMTX...",
                mvarTourmalineService.SystemConfig.Cameras.Count);

            // Esperar un poco a que MediaMTX esté completamente levantado
            await Task.Delay(3000, cancellationToken);
            foreach (CameraInfo auxCamera in mvarTourmalineService.SystemConfig.Cameras)
                await AddCameraAsync(auxCamera, cancellationToken);
            mvarLogger.LogInformation("Configuración de {0} cámaras finalizada.",auxContador);
        }

        private async Task AddCameraAsync(CameraInfo cam, CancellationToken cancellationToken)
        {
            int maxRetries = 5;
            int delayMs = 2000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var payload = new
                    {
                        source = string.Format("rtsp://{0}:554/v2", cam.Address),
                        sourceOnDemand = true,                    // bool está bien
                        rtspTransport = "tcp",                    // ← debe ser string
                        sourceOnDemandStartTimeout = "15s",       // ← como string con "s"
                        sourceOnDemandCloseAfter = "30s"          // ← como string con "s"
                    };

                    var response = await _httpClient.PostAsJsonAsync(
                        $"http://127.0.0.1:9997/v3/config/paths/add/cc{cam.Id}",
                        payload,
                        cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        mvarLogger.LogInformation("Cámara añadida correctamente: {Name} en coche {Coach}", 
                            cam.Name,cam.CoachId);
                        return;
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
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            mvarLogger.LogInformation("Deteniendo servicio de configuración de MediaMTX");
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }
}

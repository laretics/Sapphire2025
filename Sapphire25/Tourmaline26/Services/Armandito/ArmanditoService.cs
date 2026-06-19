using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tourmaline26.Services.Armandito
{
    public class ArmanditoService
    {
        private readonly HttpClient mvarHttpClient;
        private readonly ILogger<ArmanditoService> mvarLogger;
        private readonly string mvarToken;

        private static readonly JsonSerializerOptions mvarJsonOptions = new()
        {PropertyNameCaseInsensitive = true};
        public ArmanditoService(HttpClient client, 
            ILogger<ArmanditoService> logger,
            IConfiguration config)
        {
            mvarHttpClient = client;
            mvarHttpClient.Timeout = TimeSpan.FromSeconds(10);
            mvarLogger = logger;
            mvarToken = config["SystemConfiguration:SfmInfoToken"] ?? "SFM2026";
            
            if(null==mvarHttpClient.BaseAddress)
            {
                string baseUrl = config["SystemConfiguration:SfmInfoUrl"] ?? "https://info.trensfm.com:8084";
                mvarHttpClient.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
            }
        }

        public async Task<IReadOnlyList<ArmanditoMessage>> GetMessagesAsync
            (string TrainId, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(TrainId))
                throw new ArgumentException("Circulation service name must not be empty.", nameof(TrainId));
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"api/servicio_info?nombre_servicio={Uri.EscapeDataString(TrainId)}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", mvarToken);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                using HttpResponseMessage response = await mvarHttpClient.SendAsync(request, cancellationToken);
                if(System.Net.HttpStatusCode.Unauthorized == response.StatusCode)
                {
                    mvarLogger.LogWarning("Forbidden access to SFM realtime information for {Servicio}.", TrainId);
                    return Array.Empty<ArmanditoMessage>();
                }
                
                if(!response.IsSuccessStatusCode)
                {
                    string errorText = await response.Content.ReadAsStringAsync(cancellationToken);
                    mvarLogger.LogWarning("Error trying to pool SFM for {Servicio}: {Status} - {Error}",
                        TrainId, response.StatusCode, errorText);
                    return Array.Empty<ArmanditoMessage>();
                }

                IReadOnlyList<ArmanditoMessage>? messages = await response.Content.ReadFromJsonAsync<List<ArmanditoMessage>>(mvarJsonOptions, cancellationToken);
                return messages ?? Array.Empty<ArmanditoMessage>();
            }
            catch(TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                mvarLogger.LogError(ex, "Timeout pooling external incidence info for {Servicio}.", TrainId);
                throw new TimeoutException("Timeout checking SFM realtime information API.", ex);
            }
        }


    }

    public sealed record ArmanditoMessage
    {
        [JsonPropertyName("codigo")]
        public int Codigo { get; init; }
        [JsonPropertyName("prioridad")]
        public int Prioridad { get;init; }
        [JsonPropertyName("texto")]
        public List<ArmanditoText> Texto { get; init; } = [];
    }
    public sealed record ArmanditoText
    {
        [JsonPropertyName("idioma")]
        public int Idioma { get; init; }
        [JsonPropertyName("texto")]
        public string Texto { get; init; } = string.Empty;
    }

}

using Microsoft.EntityFrameworkCore.Storage.Json;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using Tourmaline26.Logic;
namespace Tourmaline26.Services
{
    public class MVBService
    {
        private readonly HttpClient mvarHttpClient;
        private ILogger<MVBService> mvarLogger;
        private readonly string mvarUrl;
        private int DEFAULT_MAX_RETRIES => 5;
        private int mvarRetries; //Intentos hasta deshabilitar MVB.
        private readonly int mvarMaxRetries; //Intentos máximos hasta deshabilitar MVB.
        

        public MVBService(
            HttpClient mvarHttpClient, 
            ILogger<MVBService> logger,
            IConfiguration config)
        {
            this.mvarHttpClient = mvarHttpClient;
            mvarHttpClient.Timeout = TimeSpan.FromSeconds(2); // Set a timeout for the HTTP client
            this.mvarLogger = logger;
            mvarUrl = config.GetSection("SystemConfiguration")["MVBUrl"] ?? string.Empty;
            if (mvarUrl.Length < 1)
                mvarLogger.LogError("MVB parameter missing in configuration");
            string auxNum = config.GetSection("SystemConfiguration")["MVBRetries"] ?? DEFAULT_MAX_RETRIES.ToString();
            if (!int.TryParse(auxNum, out mvarMaxRetries))
                mvarMaxRetries = DEFAULT_MAX_RETRIES;
        }
        public bool IsOK { get => mvarUrl.Length > 0 && mvarRetries>0; }
        public async Task<MVB8100Data?> GetMVBDataAsync()
        {
            if (mvarRetries < 1) 
                return null;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                var response = await mvarHttpClient.GetAsync(mvarUrl,cts.Token);
                response.EnsureSuccessStatusCode();
                var jsonString = await response.Content.ReadAsStringAsync();
                mvarLogger.LogInformation("MVB data read {0}", response.StatusCode);
                return JsonSerializer.Deserialize<MVB8100Data>(jsonString);
            }
            catch (TaskCanceledException ex)
            {                
                mvarRetries--;
                mvarLogger.LogError($"MVB error: Timeout. {mvarRetries} retries.");
                if (mvarRetries < 1)
                    mvarLogger.LogError("MVB intents exceeded");
                throw new TimeoutException("Timeout while trying to get MVB data");
            }
            catch (Exception ex)
            {
                mvarLogger.LogError("MVB error: {0}", ex.Message);
            }
            return null;
        } 
    }
}

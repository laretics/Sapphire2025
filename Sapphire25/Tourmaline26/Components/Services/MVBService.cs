using Microsoft.EntityFrameworkCore.Storage.Json;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using Tourmaline26.Logic;
namespace Tourmaline26.Components.Services
{
    public class MVBService
    {
        private readonly HttpClient mvarHttpClient;
        private ILogger<MVBService> mvarLogger;
        private readonly string mvarUrl;

        public MVBService(
            HttpClient mvarHttpClient, 
            ILogger<MVBService> logger,
            IConfiguration config)
        {
            this.mvarHttpClient = mvarHttpClient;
            mvarHttpClient.Timeout = TimeSpan.FromSeconds(2); // Set a timeout for the HTTP client
            this.mvarLogger = logger;
            mvarUrl = config.GetSection("SystemConfiguration")["MVBUrl"]
                ?? throw new InvalidOperationException("MVB parameter missing in configuration");
        }
        public async Task<MVB8100Data?> GetMVBDataAsync()
        {
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
                mvarLogger.LogError("MVB error: Timeout");
                throw new TimeoutException("Timeout while trying to get MVB data");
            }
            catch (Exception ex)
            {
                mvarLogger.LogError("MVB error: {0}", ex.Message);
                throw ex;
            }
        } 
    }
}

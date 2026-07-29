using Microsoft.EntityFrameworkCore.Storage.Json;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using Tourmaline26.Logic;
namespace Tourmaline26.Services
{
    public class MVBService:BackgroundService
    {
        private readonly HttpClient mvarHttpClient;
        private ILogger<MVBService> mvarLogger;
        private readonly string mvarUrl;
        private readonly TimeSpan mvarPollInterval = TimeSpan.FromMilliseconds(100);
        private readonly object mvarLock = new();
        private MVB8100Data? mvarLastData;
        public MVB8100Data? CurrentData
        {
            get { lock (mvarLock) return mvarLastData; }
        }
        private int DEFAULT_MAX_RETRIES => 5;
        private int mvarRetries=0; //Intentos hasta deshabilitar MVB.

        private readonly int mvarMaxRetries; //Intentos máximos hasta deshabilitar MVB.
        private DateTime mvarNextAttempt = DateTime.MinValue; //Próximo reintento.

        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using PeriodicTimer auxTimer = new PeriodicTimer(mvarPollInterval);

            while(await auxTimer.WaitForNextTickAsync(stoppingToken))
            {
                if (string.IsNullOrWhiteSpace(mvarUrl)) continue;
                if (DateTime.UtcNow < mvarNextAttempt) continue;
                try
                {
                    using HttpResponseMessage response = await mvarHttpClient.GetAsync(
                        mvarUrl,
                        HttpCompletionOption.ResponseHeadersRead,
                        stoppingToken);
                    response.EnsureSuccessStatusCode();

                    MVB8100Data? data = await response.Content.ReadFromJsonAsync<MVB8100Data>(cancellationToken: stoppingToken);
                    if(null!=data)
                    {
                        lock (mvarLock)
                        {
                            mvarLastData = data;
                            mvarRetries = 0;
                            mvarNextAttempt = DateTime.MinValue;
                        }
                    }                    
                }
                catch(OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    RegisterFailure(ex);
                    
                }
            }
        }

        private void RegisterFailure(Exception ex)
        {
            int failures;
            TimeSpan backoff;

            lock (mvarLock)
            {
                mvarRetries++;
                failures = mvarRetries;
                backoff = GetBackoff(failures);
                mvarNextAttempt = DateTime.UtcNow.Add(backoff);
            }
            //El logging se limita. Ponemos el primer fallo y luego cada 10.
            if (1==failures || 0 == failures % 10)
                mvarLogger.LogWarning(ex, $"MVB poll failed: {ex.Message}. Retry in {backoff}. Consecutive failures: {failures}");
        }

        private TimeSpan GetBackoff(int failures)
        {
            int exponent = Math.Min(failures - 1, 7);
            int milliseconds = Math.Min(30000, (int)(250 * Math.Pow(2, exponent)));
            return TimeSpan.FromMilliseconds(milliseconds);
        }

        public MVBService(
            HttpClient httpClient, 
            ILogger<MVBService> logger,
            IConfiguration config)
        {
            mvarHttpClient = httpClient;
            mvarLogger = logger;
            mvarHttpClient.Timeout = TimeSpan.FromSeconds(2);
            mvarUrl = config.GetSection("SystemConfiguration")["MVBUrl"] ?? string.Empty;
            if (mvarUrl.Length < 1)
                mvarLogger.LogError("MVB parameter missing in configuration");            

            string auxNum = config.GetSection("SystemConfiguration")["MVBRetries"] ?? DEFAULT_MAX_RETRIES.ToString();
            if (!int.TryParse(auxNum, out mvarMaxRetries))
                mvarMaxRetries = DEFAULT_MAX_RETRIES;

        //    ResetRetries();
        }
        //public bool IsOK { get => mvarUrl.Length > 0 && mvarRetries>0; }
        public void ResetRetries() 
        { 
            lock(mvarLock)
            {
                mvarRetries = 0;
                mvarNextAttempt = DateTime.MinValue;
            }
            mvarRetries = mvarMaxRetries; 
        }
        //public async Task<MVB8100Data?> GetMVBDataAsync()
        //{
        //    if (mvarRetries < 1) 
        //        return null;
        //    try
        //    {
        //        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        //        var response = await mvarHttpClient.GetAsync(mvarUrl,cts.Token);
        //        response.EnsureSuccessStatusCode();
        //        var jsonString = await response.Content.ReadAsStringAsync();
        //        mvarLogger.LogInformation("MVB data read {0}", response.StatusCode);
        //        return JsonSerializer.Deserialize<MVB8100Data>(jsonString);
        //    }
        //    catch (TaskCanceledException ex)
        //    {                
        //        mvarRetries--;
        //        mvarLogger.LogError($"MVB error: Timeout. {mvarRetries} retries.");
        //        if (mvarRetries < 1)
        //            mvarLogger.LogError("MVB intents exceeded");
        //        throw new TimeoutException("Timeout while trying to get MVB data");
        //    }
        //    catch (Exception ex)
        //    {
        //        mvarLogger.LogError("MVB error: {0}", ex.Message);
        //    }
        //    return null;
        //} 
    }
}

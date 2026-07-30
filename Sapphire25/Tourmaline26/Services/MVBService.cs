using System.Net.Http.Json;
using Tourmaline26.Logic;

namespace Tourmaline26.Services
{
    /// <summary>
    /// Polling del endpoint MVB del tren. Debe registrarse como Singleton + HostedService
    /// (ver Program.cs): si solo se usa AddHttpClient&lt;MVBService&gt;, ExecuteAsync nunca arranca
    /// y CurrentData queda siempre null.
    /// </summary>
    public class MVBService : BackgroundService
    {
        private readonly IHttpClientFactory mvarHttpClientFactory;
        private readonly ILogger<MVBService> mvarLogger;
        private readonly string mvarUrl;
        private readonly TimeSpan mvarPollInterval = TimeSpan.FromMilliseconds(100);
        private readonly object mvarLock = new();
        private MVB8100Data? mvarLastData;

        public MVB8100Data? CurrentData
        {
            get { lock (mvarLock) return mvarLastData; }
        }

        private const int DefaultMaxRetries = 5;
        /// <summary>Fallos consecutivos (solo para backoff y logging).</summary>
        private int mvarConsecutiveFailures;
        private readonly int mvarMaxRetries;
        private DateTime mvarNextAttempt = DateTime.MinValue;
        private long mvarSuccessCount;
        private long mvarFailureCount;

        public long SuccessCount => Interlocked.Read(ref mvarSuccessCount);
        public long FailureCount => Interlocked.Read(ref mvarFailureCount);
        public string Url => mvarUrl;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(mvarUrl);

        public MVBService(
            IHttpClientFactory httpClientFactory,
            ILogger<MVBService> logger,
            IConfiguration config)
        {
            mvarHttpClientFactory = httpClientFactory;
            mvarLogger = logger;

            mvarUrl = config.GetSection("SystemConfiguration")["MVBUrl"] ?? string.Empty;
            if (mvarUrl.Length < 1)
                mvarLogger.LogError("MVB parameter missing in configuration");
            else
                mvarLogger.LogInformation("MVB service configured. URL: {Url}", mvarUrl);

            string auxNum = config.GetSection("SystemConfiguration")["MVBRetries"] ?? DefaultMaxRetries.ToString();
            if (!int.TryParse(auxNum, out mvarMaxRetries))
                mvarMaxRetries = DefaultMaxRetries;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (string.IsNullOrWhiteSpace(mvarUrl))
            {
                mvarLogger.LogError("MVB background loop not started: URL empty.");
                return;
            }

            mvarLogger.LogInformation("MVB background loop started.");
            HttpClient httpClient = mvarHttpClientFactory.CreateClient("MVB");

            using PeriodicTimer auxTimer = new PeriodicTimer(mvarPollInterval);

            while (await auxTimer.WaitForNextTickAsync(stoppingToken))
            {
                if (DateTime.UtcNow < mvarNextAttempt)
                    continue;

                try
                {
                    using HttpResponseMessage response = await httpClient.GetAsync(
                        mvarUrl,
                        HttpCompletionOption.ResponseHeadersRead,
                        stoppingToken);
                    response.EnsureSuccessStatusCode();

                    MVB8100Data? data = await response.Content.ReadFromJsonAsync<MVB8100Data>(
                        cancellationToken: stoppingToken);

                    if (null != data)
                    {
                        lock (mvarLock)
                        {
                            mvarLastData = data;
                            mvarConsecutiveFailures = 0;
                            mvarNextAttempt = DateTime.MinValue;
                        }

                        long n = Interlocked.Increment(ref mvarSuccessCount);
                        if (1 == n || 0 == n % 100)
                            mvarLogger.LogDebug("MVB data OK (#{Count}). Speed={Speed}", n, data.current_speed);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    RegisterFailure(ex);
                }
            }

            mvarLogger.LogInformation("MVB background loop stopped.");
        }

        private void RegisterFailure(Exception ex)
        {
            int failures;
            TimeSpan backoff;

            lock (mvarLock)
            {
                mvarConsecutiveFailures++;
                failures = mvarConsecutiveFailures;
                backoff = GetBackoff(failures);
                mvarNextAttempt = DateTime.UtcNow.Add(backoff);
            }

            Interlocked.Increment(ref mvarFailureCount);

            // Primer fallo y luego cada 10 para no saturar el log.
            if (1 == failures || 0 == failures % 10)
                mvarLogger.LogWarning(
                    ex,
                    "MVB poll failed: {Message}. Retry in {Backoff}. Consecutive failures: {Failures}",
                    ex.Message,
                    backoff,
                    failures);
        }

        private static TimeSpan GetBackoff(int failures)
        {
            int exponent = Math.Min(failures - 1, 7);
            int milliseconds = Math.Min(30000, (int)(250 * Math.Pow(2, exponent)));
            return TimeSpan.FromMilliseconds(milliseconds);
        }

        /// <summary>
        /// Reinicia el contador de fallos y permite reintentar de inmediato
        /// (p. ej. al reactivar MVB desde la UI).
        /// </summary>
        public void ResetRetries()
        {
            lock (mvarLock)
            {
                mvarConsecutiveFailures = 0;
                mvarNextAttempt = DateTime.MinValue;
            }
            mvarLogger.LogInformation("MVB retries reset (max configured={Max}).", mvarMaxRetries);
        }
    }
}

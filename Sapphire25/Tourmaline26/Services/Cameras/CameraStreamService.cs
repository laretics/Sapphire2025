using System.Collections.Concurrent;
using System.Net;
using RtspClientSharp;
using RtspClientSharp.RawFrames;
using RtspClientSharp.RawFrames.Video;
using RtspClientSharp.Rtsp;
using Tourmaline26.Logic;

namespace Tourmaline26.Services.Cameras
{
    /// <summary>
    /// Cliente RTSP nativo (sin MediaMTX). Mantiene el último JPEG por cámara
    /// y sirve snapshots / multipart MJPEG al HMI.
    /// </summary>
    public sealed class CameraStreamService : IDisposable
    {
        private readonly ILogger<CameraStreamService> mvarLogger;
        private readonly ConcurrentDictionary<int, CameraSession> mvarSessions = new();
        private readonly Dictionary<int, CameraInfo> mvarCameras = new();
        private bool mvarDisposed;

        public CameraStreamService(
            ILogger<CameraStreamService> logger,
            IConfiguration config,
            TourmalineService tourmaline)
        {
            mvarLogger = logger;

            // Preferir la lista ya cargada en TourmalineService; si aún está vacía, leer appsettings.
            IEnumerable<CameraInfo> cameras = tourmaline.SystemConfig.Cameras;
            if (!cameras.Any())
            {
                List<CameraInfo>? fromRoot = config.GetSection("Cameras").Get<List<CameraInfo>>();
                if (fromRoot is { Count: > 0 })
                    cameras = fromRoot;
                else
                {
                    List<CameraInfo>? fromSys = config.GetSection("SystemConfiguration:Cameras").Get<List<CameraInfo>>();
                    if (fromSys is { Count: > 0 })
                        cameras = fromSys;
                }
            }

            foreach (CameraInfo cam in cameras)
            {
                if (cam.Id < 0) continue;
                // appsettings a veces repite el mismo Id (p.ej. plantilla 8100 + IP de prueba).
                if (!mvarCameras.TryAdd(cam.Id, cam))
                {
                    mvarLogger.LogWarning(
                        "Cámara duplicada Id={Id} ({Name} @ {Address}); se mantiene la primera.",
                        cam.Id, cam.Name, cam.Address);
                }
            }

            mvarLogger.LogInformation(
                "CameraStreamService: {Count} cámara(s) configurada(s).",
                mvarCameras.Count);
        }

        public IReadOnlyCollection<CameraInfo> Cameras => mvarCameras.Values.ToList();

        public bool TryGetCamera(int id, out CameraInfo? camera) =>
            mvarCameras.TryGetValue(id, out camera);

        /// <summary>Último JPEG conocido (copia). Null si aún no hay frames.</summary>
        public byte[]? TryGetLatestJpeg(int cameraId)
        {
            if (!mvarSessions.TryGetValue(cameraId, out CameraSession? session))
                return null;
            return session.GetLatestJpegCopy();
        }

        public DateTime? GetLastFrameUtc(int cameraId)
        {
            if (!mvarSessions.TryGetValue(cameraId, out CameraSession? session))
                return null;
            return session.LastFrameUtc;
        }

        /// <summary>
        /// Corta el cliente RTSP y arranca otro. El visor HMI lo usa cuando
        /// el &lt;img&gt; se queda congelado sin disparar onerror.
        /// </summary>
        public void ForceRestart(int cameraId)
        {
            if (!mvarCameras.TryGetValue(cameraId, out CameraInfo? cam) || cam is null)
                return;
            CameraSession session = mvarSessions.GetOrAdd(cameraId, _ => new CameraSession(cam, mvarLogger));
            session.ForceRestart();
        }

        /// <summary>
        /// Asegura que hay un cliente RTSP corriendo para la cámara y escribe
        /// un stream multipart/x-mixed-replace al response.
        /// </summary>
        public async Task WriteMjpegAsync(int cameraId, HttpResponse response, CancellationToken token)
        {
            if (!mvarCameras.TryGetValue(cameraId, out CameraInfo? cam) || cam is null)
            {
                response.StatusCode = StatusCodes.Status404NotFound;
                await response.WriteAsync($"Camera {cameraId} not found.", token);
                return;
            }

            CameraSession session = mvarSessions.GetOrAdd(cameraId, id => new CameraSession(cam, mvarLogger));
            session.AddViewer();
            try
            {
                session.EnsureStarted();

                // Esperar un JPEG reciente (no el último congelado de una sesión muerta).
                byte[]? first = null;
                DateTime deadline = DateTime.UtcNow.AddSeconds(8);
                while (first is null && DateTime.UtcNow < deadline && !token.IsCancellationRequested)
                {
                    first = session.GetFreshJpegCopy(TimeSpan.FromSeconds(8));
                    if (first is null)
                        await Task.Delay(100, token);
                }

                if (first is null)
                {
                    response.StatusCode = StatusCodes.Status504GatewayTimeout;
                    await response.WriteAsync($"No frames from camera {cameraId} ({session.LastError ?? "timeout"}).", token);
                    return;
                }

                string boundary = "tourmalineframe";
                response.ContentType = $"multipart/x-mixed-replace; boundary={boundary}";
                response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                response.Headers.Pragma = "no-cache";
                // Evita buffering del proxy/Kestrel en streams largos.
                response.Headers["X-Accel-Buffering"] = "no";

                await response.StartAsync(token);
                Stream body = response.Body;

                byte[]? lastSent = null;
                // Si el RTSP se queda mudo pero deja el último JPEG, el <img> no dispara
                // onerror y el HMI parece "congelado". Cortamos el HTTP para forzar reintento.
                const int mjpegStaleSeconds = 20;
                while (!token.IsCancellationRequested)
                {
                    DateTime? frameUtc = session.LastFrameUtc;
                    if (frameUtc.HasValue
                        && (DateTime.UtcNow - frameUtc.Value).TotalSeconds > mjpegStaleSeconds)
                    {
                        mvarLogger.LogWarning(
                            "MJPEG stale camera {Id}: sin frames nuevos > {Sec}s; abortando HTTP y forzando RTSP",
                            cameraId, mjpegStaleSeconds);
                        session.ForceRestart();
                        try { response.HttpContext.Abort(); }
                        catch { /* ignore */ }
                        break;
                    }

                    byte[]? jpeg = session.GetLatestJpegCopy();
                    if (jpeg is null || (lastSent is not null && jpeg.AsSpan().SequenceEqual(lastSent)))
                    {
                        await Task.Delay(40, token);
                        continue;
                    }

                    lastSent = jpeg;
                    await WriteMultipartPartAsync(body, boundary, jpeg, token);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Cliente cerró el <img> / pestaña.
            }
            finally
            {
                session.RemoveViewer();
            }
        }

        public async Task WriteSnapshotAsync(int cameraId, HttpResponse response, CancellationToken token)
        {
            if (!mvarCameras.TryGetValue(cameraId, out CameraInfo? cam) || cam is null)
            {
                response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            CameraSession session = mvarSessions.GetOrAdd(cameraId, id => new CameraSession(cam, mvarLogger));
            session.AddViewer();
            try
            {
                session.EnsureStarted();

                byte[]? jpeg = null;
                DateTime deadline = DateTime.UtcNow.AddSeconds(8);
                while (jpeg is null && DateTime.UtcNow < deadline && !token.IsCancellationRequested)
                {
                    jpeg = session.GetLatestJpegCopy();
                    if (jpeg is null)
                        await Task.Delay(100, token);
                }

                if (jpeg is null)
                {
                    response.StatusCode = StatusCodes.Status504GatewayTimeout;
                    await response.WriteAsync(session.LastError ?? "No frame", token);
                    return;
                }

                response.ContentType = "image/jpeg";
                response.Headers.CacheControl = "no-cache, no-store";
                await response.Body.WriteAsync(jpeg, token);
            }
            finally
            {
                session.RemoveViewer();
            }
        }

        private static async Task WriteMultipartPartAsync(
            Stream body,
            string boundary,
            byte[] jpeg,
            CancellationToken token)
        {
            // Cabecera de parte
            string header =
                $"--{boundary}\r\n" +
                "Content-Type: image/jpeg\r\n" +
                $"Content-Length: {jpeg.Length}\r\n" +
                "\r\n";
            byte[] headerBytes = System.Text.Encoding.ASCII.GetBytes(header);
            await body.WriteAsync(headerBytes, token);
            await body.WriteAsync(jpeg, token);
            await body.WriteAsync("\r\n"u8.ToArray(), token);
            await body.FlushAsync(token);
        }

        public void Dispose()
        {
            if (mvarDisposed) return;
            mvarDisposed = true;
            foreach (CameraSession s in mvarSessions.Values)
                s.Dispose();
            mvarSessions.Clear();
        }

        private sealed class CameraSession : IDisposable
        {
            /// <summary>
            /// Sin JPEG nuevo en este tiempo → se considera el RTSP muerto aunque el socket
            /// siga abierto (caso típico de congelado sin reintento).
            /// </summary>
            private const int FrameStaleTimeoutSeconds = 12;
            private const int MaxBackoffMs = 10000;

            private readonly CameraInfo mvarCam;
            private readonly ILogger mvarLogger;
            private readonly object mvarLock = new();
            private readonly string mvarRtspUrl;

            private byte[]? mvarLatestJpeg;
            private int mvarViewers;
            private CancellationTokenSource? mvarCts;
            private Task? mvarLoop;
            private bool mvarDisposed;

            public DateTime? LastFrameUtc { get; private set; }
            public string? LastError { get; private set; }
            public long FramesReceived { get; private set; }

            public CameraSession(CameraInfo cam, ILogger logger)
            {
                mvarCam = cam;
                mvarLogger = logger;
                mvarRtspUrl = cam.BuildRtspUrl();
            }

            public void AddViewer() => Interlocked.Increment(ref mvarViewers);

            public void RemoveViewer()
            {
                int left = Interlocked.Decrement(ref mvarViewers);
                if (left <= 0)
                {
                    // Parar tras un margen: el HMI puede reabrir el <img> al re-render.
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(15000);
                        if (Volatile.Read(ref mvarViewers) <= 0)
                            Stop();
                    });
                }
            }

            public byte[]? GetLatestJpegCopy()
            {
                lock (mvarLock)
                {
                    if (mvarLatestJpeg is null) return null;
                    return (byte[])mvarLatestJpeg.Clone();
                }
            }

            public byte[]? GetFreshJpegCopy(TimeSpan maxAge)
            {
                lock (mvarLock)
                {
                    if (mvarLatestJpeg is null || LastFrameUtc is null)
                        return null;
                    if (DateTime.UtcNow - LastFrameUtc.Value > maxAge)
                        return null;
                    return (byte[])mvarLatestJpeg.Clone();
                }
            }

            public bool IsFrameStale(TimeSpan maxAge)
            {
                DateTime? last = LastFrameUtc;
                if (!last.HasValue)
                    return false;
                return DateTime.UtcNow - last.Value > maxAge;
            }

            public void EnsureStarted()
            {
                if (mvarDisposed) return;

                lock (mvarLock)
                {
                    if (mvarDisposed) return;

                    bool loopRunning = mvarLoop is { IsCompleted: false }
                        && mvarCts is { IsCancellationRequested: false };
                    bool stale = IsFrameStale(TimeSpan.FromSeconds(FrameStaleTimeoutSeconds * 2));
                    if (loopRunning && !stale)
                        return;

                    StartLoopLocked(stale ? "stale-restart" : "start");
                }
            }

            public void ForceRestart()
            {
                if (mvarDisposed) return;
                lock (mvarLock)
                {
                    if (mvarDisposed) return;
                    StartLoopLocked("force-restart");
                }
            }

            private void StartLoopLocked(string reason)
            {
                try { mvarCts?.Cancel(); } catch { /* ignore */ }

                mvarCts = new CancellationTokenSource();
                CancellationToken token = mvarCts.Token;
                mvarLoop = Task.Run(() => ReceiveLoopAsync(token));
                mvarLogger.LogInformation(
                    "RTSP {Reason} camera {Id} ({Name}) → {Url}",
                    reason, mvarCam.Id, mvarCam.Name, SanitizeUrl(mvarRtspUrl));
            }

            private void Stop()
            {
                lock (mvarLock)
                {
                    try { mvarCts?.Cancel(); } catch { /* ignore */ }
                    // No anular mvarLoop aquí: EnsureStarted mira IsCompleted y evita
                    // un segundo ReceiveLoop mientras el anterior aún se cierra.
                    // Tampoco se pone mvarLoop = null (antes dejaba carrera al reabrir el <img>).
                }
            }

            private async Task ReceiveLoopAsync(CancellationToken token)
            {
                int backoffMs = 400;
                while (!token.IsCancellationRequested)
                {
                    CancellationTokenSource? sessionCts = null;
                    RtspClient? client = null;
                    bool staleReconnect = false;
                    try
                    {
                        Uri uri = new(mvarRtspUrl);
                        ConnectionParameters parameters = new(uri)
                        {
                            RtpTransport = RtpTransportProtocol.TCP,
                            RequiredTracks = RequiredTracks.Video,
                            ConnectTimeout = TimeSpan.FromSeconds(5),
                            ReceiveTimeout = TimeSpan.FromSeconds(8)
                        };

                        sessionCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                        client = new RtspClient(parameters);
                        client.FrameReceived += OnFrameReceived;

                        await client.ConnectAsync(sessionCts.Token);
                        LastError = null;
                        backoffMs = 400;
                        DateTime connectedUtc = DateTime.UtcNow;
                        mvarLogger.LogInformation(
                            "RTSP connected camera {Id} ({Name})",
                            mvarCam.Id, mvarCam.Name);

                        Task receiveTask = client.ReceiveAsync(sessionCts.Token);
                        while (!receiveTask.IsCompleted && !token.IsCancellationRequested)
                        {
                            await Task.WhenAny(receiveTask, Task.Delay(1000, token));
                            if (receiveTask.IsCompleted)
                                break;

                            DateTime? last = LastFrameUtc;
                            bool gotFrameThisSession = last.HasValue && last.Value >= connectedUtc;
                            TimeSpan silence = gotFrameThisSession
                                ? DateTime.UtcNow - last!.Value
                                : DateTime.UtcNow - connectedUtc;

                            if (silence.TotalSeconds >= FrameStaleTimeoutSeconds)
                            {
                                LastError = gotFrameThisSession
                                    ? $"Sin frames durante {(int)silence.TotalSeconds}s; reconectando"
                                    : "Sin frames tras conectar; reintentando";
                                mvarLogger.LogWarning(
                                    "RTSP stale camera {Id} ({Name}): {Seconds}s sin JPEG nuevo. Reconectando…",
                                    mvarCam.Id, mvarCam.Name, (int)silence.TotalSeconds);
                                staleReconnect = true;
                                try { sessionCts.Cancel(); } catch { /* ignore */ }
                                break;
                            }
                        }

                        await DrainReceiveAsync(receiveTask);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (OperationCanceledException) when (staleReconnect)
                    {
                        // Watchdog: reintento inmediato, sin backoff.
                    }
                    catch (OperationCanceledException ex)
                    {
                        LastError = ex.Message;
                        mvarLogger.LogWarning(
                            "RTSP session cancel camera {Id} ({Name}): {Message}. Reintento en {Ms} ms",
                            mvarCam.Id, mvarCam.Name, ex.Message, backoffMs);
                        if (!await DelayOrStop(backoffMs, token))
                            break;
                        backoffMs = Math.Min(MaxBackoffMs, backoffMs * 2);
                    }
                    catch (Exception ex)
                    {
                        LastError = ex.Message;
                        mvarLogger.LogWarning(
                            "RTSP error camera {Id} ({Name}): {Message}. Reintento en {Ms} ms",
                            mvarCam.Id, mvarCam.Name, ex.Message, backoffMs);
                        if (!await DelayOrStop(backoffMs, token))
                            break;
                        backoffMs = Math.Min(MaxBackoffMs, backoffMs * 2);
                    }
                    finally
                    {
                        if (client is not null)
                        {
                            try { client.FrameReceived -= OnFrameReceived; } catch { /* ignore */ }
                            try { client.Dispose(); } catch { /* ignore */ }
                        }
                        try { sessionCts?.Dispose(); } catch { /* ignore */ }
                    }

                    if (token.IsCancellationRequested)
                        break;

                    if (staleReconnect)
                    {
                        if (!await DelayOrStop(250, token))
                            break;
                    }
                }

                mvarLogger.LogInformation("RTSP stopped camera {Id}", mvarCam.Id);
            }

            private static async Task DrainReceiveAsync(Task receiveTask)
            {
                try
                {
                    await receiveTask.WaitAsync(TimeSpan.FromSeconds(2));
                }
                catch (TimeoutException)
                {
                    // ReceiveAsync ignoró la cancelación; el Dispose del cliente corta el socket.
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception)
                {
                }
            }

            private static async Task<bool> DelayOrStop(int milliseconds, CancellationToken token)
            {
                try
                {
                    await Task.Delay(milliseconds, token);
                    return true;
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }

            private void OnFrameReceived(object? sender, RawFrame frame)
            {
                if (frame is not RawJpegFrame jpeg)
                    return;

                ArraySegment<byte> seg = jpeg.FrameSegment;
                if (seg.Count < 2)
                    return;

                // SOI JPEG: FF D8
                byte b0 = seg.Array![seg.Offset];
                byte b1 = seg.Array![seg.Offset + 1];
                if (b0 != 0xFF || b1 != 0xD8)
                    return;

                byte[] copy = new byte[seg.Count];
                Buffer.BlockCopy(seg.Array!, seg.Offset, copy, 0, seg.Count);

                lock (mvarLock)
                {
                    mvarLatestJpeg = copy;
                    LastFrameUtc = DateTime.UtcNow;
                    FramesReceived++;
                }
            }

            private static string SanitizeUrl(string url)
            {
                // Oculta user:pass@ si los hubiera.
                int scheme = url.IndexOf("://", StringComparison.Ordinal);
                if (scheme < 0) return url;
                int at = url.IndexOf('@');
                if (at < 0) return url;
                return url.Substring(0, scheme + 3) + "***@" + url.Substring(at + 1);
            }

            public void Dispose()
            {
                mvarDisposed = true;
                Stop();
            }
        }
    }
}

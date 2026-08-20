window._ortsReceiver = window._ortsReceiver || {
    ws: null,
    stallTimer: null,
    connectTimeout: null,
    resizeHandler: null,
    drawing: false,
    lastObjectUrl: null,
    stopped: false,
    currentUrl: null,
    dotNetRef: null,
    reconnectTimer: null
};

const STALL_MS = 1500;         // Sin frames => reconectar (antes 4 s: fallback eterno)
const CONNECT_TIMEOUT_MS = 1200; // Sin primer frame => error (antes 5 s)

function _ortsClearTimers() {
    const r = window._ortsReceiver;
    if (r.stallTimer) {
        clearTimeout(r.stallTimer);
        r.stallTimer = null;
    }
    if (r.connectTimeout) {
        clearTimeout(r.connectTimeout);
        r.connectTimeout = null;
    }
    if (r.reconnectTimer) {
        clearTimeout(r.reconnectTimer);
        r.reconnectTimer = null;
    }
}

function _ortsCleanupCanvasListeners() {
    const r = window._ortsReceiver;
    if (r.resizeHandler) {
        window.removeEventListener('resize', r.resizeHandler);
        r.resizeHandler = null;
    }
}

function _ortsRevokeLastUrl() {
    const r = window._ortsReceiver;
    if (r.lastObjectUrl) {
        try { URL.revokeObjectURL(r.lastObjectUrl); } catch { }
        r.lastObjectUrl = null;
    }
}

function _ortsNotifyError(dotNetRef) {
    if (dotNetRef) {
        try {
            dotNetRef.invokeMethodAsync('OnStreamError');
        } catch (e) {
            console.warn('ORTS: no se pudo notificar OnStreamError', e);
        }
    }
}

function _ortsCloseWs() {
    const r = window._ortsReceiver;
    if (r.ws) {
        try {
            r.ws.onopen = null;
            r.ws.onmessage = null;
            r.ws.onerror = null;
            r.ws.onclose = null;
            r.ws.close();
        } catch { }
        r.ws = null;
    }
}

/**
 * Arranca el stream WebSocket de frames JPEG.
 * Si se pierde el vídeo (stall o cierre), notifica a .NET para reconectar.
 */
window.startWebSocketStreamWithFallback = (wsUrl, dotNetRef) => {
    const r = window._ortsReceiver;
    r.stopped = false;
    r.currentUrl = wsUrl;
    r.dotNetRef = dotNetRef;
    r.drawing = false;

    _ortsClearTimers();
    _ortsCleanupCanvasListeners();
    _ortsCloseWs();
    _ortsRevokeLastUrl();

    let ws;
    try {
        ws = new WebSocket(wsUrl);
        r.ws = ws;
    } catch (err) {
        console.error('ORTS: error al crear WebSocket', err);
        _ortsNotifyError(dotNetRef);
        return;
    }

    const canvas = document.getElementById('videoCanvas');
    if (!canvas) {
        console.warn('ORTS: canvas #videoCanvas no encontrado');
        _ortsCloseWs();
        _ortsNotifyError(dotNetRef);
        return;
    }

    const ctx = canvas.getContext('2d');
    let received = false;

    function resizeCanvas() {
        const w = canvas.clientWidth;
        const h = canvas.clientHeight;
        if (w > 0 && h > 0 && (canvas.width !== w || canvas.height !== h)) {
            canvas.width = w;
            canvas.height = h;
        }
    }
    resizeCanvas();
    r.resizeHandler = resizeCanvas;
    window.addEventListener('resize', resizeCanvas);

    function armStallWatchdog() {
        if (r.stallTimer) clearTimeout(r.stallTimer);
        r.stallTimer = setTimeout(() => {
            if (r.stopped) return;
            console.warn('ORTS: sin frames durante ' + STALL_MS + ' ms (stream congelado). Reconectando…');
            _ortsCloseWs();
            _ortsNotifyError(dotNetRef);
        }, STALL_MS);
    }

    // Timeout del primer frame
    r.connectTimeout = setTimeout(() => {
        if (r.stopped || received) return;
        console.warn('ORTS: no se recibió ningún frame en ' + CONNECT_TIMEOUT_MS + ' ms.');
        _ortsCloseWs();
        _ortsNotifyError(dotNetRef);
    }, CONNECT_TIMEOUT_MS);

    ws.binaryType = 'arraybuffer';

    ws.onmessage = function (event) {
        if (r.stopped) return;

        try {
            // Watchdog: cada frame reinicia el contador de stall
            // (aunque luego se descarte por estar dibujando)
            armStallWatchdog();

            // Si aún se está decodificando el frame anterior, descartar este
            // (evita acumulación de Image/ObjectURL en equipos limitados)
            if (r.drawing) {
                return;
            }

            r.drawing = true;
            const blob = new Blob([event.data], { type: 'image/jpeg' });
            const url = URL.createObjectURL(blob);
            const img = new Image();

            img.onload = function () {
                try {
                    if (!r.stopped) {
                        resizeCanvas();
                        ctx.drawImage(img, 0, 0, canvas.width, canvas.height);

                        if (!received) {
                            received = true;
                            if (r.connectTimeout) {
                                clearTimeout(r.connectTimeout);
                                r.connectTimeout = null;
                            }
                            // Primer frame dibujado: ocultar fallback en .NET
                            if (dotNetRef) {
                                try { dotNetRef.invokeMethodAsync('OnStreamRecovered'); } catch { }
                            }
                        }
                    }
                } finally {
                    URL.revokeObjectURL(url);
                    r.drawing = false;
                }
            };

            img.onerror = function () {
                URL.revokeObjectURL(url);
                r.drawing = false;
                console.error('ORTS: error al decodificar frame JPEG');
                // Un frame corrupto no implica perder el stream; el watchdog
                // se encargará si dejan de llegar frames válidos.
            };

            img.src = url;
        } catch (err) {
            r.drawing = false;
            console.error('ORTS: error en onmessage', err);
            if (!r.stopped) {
                _ortsCloseWs();
                _ortsNotifyError(dotNetRef);
            }
        }
    };

    ws.onerror = function (e) {
        console.error('ORTS: WebSocket error', e);
        // onclose se encargará de notificar (evita doble OnStreamError)
    };

    ws.onclose = function () {
        console.warn('ORTS: WebSocket cerrado. received=', received, 'stopped=', r.stopped);
        _ortsClearTimers();
        if (r.stopped) return;
        // Siempre notificar: también tras haber recibido frames (pérdida de conexión)
        _ortsNotifyError(dotNetRef);
    };
};

window.stopWebSocketStream = () => {
    const r = window._ortsReceiver;
    r.stopped = true;
    r.currentUrl = null;
    r.dotNetRef = null;
    _ortsClearTimers();
    _ortsCleanupCanvasListeners();
    _ortsCloseWs();
    _ortsRevokeLastUrl();
    r.drawing = false;
};

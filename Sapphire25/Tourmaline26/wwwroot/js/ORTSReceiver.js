window._ortsReceiver = window._ortsReceiver || {};

window.startWebSocketStreamWithFallback = (wsUrl, dotNetRef) => {
    if (window._ortsReceiver.ws) {
        try { window._ortsReceiver.ws.close(); } catch { }
    }
    let ws;
    try {
        ws = new WebSocket(wsUrl);
        window._ortsReceiver.ws = ws;
    } catch (err) {
        if (dotNetRef) dotNetRef.invokeMethodAsync('OnStreamError');
        return;
    }

    const canvas = document.getElementById('videoCanvas');
    if (!canvas) {
        return;
    }
    const ctx = canvas.getContext('2d');
    let received = false;
    let timeoutId = setTimeout(() => { 
        console.log("Timeout de 5 segundos alcanzado. Received:", received);
        if (!received && dotNetRef) {
            console.warn("No se recibió ningún frame en 5 segundos.");
            dotNetRef.invokeMethodAsync('OnStreamError');
        }
        try { ws.close(); } catch { }
    }, 5000);

    function resizeCanvas() {
        canvas.width = canvas.clientWidth;
        canvas.height = canvas.clientHeight;
    }
    resizeCanvas();
    window.addEventListener('resize', resizeCanvas);

    ws.binaryType = "arraybuffer";
    ws.onmessage = function (event) {
        try {
            if (!received) {
                received = true;
                clearTimeout(timeoutId);
            }
            const blob = new Blob([event.data], { type: "image/jpeg" });
            const url = URL.createObjectURL(blob);
            const img = new Image();
            img.onload = function () {
                resizeCanvas();
                ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
                URL.revokeObjectURL(url);
                console.log("Imagen dibujada en canvas.");
            };
            img.onerror = function () {
                console.error("Error al cargar la imagen.");
                if (dotNetRef) dotNetRef.invokeMethodAsync('OnStreamError');
            };
            img.src = url;
        } catch (err) {
            console.error("Error en onmessage:", err);
            if (dotNetRef) dotNetRef.invokeMethodAsync('OnStreamError');
        }
    };

    ws.onerror = function (e) {
        console.error("WebSocket error:", e);
        if (dotNetRef) dotNetRef.invokeMethodAsync('OnStreamError');
        try { ws.close(); } catch { }
    };

    ws.onclose = function () {
        console.warn("WebSocket cerrado. received:", received);
        if (!received && dotNetRef) dotNetRef.invokeMethodAsync('OnStreamError');
    };
};

window.stopWebSocketStream = () => {
    if (window._ortsReceiver.ws) {
        try { window._ortsReceiver.ws.close(); } catch { }
        window._ortsReceiver.ws = null;
    }
};
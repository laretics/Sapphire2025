console.log("=== hls-player.js cargado correctamente ===");

window.playHLS = function (videoId, hlsUrl) {
    console.log(`=== playHLS llamado para ${videoId} | URL: ${hlsUrl} ===`);

    const video = document.getElementById(videoId);
    if (!video) {
        console.error("No se encontró el elemento video");
        return;
    }

    if (typeof Hls === "undefined") {
        console.error("Hls.js no está cargado");
        return;
    }

    if (Hls.isSupported()) {
        console.log("Creando instancia de HLS.js...");
        const hls = new Hls({
            debug: false,
            lowLatencyMode: false
        });

        hls.loadSource(hlsUrl);
        hls.attachMedia(video);

        hls.on(Hls.Events.MANIFEST_PARSED, function () {
            console.log("Manifest parsed - Reproduciendo");
            video.play().catch(e => console.warn("Auto-play bloqueado:", e));
        });

        hls.on(Hls.Events.ERROR, function (event, data) {
            console.error("Error HLS:", data);
            if (data.fatal) {
                console.error("Error fatal en HLS - Tipo:", data.type);
            }
        });
    } else if (video.canPlayType('application/vnd.apple.mpegurl')) {
        console.log("Usando reproducción nativa (Safari)");
        video.src = hlsUrl;
        video.play();
    } else {
        console.error("Navegador no soporta HLS");
    }
};
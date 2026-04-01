window.startWebRTC = async function (videoElementId, webrtcUrl) {
    const video = document.getElementById(videoElementId);
    if (!video) return;

    try {
        const pc = new RTCPeerConnection({
            iceServers: []   // Sin STUN externo (tren sin internet)
        });

        pc.ontrack = (event) => {
            video.srcObject = event.streams[0];
            console.log("Stream recibido correctamente");
        };

        // WHEP (WebRTC HTTP Egress Protocol) - estándar usado por MediaMTX
        const response = await fetch(webrtcUrl + "/whep", {
            method: "POST",
            headers: { "Content-Type": "application/sdp" },
            body: ""
        });

        if (!response.ok) {
            throw new Error("Error al conectar con WHEP");
        }

        const offerSdp = await response.text();
        await pc.setRemoteDescription({ type: "offer", sdp: offerSdp });

        const answer = await pc.createAnswer();
        await pc.setLocalDescription(answer);

        // Enviar la answer de vuelta
        await fetch(webrtcUrl + "/whep", {
            method: "PATCH",
            headers: { "Content-Type": "application/sdp" },
            body: answer.sdp
        });

    } catch (err) {
        console.error("Error WebRTC:", err);
    }
};


//Función para mostrar un diálogo modal.
// Se mueve a document.body para evitar stacking contexts de padres
// (alert, z-index, backdrop-filter) que dejan el modal bajo el backdrop
// y con los botones inaccesibles.
window.showModal = (modalId) => {
    var el = document.getElementById(modalId);
    if (!el) return;
    if (el.parentElement !== document.body) {
        document.body.appendChild(el);
    }
    var modal = bootstrap.Modal.getOrCreateInstance(el);
    modal.show();
};

//Función del foco por defecto.
window.focusElement = (element) => {
    if (element && typeof element.focus == "function") {
        element.focus();
    }
};

//Rutina para hacer click en un InputFile oculto desde un botón normal
window.triggerFileInputClick = function (element) {
    if (element) element.click();
}

// Descarga de texto (CSV, XML, etc.) como archivo en el navegador.
window.downloadTextFile = function (content, filename, mimeType) {
    const type = mimeType || "text/plain;charset=utf-8";
    const blob = new Blob([content], { type: type });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = filename || "export.txt";
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
};

//Rutina para pegar contenido del portapapeles en un elemento HTML.
window.clipboardInterop = {
    registerPasteListener: function (elemento, refDotNet) {
        elemento.addEventListener("paste", function (evento) {
            if (evento.clipboardData) {
                var html = evento.clipboardData.getData("text/html");
                refDotNet.invokeMethodAsync("receiveHtmlFromClipboard", html);
                evento.preventDefault();
            }
        })
    }
};

//Handle para prorrogar caducidad de sesión de un usuario que esté activo
window.sapphireSession = {
    registerActivity: function(dotNetRef) {
    const notify = () => dotNetRef.invokeMethodAsync('OnUserActivity');
    ['click', 'keydown', 'mousemove', 'scroll', 'touchstart'].forEach(evt =>
        document.addEventListener(evt, notify, { passive: true })
    );
    }
};

// Impresión de un elemento SVG en iframe oculto (una sola hoja A4 apaisada)
window.sapphirePrint = {
    _logoDataUrl: null,
    _logoLoading: null,

    preloadLogo: function (logoPath) {
        if (this._logoDataUrl) {
            return Promise.resolve(this._logoDataUrl);
        }
        if (this._logoLoading) {
            return this._logoLoading;
        }

        const logoUrl = new URL(logoPath || "img/sfmImg.png", document.baseURI).href;
        this._logoLoading = fetch(logoUrl)
            .then((response) => {
                if (!response.ok) {
                    throw new Error("Logo no encontrado");
                }
                return response.blob();
            })
            .then((blob) => new Promise((resolve, reject) => {
                const reader = new FileReader();
                reader.onloadend = () => {
                    this._logoDataUrl = reader.result;
                    resolve(this._logoDataUrl);
                };
                reader.onerror = reject;
                reader.readAsDataURL(blob);
            }))
            .catch(() => logoUrl)
            .finally(() => {
                this._logoLoading = null;
            });

        return this._logoLoading;
    },

    printSvg: async function (svgElementId, options) {
        const svg = document.getElementById(svgElementId);
        if (!svg) {
            console.error("SVG no encontrado:", svgElementId);
            return;
        }

        const title = options?.title || "Gráfico";
        const subtitle = options?.subtitle || "";
        const period = options?.period || "";
        const logoPath = options?.logoUrl || "img/sfmImg.png";
        const logoUrl = await this.preloadLogo(logoPath);

        const escapeHtml = (text) => {
            const div = document.createElement("div");
            div.textContent = text ?? "";
            return div.innerHTML;
        };

        let svgHtml = new XMLSerializer().serializeToString(svg);
        if (!svgHtml.includes("xmlns=")) {
            svgHtml = svgHtml.replace("<svg", '<svg xmlns="http://www.w3.org/2000/svg"');
        }
        svgHtml = svgHtml
            .replace(/\s+width="[^"]*"/, "")
            .replace(/\s+height="[^"]*"/, "")
            .replace(/preserveAspectRatio="[^"]*"/, 'preserveAspectRatio="xMidYMid meet"');

        const viewBox = svg.getAttribute("viewBox");
        const viewBoxParts = viewBox ? viewBox.split(/\s+/).map(Number) : [];
        const vbW = viewBoxParts[2] || 1;
        const vbH = viewBoxParts[3] || 1;
        const pageWidthMm = 277;
        const pageHeightMm = 190;
        const scale = Math.min(pageWidthMm / vbW, pageHeightMm / vbH);
        const svgWidthMm = vbW * scale;
        const svgHeightMm = vbH * scale;

        const generatedDate = new Date().toLocaleDateString("es-ES", {
            day: "2-digit",
            month: "2-digit",
            year: "numeric"
        });

        let frame = document.getElementById("sapphire-print-frame");
        if (!frame) {
            frame = document.createElement("iframe");
            frame.id = "sapphire-print-frame";
            frame.setAttribute("aria-hidden", "true");
            frame.style.cssText = "position:fixed;left:-10000px;top:0;width:297mm;height:210mm;border:0;visibility:hidden;";
            document.body.appendChild(frame);
        }

        const printDocument = frame.contentWindow.document;
        printDocument.open();
        printDocument.write(`<!DOCTYPE html>
<html lang="es">
<head>
<meta charset="utf-8">
<title>${escapeHtml(title)}</title>
<style>
  @page { size: A4 landscape; margin: 10mm; }
  * { box-sizing: border-box; }
  html, body {
    font-family: Arial, Helvetica, sans-serif;
    margin: 0;
    padding: 0;
    color: #222;
    width: ${pageWidthMm}mm;
    height: ${pageHeightMm}mm;
    overflow: hidden;
  }
  .print-page {
    position: relative;
    width: ${pageWidthMm}mm;
    height: ${pageHeightMm}mm;
    overflow: hidden;
    page-break-after: avoid;
    page-break-inside: avoid;
  }
  .chart-container {
    width: ${pageWidthMm}mm;
    height: ${pageHeightMm}mm;
    display: flex;
    align-items: center;
    justify-content: center;
    overflow: hidden;
  }
  .chart-container svg {
    display: block;
    width: ${svgWidthMm}mm;
    height: ${svgHeightMm}mm;
    flex: 0 0 auto;
    -webkit-print-color-adjust: exact;
    print-color-adjust: exact;
  }
  .chart-container svg * {
    -webkit-print-color-adjust: exact;
    print-color-adjust: exact;
  }
  @media print {
    html, body, .print-page, .chart-container {
      width: ${pageWidthMm}mm;
      height: ${pageHeightMm}mm;
      overflow: hidden;
    }
  }
  .cover-box {
    position: absolute;
    bottom: 0;
    right: 0;
    z-index: 10;
    max-width: 58mm;
    padding: 3mm 4mm;
    border: 1px solid #bbb;
    border-radius: 3px;
    background: #fff;
    line-height: 1.25;
  }
  .cover-box img {
    display: block;
    width: 22mm;
    max-width: 100%;
    height: auto;
    margin: 0 auto 2mm auto;
    object-fit: contain;
    -webkit-print-color-adjust: exact;
    print-color-adjust: exact;
  }
  .cover-box h1 {
    font-size: 8pt;
    font-weight: bold;
    margin: 0 0 1.5mm;
  }
  .cover-box .meta {
    font-size: 7pt;
    color: #444;
    margin: 0.5mm 0;
  }
</style>
</head>
<body>
  <div class="print-page">
    <div class="chart-container">${svgHtml}</div>
    <aside class="cover-box">
      <img src="${logoUrl}" alt="SFM" />
      <h1>${escapeHtml(title)}</h1>
      ${subtitle ? `<p class="meta">${escapeHtml(subtitle)}</p>` : ""}
      ${period ? `<p class="meta">${escapeHtml(period)}</p>` : ""}
      <p class="meta">Generado el ${generatedDate}</p>
    </aside>
  </div>
</body>
</html>`);
        printDocument.close();

        const printWindow = frame.contentWindow;
        printWindow.focus();
        printWindow.print();
    }
};

window.sapphirePrint.preloadLogo();

window.sapphireSound = {
    _audioCache: {},

    play: function (soundPath) {
        const url = new URL(soundPath || "sound/Ding.mp3", document.baseURI).href;
        let audio = this._audioCache[url];
        if (!audio) {
            audio = new Audio(url);
            this._audioCache[url] = audio;
        }
        audio.currentTime = 0;
        const playPromise = audio.play();
        if (playPromise) {
            playPromise.catch(() => { });
        }
    }
};


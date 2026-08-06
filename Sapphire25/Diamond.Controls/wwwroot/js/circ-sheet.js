// Libro itinerario: impresión A4 apaisada multipágina + descarga PDF (bytes desde .NET).
window.diamondCircSheet = {
  /**
   * Imprime desde un array de SVG (puede fallar con libros grandes por tamaño SignalR).
   * Preferir printFromRoot para el libro completo.
   * @param {string[]|string} svgPages
   */
  print: function (svgPages) {
    var pages = normalizePages(svgPages);
    if (!pages || pages.length === 0) {
      pages = collectPagesFromDom();
    }
    printPages(pages);
  },

  /**
   * Lee todas las .diamond-circ-sheet-page del contenedor del diálogo (evita límite SignalR).
   * @param {HTMLElement} root  elemento .diamond-circ-sheet-pages
   */
  printFromRoot: function (root) {
    var pages = collectPagesFromElement(root);
    if (!pages || pages.length === 0) {
      pages = collectPagesFromDom();
    }
    printPages(pages);
  },

  /**
   * Descarga un PDF generado en el servidor (.NET).
   * @param {string} base64  PDF en base64
   * @param {string} fileName
   */
  downloadPdf: function (base64, fileName) {
    if (!base64) {
      return;
    }
    var name = fileName || "libro-itinerario.pdf";
    var bin = atob(base64);
    var len = bin.length;
    var bytes = new Uint8Array(len);
    var i = 0;
    while (i < len) {
      bytes[i] = bin.charCodeAt(i);
      i++;
    }
    var blob = new Blob([bytes], { type: "application/pdf" });
    var url = URL.createObjectURL(blob);
    var a = document.createElement("a");
    a.href = url;
    a.download = name;
    a.rel = "noopener";
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(function () {
      URL.revokeObjectURL(url);
    }, 5000);
  }
};

function printPages(pages) {
  if (!pages || pages.length === 0) {
    window.print();
    return;
  }

  var styleId = "diamond-circ-sheet-force-page";
  var mountId = "diamond-circ-sheet-print-mount";
  removeEl(styleId);
  removeEl(mountId);

  var pageW = "297mm";
  var pageH = "210mm";

  var style = document.createElement("style");
  style.id = styleId;
  // Importante: la regla off-screen SOLO en pantalla; en @media print el mount
  // debe estar en flujo normal con page-break entre hojas (si no, 1 sola página).
  style.textContent =
    "@page { size: A4 landscape !important; margin: 0 !important; }\n" +
    "@media print {\n" +
    "  html.diamond-printing-sheet, html.diamond-printing-sheet body {\n" +
    "    width: " + pageW + " !important;\n" +
    "    height: auto !important;\n" +
    "    margin: 0 !important;\n" +
    "    padding: 0 !important;\n" +
    "    background: #fff !important;\n" +
    "    overflow: visible !important;\n" +
    "  }\n" +
    "  html.diamond-printing-sheet body > *:not(#" + mountId + ") {\n" +
    "    display: none !important;\n" +
    "  }\n" +
    "  html.diamond-printing-sheet #" + mountId + " {\n" +
    "    display: block !important;\n" +
    "    position: static !important;\n" +
    "    left: auto !important;\n" +
    "    top: auto !important;\n" +
    "    width: " + pageW + " !important;\n" +
    "    height: auto !important;\n" +
    "    margin: 0 !important;\n" +
    "    padding: 0 !important;\n" +
    "    background: #fff !important;\n" +
    "    overflow: visible !important;\n" +
    "    pointer-events: auto !important;\n" +
    "  }\n" +
    "  html.diamond-printing-sheet #" + mountId + " .diamond-circ-sheet-print-page {\n" +
    "    display: block !important;\n" +
    "    box-sizing: border-box !important;\n" +
    "    margin: 0 !important;\n" +
    "    padding: 0 !important;\n" +
    "    width: " + pageW + " !important;\n" +
    "    height: " + pageH + " !important;\n" +
    "    min-height: " + pageH + " !important;\n" +
    "    max-height: " + pageH + " !important;\n" +
    "    overflow: hidden !important;\n" +
    "    page-break-after: always !important;\n" +
    "    break-after: page !important;\n" +
    "    page-break-inside: avoid !important;\n" +
    "    break-inside: avoid !important;\n" +
    "    background: #fff !important;\n" +
    "  }\n" +
    "  html.diamond-printing-sheet #" + mountId + " .diamond-circ-sheet-print-page:last-child {\n" +
    "    page-break-after: auto !important;\n" +
    "    break-after: auto !important;\n" +
    "  }\n" +
    "  html.diamond-printing-sheet #" + mountId + " .diamond-circ-sheet-print-page svg {\n" +
    "    display: block !important;\n" +
    "    width: " + pageW + " !important;\n" +
    "    height: " + pageH + " !important;\n" +
    "  }\n" +
    "}\n" +
    "@media screen {\n" +
    "  html.diamond-printing-sheet #" + mountId + " {\n" +
    "    position: fixed;\n" +
    "    left: -10000px;\n" +
    "    top: 0;\n" +
    "    width: " + pageW + ";\n" +
    "    pointer-events: none;\n" +
    "  }\n" +
    "}\n";

  document.head.appendChild(style);

  var mount = document.createElement("div");
  mount.id = mountId;
  mount.setAttribute("aria-hidden", "true");

  var i = 0;
  while (i < pages.length) {
    var page = document.createElement("div");
    page.className = "diamond-circ-sheet-print-page";
    page.innerHTML = pages[i];
    var svg = page.querySelector("svg");
    if (svg && !svg.getAttribute("xmlns")) {
      svg.setAttribute("xmlns", "http://www.w3.org/2000/svg");
    }
    mount.appendChild(page);
    i++;
  }

  document.body.appendChild(mount);
  document.documentElement.classList.add("diamond-printing-sheet");

  var cleaned = false;
  var cleanup = function () {
    if (cleaned) {
      return;
    }
    cleaned = true;
    document.documentElement.classList.remove("diamond-printing-sheet");
    removeEl(mountId);
    removeEl(styleId);
    window.removeEventListener("afterprint", cleanup);
  };

  window.addEventListener("afterprint", cleanup);
  setTimeout(cleanup, 180000);

  // Dar tiempo a layout multipágina antes del diálogo.
  requestAnimationFrame(function () {
    requestAnimationFrame(function () {
      window.print();
    });
  });
}

function normalizePages(svgPages) {
  if (!svgPages) {
    return [];
  }
  if (typeof svgPages === "string") {
    return svgPages.trim() ? [svgPages] : [];
  }
  if (Array.isArray(svgPages) || (typeof svgPages.length === "number" && typeof svgPages !== "string")) {
    var out = [];
    var i = 0;
    var n = svgPages.length;
    while (i < n) {
      var s = svgPages[i];
      if (s != null && String(s).trim().length > 0) {
        out.push(String(s));
      }
      i++;
    }
    return out;
  }
  return [];
}

function collectPagesFromDom() {
  return collectPagesFromElement(document.querySelector(".diamond-circ-sheet-pages"));
}

function collectPagesFromElement(root) {
  if (!root) {
    return [];
  }
  var nodes = root.querySelectorAll(".diamond-circ-sheet-page");
  var out = [];
  var i = 0;
  while (i < nodes.length) {
    var svg = nodes[i].querySelector("svg");
    if (svg) {
      out.push(svg.outerHTML);
    } else if (nodes[i].innerHTML && nodes[i].innerHTML.indexOf("<svg") >= 0) {
      out.push(nodes[i].innerHTML);
    }
    i++;
  }
  return out;
}

function removeEl(id) {
  var el = document.getElementById(id);
  if (el) {
    el.remove();
  }
}

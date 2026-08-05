// Ficha de marcha: impresión A4 multipágina aislada del layout de la app.
// Recibe los SVG ya renderizados (strings) desde Blazor — no clona el DOM en pantalla
// (el diálogo puede estar recortado por overflow de la malla).
window.diamondCircSheet = {
  /**
   * @param {string[]|string} svgPages  Markup de cada página (elemento <svg>…</svg>).
   */
  print: function (svgPages) {
    var pages = normalizePages(svgPages);
    if (!pages || pages.length === 0) {
      // Fallback: intentar DOM en pantalla (mejor que nada).
      pages = collectPagesFromDom();
    }
    if (!pages || pages.length === 0) {
      window.print();
      return;
    }

    var styleId = "diamond-circ-sheet-force-page";
    var mountId = "diamond-circ-sheet-print-mount";
    removeEl(styleId);
    removeEl(mountId);

    var pageW = "210mm";
    var pageH = "297mm";

    var style = document.createElement("style");
    style.id = styleId;
    // Sin media=print: así el mount se ve en el layout de impresión de forma fiable.
    style.textContent =
      "@page { size: A4 portrait !important; margin: 0 !important; }\n" +
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
      "    visibility: hidden !important;\n" +
      "    height: 0 !important;\n" +
      "    width: 0 !important;\n" +
      "    overflow: hidden !important;\n" +
      "    position: absolute !important;\n" +
      "    left: -99999px !important;\n" +
      "  }\n" +
      "  html.diamond-printing-sheet #" + mountId + " {\n" +
      "    display: block !important;\n" +
      "    visibility: visible !important;\n" +
      "    position: static !important;\n" +
      "    left: auto !important;\n" +
      "    width: " + pageW + " !important;\n" +
      "    height: auto !important;\n" +
      "    margin: 0 !important;\n" +
      "    padding: 0 !important;\n" +
      "    background: #fff !important;\n" +
      "    overflow: visible !important;\n" +
      "  }\n" +
      "  html.diamond-printing-sheet #" + mountId + " .diamond-circ-sheet-print-page {\n" +
      "    display: block !important;\n" +
      "    visibility: visible !important;\n" +
      "    box-sizing: border-box !important;\n" +
      "    margin: 0 !important;\n" +
      "    padding: 0 !important;\n" +
      "    width: " + pageW + " !important;\n" +
      "    height: " + pageH + " !important;\n" +
      "    min-width: " + pageW + " !important;\n" +
      "    min-height: " + pageH + " !important;\n" +
      "    max-width: " + pageW + " !important;\n" +
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
      "    visibility: visible !important;\n" +
      "    width: " + pageW + " !important;\n" +
      "    height: " + pageH + " !important;\n" +
      "    max-width: none !important;\n" +
      "    max-height: none !important;\n" +
      "  }\n" +
      "}\n" +
      /* En pantalla el mount no debe tapar la app entre print y afterprint. */ +
      "html.diamond-printing-sheet #" + mountId + " {\n" +
      "  position: fixed;\n" +
      "  left: -10000px;\n" +
      "  top: 0;\n" +
      "  width: " + pageW + ";\n" +
      "  pointer-events: none;\n" +
      "}\n";

    document.head.appendChild(style);

    var mount = document.createElement("div");
    mount.id = mountId;
    mount.setAttribute("aria-hidden", "true");

    var i = 0;
    while (i < pages.length) {
      var page = document.createElement("div");
      page.className = "diamond-circ-sheet-print-page";
      // SVG completo (viewBox A4); no depende del clipping del diálogo en pantalla.
      page.innerHTML = pages[i];
      // Asegurar xmlns por si el markup viene sin él.
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
    setTimeout(cleanup, 120000);

    // Dejar un frame para que el motor aplique estilos antes de abrir el diálogo.
    requestAnimationFrame(function () {
      requestAnimationFrame(function () {
        window.print();
      });
    });
  }
};

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
  var root = document.querySelector(".diamond-circ-sheet-pages");
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

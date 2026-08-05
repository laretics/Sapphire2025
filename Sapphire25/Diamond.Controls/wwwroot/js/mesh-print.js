// Impresión de malla: escala el contenido al 100 % de la página (A3 o A4 apaisado).
// Inyecta @page size (Chrome/Edge a menudo ignoran @page con nombre del CSS estático).
window.diamondMeshPrint = {
  /**
   * @param {string} [paperSize] "A3" | "A4" (landscape). Por defecto A3.
   */
  print: function (paperSize) {
    var size = (paperSize || "A3").toString().toUpperCase() === "A4" ? "A4" : "A3";
    var pageCss = size === "A4" ? "A4 landscape" : "A3 landscape";
    var pageW = size === "A4" ? "297mm" : "420mm";
    var pageH = size === "A4" ? "210mm" : "297mm";
    // Misma proporción que MeshPrintDocument.TitleBlockHeightRatio (14 %)
    var titleRatio = 0.14;
    var plotPct = ((1 - titleRatio) * 100).toFixed(4) + "%";
    var titlePct = (titleRatio * 100).toFixed(4) + "%";

    var styleId = "diamond-mesh-print-force-page";
    var existing = document.getElementById(styleId);
    if (existing) {
      existing.remove();
    }

    var style = document.createElement("style");
    style.id = styleId;
    style.setAttribute("media", "print");
    style.textContent =
      "@page { size: " + pageCss + " !important; margin: 0 !important; }\n" +
      "html.diamond-printing-mesh, html.diamond-printing-mesh body {\n" +
      "  width: " + pageW + " !important;\n" +
      "  height: " + pageH + " !important;\n" +
      "  margin: 0 !important;\n" +
      "  padding: 0 !important;\n" +
      "  overflow: hidden !important;\n" +
      "}\n" +
      "html.diamond-printing-mesh .diamond-mesh-print-overlay,\n" +
      "html.diamond-printing-mesh .diamond-mesh-print-dialog,\n" +
      "html.diamond-printing-mesh .diamond-mesh-print-pages,\n" +
      "html.diamond-printing-mesh .diamond-mesh-print-page,\n" +
      "html.diamond-printing-mesh .diamond-mesh-print-sheet {\n" +
      "  width: " + pageW + " !important;\n" +
      "  height: " + pageH + " !important;\n" +
      "  min-width: " + pageW + " !important;\n" +
      "  min-height: " + pageH + " !important;\n" +
      "  max-width: " + pageW + " !important;\n" +
      "  max-height: " + pageH + " !important;\n" +
      "}\n" +
      "html.diamond-printing-mesh .diamond-mesh-print-drawing {\n" +
      "  width: " + pageW + " !important;\n" +
      "  height: " + plotPct + " !important;\n" +
      "  min-height: " + plotPct + " !important;\n" +
      "  max-height: " + plotPct + " !important;\n" +
      "}\n" +
      "html.diamond-printing-mesh .diamond-mesh-print-drawing svg.diamond-mesh-print-svg {\n" +
      "  width: " + pageW + " !important;\n" +
      "  height: 100% !important;\n" +
      "  min-width: " + pageW + " !important;\n" +
      "  min-height: 100% !important;\n" +
      "}\n" +
      "html.diamond-printing-mesh .diamond-mesh-print-titleblock {\n" +
      "  top: " + plotPct + " !important;\n" +
      "  width: " + pageW + " !important;\n" +
      "  height: " + titlePct + " !important;\n" +
      "  min-height: " + titlePct + " !important;\n" +
      "  max-height: " + titlePct + " !important;\n" +
      "}\n";

    document.head.appendChild(style);
    document.documentElement.classList.add("diamond-printing-mesh");
    document.documentElement.setAttribute("data-mesh-print-paper", size);

    var cleaned = false;
    var cleanup = function () {
      if (cleaned) {
        return;
      }
      cleaned = true;
      document.documentElement.classList.remove("diamond-printing-mesh");
      document.documentElement.removeAttribute("data-mesh-print-paper");
      var el = document.getElementById(styleId);
      if (el) {
        el.remove();
      }
      window.removeEventListener("afterprint", cleanup);
    };

    window.addEventListener("afterprint", cleanup);
    setTimeout(cleanup, 120000);

    window.print();
  }
};

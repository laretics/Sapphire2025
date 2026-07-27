// Utilidades de ficha de marcha (impresión acotada al visor).
window.diamondCircSheet = {
  print: function () {
    // Imprime la ventana; el CSS @media print oculta el resto de la app
    // y deja solo .diamond-circ-sheet-page.
    window.print();
  }
};

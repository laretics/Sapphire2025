//Función para mostrar un diálogo modal.
window.showModal = (modalId) => {
    var modal = new bootstrap.Modal(document.getElementById(modalId));
    modal.show();
};

//Función del foco por defecto.
window.focusElement = (element) => {
    if (element & typeof element.focus == "function") {
        element.focus();
    }
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

//Función para importar un archivo excel.
window.excelInterop = {
    uploadExcelFile: async function (bytes, colStart, colCount, rowStart, rowCount) {
        var datos = new Uint8Array(bytes);
        var libro = XLSX.read(datos, { type: "array", cellStyles: true });
        var primeraHoja = libro.Sheets[libro.SheetNames[0]];
        var rango = XLSX.utils.decode_range(primeraHoja['!ref']);
        var resultado = [];

        //Calculamos el rango de columnas a extraer.
        var cStart = Math.max(rango.s.c, colStart || 0);
        var cEnd = colCount ? Math.min(rango.e.c, cStart + colCount - 1) : rango.e.c;

        //Rango de filas a extraer.
        var rStart = Math.max(rango.s.r, rowStart || 0);
        var rEnd = rowCount ? Math.min(rango.e.r, rStart + rowCount - 1) : rango.e.r;

        for (var r = rStart; r <= rEnd; ++r) {
            var fila = [];
            for (var c = cStart; c <= cEnd; ++c) {
                var celdaRef = XLSX.utils.encode_cell({ r: r, c: c });
                var celda = primeraHoja[celdaRef];
                var texto = celda ? (celda.v !== undefined && celda.v !== null ? celda.v.toString() : "") : "";
                var color = "transparent";
                //if (celda && celda.s) {
                //    console.log("Estilo de la celda: ", celda.s);
                //}
                if (celda && celda.s && celda.s.fgColor && celda.s.fgColor.rgb) {
                    //console.log("fgColor: ", celda.s);
                    color = "#" + celda.s.fgColor.rgb.substring(2);
                }
                else if(celda && celda.s && celda.s.bgColor && celda.s.bgColor.rgb)
                {
                    //console.log("bgColor: ", celda.s);
                    color = "#" + celda.s.bgColor.rgb.substring(2);
                }
                fila.push({ text: texto, bg: color });
            }
            resultado.push(fila);
        }
        return JSON.stringify(resultado); //Información que recibiré en C#
    }
}

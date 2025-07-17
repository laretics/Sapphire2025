//Auxiliar para obtener el color de una celda sin el flag de transparencia
function getCellColor(rgb) {
    if (!rgb) return "transparent";
    return "#" + (rgb.length > 6 ? rgb.substring(2) : rgb);
}

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
        var comentarios = (primeraHoja['!comments'] || []);
        var notas = (primeraHoja['!notes'] || []);
        var comentariosPorCelda = {};

        //Mapeamos los comentarios de la hoja
        comentarios.forEach(function (comentario) {
            comentariosPorCelda[comentario.ref] = comentario.t;
        });
        //Mapeo de las notas clásicas
        notas.forEach(function (nota) {
            comentariosPorCelda[nota.ref] = nota.t;
        });

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
                    color = getCellColor(celda.s.fgColor.rgb);
                }
                else if(celda && celda.s && celda.s.bgColor && celda.s.bgColor.rgb)
                {
                    color = getCellColor(celda.s.bgColor.rgb);
                }
                var anotacion = comentariosPorCelda[celdaRef] || null;
                fila.push({ text: texto, bg: color, annotation: anotacion });
            }
            resultado.push(fila);
        }
        return JSON.stringify(resultado); //Información que recibiré en C#
    },
    takeSnapShot: async function (bytes, colMak,colStart, colCount) {
        var datos = new Uint8Array(bytes);
        var libro = XLSX.read(datos, { type: "array", cellStyles: true });
        var primeraHoja = libro.Sheets[libro.SheetNames[0]];
        var comentarios = (primeraHoja['!comments'] || []);
        var notas = (primeraHoja['!notes'] || []);
        var comentariosPorCelda = {};

        //Mapeamos los comentarios de la hoja
        comentarios.forEach(function (comentario) {
            comentariosPorCelda[comentario.ref] = comentario.t;
        });
        //Mapeo de las notas clásicas
        notas.forEach(function (nota) {
            comentariosPorCelda[nota.ref] = nota.t;
        });

        var rango = XLSX.utils.decode_range(primeraHoja['!ref']);
        var resultado = [];

        //Calculamos el rango de columnas a extraer.
        var cStart = Math.max(rango.s.c, colStart || 0);
        var cEnd = colCount ? Math.min(rango.e.c, cStart + colCount - 1) : rango.e.c;

        var filasVacias = 0;

        for (var r = 1; r <= rango.e.r; ++r) {
            var filaVacia = true;            
            var fila = [];
            var makiRef = XLSX.utils.encode_cell({ r: r, c: colMak });
            var celdaMaki = primeraHoja[makiRef];
            var textoMaki = celdaMaki ? (celdaMaki.v !== undefined && celdaMaki.v !== null ? celdaMaki.v.toString() : "") : "";
            fila.push({ text: textoMaki, bg: "transparent", annotation: "" });            
            for (var c = cStart; c <= cEnd; ++c) {
                var celdaRef = XLSX.utils.encode_cell({ r: r, c: c });
                var celda = primeraHoja[celdaRef];
                var texto = celda ? (celda.v !== undefined && celda.v !== null ? celda.v.toString() : "") : "";
                if ("" != texto) filaVacia = false;
                var color = "transparent";
                //if (celda && celda.s) {
                //    console.log("Estilo de la celda: ", celda.s);
                //}
                if (celda && celda.s && celda.s.fgColor && celda.s.fgColor.rgb) {
                    //console.log("fgColor: ", celda.s);
                    color = getCellColor(celda.s.fgColor.rgb);
                }
                else if (celda && celda.s && celda.s.bgColor && celda.s.bgColor.rgb) {
                    //console.log("bgColor: ", celda.s);
                    color = getCellColor(celda.s.bgColor.rgb);
                }
                var anotacion = comentariosPorCelda[celdaRef] || null;
                fila.push({ text: texto, bg: color, comment: anotacion });
            }
            if (filaVacia) filasVacias++;
            if (filasVacias > 1) break;
            resultado.push(fila);
        }
        return JSON.stringify(resultado); //Información que recibiré en C#
    },
    locateDateColumn: async function(bytes, month, day) {
        var datos = new Uint8Array(bytes);
        var libro = XLSX.read(datos, { type: "array" });
        var hoja = libro.Sheets[libro.SheetNames[0]];
        var rango = XLSX.utils.decode_range(hoja['!ref']);

        var mesBuscado = month.trim().toUpperCase();
        var diaBuscado = day.toString();
        var colMes = -1;

        for (var c = rango.s.c; c < rango.e.c; ++c) {
            var celda = hoja[XLSX.utils.encode_cell({ r: 1, c: c })];
            var valor = celda && celda.v ? celda.v.toString().trim().toUpperCase() : "";
            //Si encontramos el mes, activamos la búsqueda de días
            if (valor === mesBuscado) {
                colMes = c;
                continue; //Como el mes no contiene un día, saltamos a la celda siguiente.
            }

            //Modo de búsqueda del día
            if (-1!=colMes) {
                //Si encontramos otro mes se para la búsqueda
                if (valor && isNaN(Number(valor))) { break; }
                //Si el valor coincide con el día buscado, devolvemos el índice de la columna
                if (valor === diaBuscado) { return [colMes,c]; }
            }            
        }
        return [-1,-1]; //Valor no encontrado. Error.
    }
}

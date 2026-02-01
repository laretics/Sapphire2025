window.excelInterop = {
    extractWorksheetData: async function (bytes, month, day, days, startCol) {
        var datosEntrada = new Uint8Array(bytes);
        var libro = XLSX.read(datosEntrada, { type: "array", cellStyles: true, cellComments: true });        
        var hoja = libro.Sheets[libro.SheetNames[0]];
        console.log("Claves de la hoja:", Object.keys(hoja));

        var rango = XLSX.utils.decode_range(hoja['!ref']);

        // Función auxiliar para verificar si una columna está oculta
        function isColumnHidden(colIndex) {
            if (!hoja['!cols'] || !hoja['!cols'][colIndex]) {
                return false;
            }
            return hoja['!cols'][colIndex].hidden === true;
        }

        var mesBuscado = getMontString(month); 
        var colMes = -1;
        var colDia = -1;

        // Buscar mes y día, saltando columnas ocultas
        for (var col = (typeof startCol === "number" && startCol >= 0 ? startCol : rango.s.c); col < rango.e.c; ++col) {
            // Saltar columnas ocultas
            if (isColumnHidden(col)) {
                console.log("Saltando columna oculta:", col);
                continue;
            }

            var refBusca = XLSX.utils.encode_cell({ r: 1, c: col });
            var celdaBusca = hoja[refBusca];
            var textoCandidato = celdaBusca && celdaBusca.v ? celdaBusca.v.toString().trim().toUpperCase() : "";
            
            if (textoCandidato === mesBuscado) {
                colMes = col;
                continue;
            }            
            if (-1 != colMes) {
                if (textoCandidato && isNaN(Number(textoCandidato))) { break; }
                if (Number(textoCandidato) === day) { colDia = col; }
            }
        }

        var salida = [];

        if (-1 != colMes && -1 != colDia) {
            var colStart = Math.max(rango.s.c, colDia || 0);
            var colEnd = days ? Math.min(rango.e.c, colDia + days - 1) : rango.e.c;
            var filasVacias = 0;

            for (var row = 1; row <= rango.e.r; ++row) {
                var fila = [];
                var refAgente = XLSX.utils.encode_cell({ r: row, c: colMes });
                var celdaAgente = hoja[refAgente];
                var textoAgente = celdaAgente ? (celdaAgente.v !== undefined && celdaAgente.v !== null ? celdaAgente.v.toString() : "") : "";
                
                if ("" == textoAgente || mesBuscado == textoAgente) {
                    filasVacias++;
                } else {
                    filasVacias = 0;
                    fila.push({ Text: textoAgente, Bg: "transparent", Comment: "" });
                    var filaVacia = true;
                    var auxColEnd = colEnd;
                    
                    for (var col = colStart; col <= auxColEnd; ++col) {
                        // Saltar columnas ocultas durante la extracción
                        if (isColumnHidden(col)) {
                            continue;
                        }

                        var celdaInfoRef = XLSX.utils.encode_cell({ r: row, c: col });
                        var celdaInfo = hoja[celdaInfoRef];
                        var texto = celdaInfo ? (celdaInfo.v !== undefined && celdaInfo.v !== null ? celdaInfo.v.toString() : "") : "";                        
                        
                        if (texto == textoAgente) {
                            auxColEnd++;
                        } else {
                            if ("" != texto) filaVacia = false;
                            var color = "transparent";
                            if (celdaInfo && celdaInfo.s && celdaInfo.s.fgColor && celdaInfo.s.fgColor.rgb) {
                                color = getCellColor(celdaInfo.s.fgColor.rgb);
                            }
                            if (celdaInfo && celdaInfo.s && celdaInfo.s.bgColor && celdaInfo.s.bgColor.rgb) {
                                color = getCellColor(celdaInfo.s.bgColor.rgb);
                            }
                            var comment = "";
                            if (celdaInfo && celdaInfo.c && celdaInfo.c[0] && celdaInfo.c[0].t) {
                                comment = celdaInfo.c[0].t;
                            }
                            fila.push({ Text: texto, Bg: color, Comment: comment });
                        }
                    }
                    if (!filaVacia) {
                        salida.push(fila);
                    }                                       
                }
                if (filasVacias > 5) break;
            }
        }
        return JSON.stringify(salida);
    },
    
    // Función auxiliar para debugging
    showHiddenColumns: function(bytes) {
        var datosEntrada = new Uint8Array(bytes);
        var libro = XLSX.read(datosEntrada, { type: "array", cellStyles: true });        
        var hoja = libro.Sheets[libro.SheetNames[0]];
        
        if (hoja['!cols']) {
            console.log("Información de columnas:");
            hoja['!cols'].forEach((col, index) => {
                if (col && col.hidden) {
                    console.log(`Columna ${index} está OCULTA`);
                }
            });
        } else {
            console.log("No hay información de columnas ocultas/visibles");
        }
    }
};
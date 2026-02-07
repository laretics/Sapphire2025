//Librería de funciones para la importación de gráficos de personal en Excel.
//El ordenador cliente debería tener instalado Excel.

//Auxiliar para obtener el color de una celda sin el flag de transparencia
function getCellColor(rgb) {
    if (!rgb) return "transparent";
    return "#" + (rgb.length > 6 ? rgb.substring(2) : rgb);
}

function getMontString(month) {
    switch (Number(month)) {
        case 1: return "ENE"; break;
        case 2: return "FEB"; break;
        case 3: return "MAR"; break;
        case 4: return "ABR"; break;
        case 5: return "MAY"; break;
        case 6: return "JUN"; break;
        case 7: return "JUL"; break;
        case 8: return "AGO"; break;
        case 9: return "SEP"; break;
        case 10: return "OCT"; break;
        case 11: return "NOV"; break;
        case 12: return "DIC"; break;
        default: return ""; break;
    }
}

window.excelInterop = {
    //bytes: Entrada en bruto del importador.
    //month: Mes de la fecha.
    //day: día de la fecha
    //days: Número de días (o columnas) a procesar.
    //startCol: Índice de columna VISIBLE donde empieza el proceso (0-based)
    extractWorksheetData: async function (bytes, month, day, days, startCol) {
        var datosEntrada = new Uint8Array(bytes);
        var libro = XLSX.read(datosEntrada, {
            type: "array",
            cellStyles: true,
            cellComments: true,
            sheetRows: 0 //Leemos todas las filas
        });
        var hoja = libro.Sheets[libro.SheetNames[0]];

       
        var rangoCompleto = hoja['!fullref'] || hoja['!ref'];
        console.log("Rango inicial:", rangoCompleto);

        var rango = XLSX.utils.decode_range(rangoCompleto);

        //Vamos a buscar si hay celdas fuera del rango que se ha declarado
        var maxFila = rango.e.r;
        Object.keys(hoja).forEach(function (key) {
            if (key[0] !== '!') {
                var pos = XLSX.utils.decode_cell(key);
                if (pos.r > maxFila) maxFila = pos.r;
            }
        });

        if (maxFila > rango.e.r) {
            console.warn(`⚠️ ÁREA DE IMPRESIÓN DETECTADA: Extendiendo de ${rango.e.r} a ${maxFila}`);
            rango.e.r = maxFila;
        }
        console.log(`Rango final: filas 0-${rango.e.r}`);

        // MEJORADA: Verifica si una columna está oculta O tiene ancho mínimo
        function isColumnHidden(colIndex) {
            if (!hoja['!cols'] || !hoja['!cols'][colIndex]) {
                return false;
            }

            var colInfo = hoja['!cols'][colIndex];

            // Verificar si está marcada como oculta
            if (colInfo.hidden === true) {
                return true;
            }

            // NUEVO: Verificar ancho de columna
            // wch = ancho en caracteres, wpx = ancho en píxeles
            // Consideramos oculta si el ancho es menor a 1 carácter o menor a 8 píxeles
            if (colInfo.wch !== undefined && colInfo.wch < 1) {
                console.log(`Columna ${colIndex} considerada oculta por ancho (${colInfo.wch} caracteres)`);
                return true;
            }

            if (colInfo.wpx !== undefined && colInfo.wpx < 8) {
                console.log(`Columna ${colIndex} considerada oculta por ancho (${colInfo.wpx} píxeles)`);
                return true;
            }

            return false;
        }

        // NUEVA FUNCIÓN: Convierte índice de columna visible a índice absoluto
        function getAbsoluteColumnIndex(visibleIndex) {
            if (visibleIndex < 0) return rango.s.c; // Si es negativo, empezar desde el inicio

            var visibleCount = 0;
            for (var col = rango.s.c; col <= rango.e.c; col++) {
                if (!isColumnHidden(col)) {
                    if (visibleCount === visibleIndex) {
                        console.log(`Columna visible ${visibleIndex} corresponde a columna absoluta ${col}`);
                        return col;
                    }
                    visibleCount++;
                }
            }
            // Si el índice visible es mayor que el número de columnas visibles, devolver la última columna
            console.warn(`Índice visible ${visibleIndex} fuera de rango, usando última columna visible`);
            return rango.e.c;
        }

        // MODIFICADO: Convertir startCol de índice visible a índice absoluto
        var startColAbsolute = (typeof startCol === "number" && startCol >= 0)
            ? getAbsoluteColumnIndex(startCol)
            : rango.s.c;

        console.log(`startCol (visible): ${startCol}, startCol (absoluto): ${startColAbsolute}`);

        //Busco la columna del mes - Usando las 3 primeras letras
        var mesBuscado = getMontString(month); // "ENE", "FEB", "MAR"...
        console.log("Buscando mes:", mesBuscado);
        var colMes = -1;
        var colDia = -1;

        // MODIFICADO: Empezar desde la columna absoluta calculada
        for (var col = startColAbsolute; col <= rango.e.c; ++col) {
            // Saltar columnas ocultas durante la búsqueda
            if (isColumnHidden(col)) {
                //console.log("Saltando columna oculta en búsqueda:", col);
                continue;
            }

            var refBusca = XLSX.utils.encode_cell({ r: 1, c: col });
            var celdaBusca = hoja[refBusca];
            var textoCandidato = celdaBusca && celdaBusca.v ? celdaBusca.v.toString().trim().toUpperCase() : "";

            //Si encontramos el mes (comparando si contiene las 3 letras)
            if (textoCandidato.includes(mesBuscado)) {
                colMes = col;
                console.log("Mes encontrado en columna:", col, "Texto:", textoCandidato);
                continue; //El mes nunca va a contener el número del día, así que saltamos a la celda siguiente.
            }
            if (-1 != colMes) {
                //Buscando el día
                if (textoCandidato && isNaN(Number(textoCandidato))) {
                    // Verificar si es otro mes (3 letras consecutivas)
                    if (textoCandidato.length >= 3 && /^[A-Z]{3}/.test(textoCandidato)) {
                        console.log("Detectado nuevo mes, finalizando búsqueda");
                        break;
                    }
                }
                //Si el valor de la columna coincide con el día buscado, ya tenemos el índice.
                if (Number(textoCandidato) === day) {
                    colDia = col;
                    console.log("Día encontrado en columna:", col);
                }
            }
        }

        var salida = [];

        if (-1 != colMes && -1 != colDia) {
            console.log("Extrayendo datos desde colMes:", colMes, "colDia:", colDia);
            //Límites del bucle de extracción.
            var colStart = Math.max(rango.s.c, colDia || 0);
            var colEnd = days ? Math.min(rango.e.c, colDia + days - 1) : rango.e.c;
            var filasVacias = 0;

            for (var row = 1; row <= rango.e.r; ++row) {
                var fila = [];
                var refAgente = XLSX.utils.encode_cell({ r: row, c: colMes });
                var celdaAgente = hoja[refAgente];
                var textoAgente = celdaAgente ? (celdaAgente.v !== undefined && celdaAgente.v !== null ? celdaAgente.v.toString() : "") : "";

                // Verificar si es fila vacía o contiene el nombre del mes
                var esFilaVacia = ("" == textoAgente) || (textoAgente.toUpperCase().includes(mesBuscado) && row<2);

                if (esFilaVacia) {
                    filasVacias++;
                } else {
                    filasVacias = 0; //Reseteamos el contador de filas vacías.
                    fila.push({ Text: textoAgente, Bg: "transparent", Comment: "" }); //La primera columna siempre va a ser el Agente.
                    var filaVacia = true;
                    var auxColEnd = colEnd;

                    for (var col = colStart; col <= auxColEnd; ++col) {
                        // Saltar columnas ocultas durante la extracción
                        if (isColumnHidden(col)) {
                            //console.log("Saltando columna oculta en extracción:", col);
                            continue;
                        }

                        var celdaInfoRef = XLSX.utils.encode_cell({ r: row, c: col });
                        var celdaInfo = hoja[celdaInfoRef];
                        var texto = celdaInfo ? (celdaInfo.v !== undefined && celdaInfo.v !== null ? celdaInfo.v.toString() : "") : "";

                        if (texto == textoAgente) {
                            //Cambiamos de mes.
                            auxColEnd++;
                        } else {
                            if ("" != texto) filaVacia = false; //Lo hago para evitar procesar más filas de las necesarias sin especificar un tamaño concreto.                        
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
                if (filasVacias > 5) break; //Evitamos importar muchas filas vacías.                
            }
        } else {
            console.warn("No se encontró el mes o el día. colMes:", colMes, "colDia:", colDia);
        }
        return JSON.stringify(salida); //Esto es lo que voy a recibir en C#
    },

    showJSonTable: function (jsonString) {
        try {
            const data = JSON.parse(jsonString);
            if (Array.isArray(data) && Array.isArray(data[0])) {
                const flat = data.flat();
                console.table(flat);
            } else {
                console.table(data);
            }
            console.log(XLSX.version);
        }
        catch (e) {
            console.error("No se pudo mostrar la tabla: ", e);
        }
    },

    // MEJORADA: Función auxiliar para debugging - Ver columnas ocultas y anchos
    showHiddenColumns: function (bytes) {
        var datosEntrada = new Uint8Array(bytes);
        var libro = XLSX.read(datosEntrada, { type: "array", cellStyles: true });
        var hoja = libro.Sheets[libro.SheetNames[0]];

        if (hoja['!cols']) {
            //console.log("Información de columnas:");
            hoja['!cols'].forEach((col, index) => {
                if (col) {
                    var status = col.hidden ? 'OCULTA' : 'visible';
                    var width = '';

                    if (col.wch !== undefined) {
                        width += ` ancho: ${col.wch.toFixed(2)} caracteres`;
                        if (col.wch < 1) status = 'OCULTA (ancho mínimo)';
                    }
                    if (col.wpx !== undefined) {
                        width += ` (${col.wpx}px)`;
                        if (col.wpx < 8 && status !== 'OCULTA') status = 'OCULTA (ancho mínimo)';
                    }

                    //console.log(`Columna ${index}: ${status}${width}`);
                }
            });
        } else {
            console.log("No hay información de columnas ocultas/visibles");
        }
    }
}
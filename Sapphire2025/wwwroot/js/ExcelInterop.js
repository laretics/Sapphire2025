//Librería de funciones para la importación de gráficos de personal en Excel.
//El ordenador cliente debería tener instalado Excel.

//Auxiliar para obtener el color de una celda sin el flag de transparencia
function getCellColor(rgb) {
    if (!rgb) return "transparent";
    return "#" + (rgb.length > 6 ? rgb.substring(2) : rgb);
}
function getMontString(month) {
    switch (Number(month)) {
        case 1: return "ENERO"; break;
        case 2: return "FEBRERO"; break;
        case 3: return "MARZO"; break;
        case 4: return "ABRIL"; break;
        case 5: return "MAYO"; break;
        case 6: return "JUNIO"; break;
        case 7: return "JULIO"; break;
        case 8: return "AGOSTO"; break;
        case 9: return "SEPTIEMBRE"; break;
        case 10: return "OCTUBRE"; break;
        case 11: return "NOVIEMBRE"; break;
        case 12: return "DICIEMBRE"; break;
        default: return ""; break;
    }
}
window.excelInterop = {
    //bytes: Entrada en bruto del importador.
    //month: Mes de la fecha.
    //day: día de la fecha
    //days: Número de días (o columnas) a procesar.
    extractWorksheetData: async function (bytes, month, day, days) {
        var datosEntrada = new Uint8Array(bytes);
        var libro = XLSX.read(datosEntrada, { type: "array", cellStyles: true, cellComments: true });        
        var hoja = libro.Sheets[libro.SheetNames[0]];
        console.log("Claves de la hoja:", Object.keys(hoja));

        var rango = XLSX.utils.decode_range(hoja['!ref']);

        //Busco la columna del mes
        var mesBuscado = getMontString(month); 
        var colMes = -1;
        var colDia = -1;

        for (var col = rango.s.c; col < rango.e.c; ++col) {
            var refBusca = XLSX.utils.encode_cell({ r: 1, c: col });
            var celdaBusca = hoja[refBusca];
            var textoCandidato = celdaBusca && celdaBusca.v ? celdaBusca.v.toString().trim().toUpperCase() : "";
            //Si encontramos el mes, nos ponemos a buscar el día.
            if (textoCandidato === mesBuscado) {
                colMes = col;
                continue; //El mes nunca va a contener el número del día, así que saltamos a la celda siguiente.
            }            
            if (-1 != colMes) {
                //Buscando el día
                if (textoCandidato && isNaN(Number(textoCandidato))) { break; } //Hemos saltado al mes siguiente. Hay un error.
                //Si el valor de la columna coincide con el día buscado, ya tenemos el índice.
                if (Number(textoCandidato) === day) { colDia = col; }
            }
        }

        var salida = [];
        //console.log("colMes:", colMes, "colDia:", colDia);

        if (-1 != colMes && -1 != colDia) {
            //Límites del bucle de extracción.
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
                    filasVacias = 0; //Reseteamos el contador de filas vacías.
                    fila.push({ Text: textoAgente, Bg: "transparent", Comment: "" }); //La primera columna siempre va a ser el Agente.
                    var filaVacia = true;
                    var auxColEnd = colEnd;
                    for (var col = colStart; col <= auxColEnd; ++col) {                        
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
                if (filasVacias > 3) break; //Evitamos importar muchas filas vacías.                
            }
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
    }
}
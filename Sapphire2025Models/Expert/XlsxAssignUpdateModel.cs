using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Expert
{
    public class XlsxAssignUpdateModel:BasicRequestModel
    {
        public string? ExcelDump { get; set; } //Volcado en JSon del excel.
        public DateTime Date { get; set; } //Fecha de comienzo.
        public int Days { get; set; } //Número de días en el volcado.
        public int TimeOffset { get; set; } //decalaje de minutos en la zona horaria del cliente.
    }
}

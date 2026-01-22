using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeNet2026.DBStorage
{
    internal class DBCirculation
    {
        [Key]
        public int Id { get; set; } //Código interno de la circulación
        public int BlockId { get; set; } //Referencia al bloque que contiene esta circulación
        public string Name { get; set; } = string.Empty;
        public TimeSpan Departure { get; set; }
        public string Comment { get; set; } = string.Empty;
        public string Color0 { get; set; } = string.Empty;
        public string Color1 { get; set; } = string.Empty;
    }
}

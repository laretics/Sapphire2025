using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeNet2026.DBStorage
{
    internal class DBPlan
    {
        [Key]
        public int Id { get; set; } //Id interno del plan.
        public int RautaId { get; set; } //Identificador del Rauta al que pertenece.
        public string PlanId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string Color0 { get; set; } = string.Empty;
        public string Color1 { get; set; } = string.Empty;

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Expert.WorkshiftTemplates
{
    public class WorkTemplateModel : AttTemplateModel
    {
        public List<WorkShiftContentModel>? Content { get; set; } //Trenes y depósitos que contiene este turno.
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Expert
{
    public class WorkShiftRequestModel
    {
        public DateTime Date { get; set; }
        public int Days { get; set; }
        public string? AgentsTableId { get; set; }
        public Guid Id { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Expert
{
	public class WorkShiftAssignationModel
	{
		public string CF { get; set; }
		public DateTime Date { get; set; }
		public string Assignation { get; set; }
		public string Definitive { get; set; }
		public bool IsTD { get; set; }
		public string? SwappingCF { get; set; }
		public WorkShiftAssignationModel()
		{
			CF = "";
			Assignation = "";
			Definitive = "";
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Expert
{
	public class WorkShiftAssignationModel
	{
		public Guid Agent { get; set; }
		public DateTime Date { get; set; }
		public string Assignation { get; set; }
		public string Definitive { get; set; }
		public bool IsTD { get; set; }
		public Guid SwappingAgent { get; set; }
		public string? Comment { get; set; } //Anotación que hace el Jefe de Maquinistas en la hoja de Excel.
		public WorkShiftAssignationModel()
		{			
			Assignation = "";
			Definitive = "";
		}
	}
}

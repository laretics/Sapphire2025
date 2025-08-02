using Sapphire2025Models.Expert.WorkshiftTemplates;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Expert
{
	public class WorkShiftTemplateCollectionModel
	{
		public Guid Id { get; set; }
		public string? Name { get; set; }
		public DateTime Begin { get; set; }
		public string? Comment { get; set; }
		public byte Collective { get; set; }
		public Guid Owner { get; set; }	
		public Dictionary<string,WorkShiftTemplateModel>? Templates { get; set; } //Turnos que contiene este plan.

	}
}

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
		//public Dictionary<string,WorkShiftTemplateModel>? Templates { get; set; } //Turnos que contiene este plan.
		public List<WorkShiftTemplateModel>? Templates { get; set; } //Lista completa de plantillas que contiene este plan.
		/// <summary>
		/// Devuelve el turno correspondiente a este día en base a la palabra clave con el que se invoca.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="day"></param>
		/// <param name="festive"></param>
		/// <returns></returns>
		public WorkShiftTemplateModel? Template(string name, DateTime day, bool festive)
		{
			if(null!=Templates)
			{
				foreach (WorkShiftTemplateModel auxTemplate in Templates)
				{
					if(auxTemplate.Match(name))
					{
						if (auxTemplate.IsEnabled(day.DayOfWeek, festive))
							return auxTemplate;
					}
				}
			}
			return null;
		}
	}
}

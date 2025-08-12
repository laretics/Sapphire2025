using Sapphire2025Models.Expert.WorkshiftTemplates;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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
		public WorkShiftTemplateModel? Template(string name, byte dayPattern)
		{
            if (null != Templates)
            {
                foreach (WorkShiftTemplateModel auxTemplate in Templates)
                {
					if(auxTemplate.Match(name))
					{
						if (auxTemplate.DayOfWeekEnabled == dayPattern)
							return auxTemplate;
                    }
                }
            }
            return null;
        }
		/// <summary>
		/// Añade un template a la colección, pero sólo si no existe otro similar.
		/// </summary>
		/// <param name="template">El nuevo template a meter</param>
		/// <param name="overriding">Si es true, eliminará el template existente previo.</param>
		/// <returns>true si ha logrado añadirlo a la colección</returns>
		public bool Add(WorkShiftTemplateModel template, bool overriding)
		{
			if (null == Templates) return false;
			bool adding = true;
			WorkShiftTemplateModel? existente = this.Template(template.Name ?? "", template.DayOfWeekEnabled);
			if(null!=existente)
			{
				if (overriding)
					Templates.Remove(existente);
				else
					adding = false; //Ya existe un template incompatible, así que no añadirá.
			}
            if (adding) 
				Templates.Add(template);

            return adding;
		}
	
		/// <summary>
		/// Obtiene todos los templates vigentes un día concreto del año.
		/// Se usa para la vista diaria de turnos cubiertos.
		/// </summary>
		/// <param name="day">Fecha</param>
		/// <param name="festive">Indicación de si ese día se considera festivo o no</param>
		/// <returns>Lista de los turnos vigentes ese día</returns>
		public List<WorkShiftTemplateModel> TemplatesByDay (DateTime day, bool festive)
		{
			List<WorkShiftTemplateModel> salida = new List<WorkShiftTemplateModel>();
			if(null!=Templates)
			{
                foreach (WorkShiftTemplateModel auxTemplate in Templates)
                {
                    if (auxTemplate.IsEnabled(day.DayOfWeek, festive))
                        salida.Add(auxTemplate);
                }
            }
			return salida;
		}
	}
}

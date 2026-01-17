using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace TimeNet2026.DBStorage
{
	internal class DBSchedule
	{
		[Key]
		public int Id { get; set; } //Autonumérico para el turno de trabajo
		public int PlanId { get; set; } //Referencia al plan de explotación
		public string Name { get; set; } = string.Empty; //Nombre del turno de trabajo
		public string Comment { get; set; } = string.Empty; //Comentarios del turno de trabajo
		public byte WeekdayMask { get; set; } //Días de la semana en que está operativo este horario.
		public int CoordinateX { get; set; } //Coordenada X en la presentación gráfica
		public int CoordinateY { get; set; } //Coordenada Y en la presentación gráfica

		public string Color1 { get; set; } = "white"; //Color primero del turno
		public string Color2 { get; set; } = "black"; //Color segundo del turno


	}
}

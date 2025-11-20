using System.Drawing;

namespace TimeNet2026
{
	internal interface Entity
	{
		public string name { get; set; } //Nombre de la entidad
		public string comment { get; set; } //Un comentario sobre esta entidad
		public string[] color { get; set; } //Color para representar gráficamente esta entidad
	}
}

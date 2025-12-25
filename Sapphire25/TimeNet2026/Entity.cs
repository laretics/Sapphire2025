using System.Drawing;

namespace TimeNet2026
{
	public interface Entity
	{
		string name { get; set; } //Nombre de la entidad
		string comment { get; set; } //Un comentario sobre esta entidad
		string[] color { get; set; } //Color para representar gráficamente esta entidad
	}
}

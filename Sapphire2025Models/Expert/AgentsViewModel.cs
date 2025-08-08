using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Expert
{
	/// <summary>
	/// Tabla de Agentes para mostrar en las vistas de gráfico mensual.
	/// </summary>
	public class AgentsViewModel
	{
		public AgentsViewModel()
		{
			ColModel = new List<AgentsViewRegisterModel>();
			Name = string.Empty;
		}
		public string Name { get; set; } //Nombre para mostrar en el título o en un encabezado.
		public List<AgentsViewRegisterModel> ColModel { get; set; } //Lista de elementos.
        public bool Collapsed { get; set; }
    }



}

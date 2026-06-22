using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.GMao
{
	public class WorkCatalogModel
	{
		public Guid Id { get; set; } //Código único de este tipo de trabajo
		public bool Atomic{ get; set; } //Un trabajo "atómico" no se puede pausar. Debe empezar y terminar antes de desbloquear el código.
		public string Name { get; set; } = string.Empty; //Nombre descriptivo de este tipo de trabajo
		public string? Comment { get; set; } //Descripción más abundante de este tipo de trabajo
	}
}

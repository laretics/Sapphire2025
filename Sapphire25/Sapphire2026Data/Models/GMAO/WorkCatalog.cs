using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Sapphire2026Data.Models.GMAO
{
	[Table("GMAOWorksCatalog")]
	public class WorkCatalog
	{
		[Key]
		public Guid Id{ get; set; } //Código único de este tipo de trabajo
		public string Name{ get; set; } //Nombre descriptivo de este tipo de trabajo
		public string Comment{  get; set; } //Descripción más abundante de este tipo de trabajo

	}
}

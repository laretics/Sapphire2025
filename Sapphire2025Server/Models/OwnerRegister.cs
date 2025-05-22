using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Sapphire2025Server.Models
{
	[Table("OwnerRegister")]
	public class OwnerRegister
	{
		[Key]
		public Guid Guid { get; set; } //Id del registro

		public Guid OwnerId { get; set; } //Id del propietario al que pertenece este registro
		public string Key { get; set; } //Clave del registro
		public string Value { get; set; } //Valor del registro
	}
}

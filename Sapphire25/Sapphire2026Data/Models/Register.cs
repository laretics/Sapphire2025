using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Sapphire2026.Data.Models
{
	[Table("Register")]
	public class Register
	{
		public Register()
		{		
			Key = string.Empty;
			Value = string.Empty;
		}
		[Key]
		public string Key { get; set; } //Clave del registro
		public string Value { get; set; } //Valor del registro
	}
}

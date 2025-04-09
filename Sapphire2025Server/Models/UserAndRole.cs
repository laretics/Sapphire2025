using Sapphire2025Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sapphire2025Server.Models
{
	/// <summary>
	/// Este objeto contiene una relación de pertenencia de un usuario con un rol.
	/// Existen dos tipos de roles... los primarios y los extendidos.
	/// Los primarios van del 0 al 7 y están contenidos en un tipo enumerado. Estos son los que definen
	/// los permisos que los diferentes usuarios tendrán según su nivel de acceso.
	/// Los extendidos se pueden crear más adelante y permitirán o no otras funciones, consultas y accesos
	/// </summary>
	[Table("UserAndRole")]
	public class UserAndRole
	{
		[Key]
		public string Id { get; set; }
		public string UserId { get; set; }
		public uint RoleId { get; set; }

		[NotMapped]
		public Common.UserRole basicRole 
		{ 
			get
			{
				if (RoleId < 8)
					return (Common.UserRole)RoleId;
				else
					return Common.UserRole.Anonymous;
			}
			set //El momento en que se haga esta asignación se pierde la anterior
			{
				RoleId = (uint)value;
			}
		}
	}
}

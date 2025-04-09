using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
///Conjunto de clases que gestionan los roles y la autorización de acceso a los roles.
namespace Sapphire25Server.Storage
{
	public class UserRole
	{
		[Key]
		public string? UserId { get; set; }
		public string? RoleId { get; set; }			
	}

	public class Role
	{
		[Key]
		public string? Id { get; set; }
		public string? Name { get; set; }
		public string? NormalizedName { get; set; }
		public string? ConcurrencyStamp { get; set; }
	}

}

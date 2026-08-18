using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sapphire2026.Data.Models
{
	/// <summary>
	/// Preferencia abierta de un usuario (clave/valor).
	/// Claves conocidas: locale, theme, …; se pueden añadir otras sin migración.
	/// </summary>
	[Table("UserPreferences")]
	public class UserPreference
	{
		[Key]
		public Guid Id { get; set; }

		[Required]
		[MaxLength(64)]
		public string UserId { get; set; } = string.Empty;

		[Required]
		[MaxLength(64)]
		public string Key { get; set; } = string.Empty;

		[MaxLength(1024)]
		public string Value { get; set; } = string.Empty;

		public DateTime UpdatedUtc { get; set; }
	}
}

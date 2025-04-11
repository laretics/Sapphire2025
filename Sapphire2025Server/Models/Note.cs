using System.ComponentModel.DataAnnotations.Schema;

namespace Sapphire2025Server.Models
{
	[Table("Notes")]
	public class Note
	{
		public Guid Id { get; set; }
		public DateTime TimeStamp { get; set; }
		public Guid UserId { get; set; }
		public string? Text { get; set; }
	}
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Sapphire2026.Data.Models
{
	[Table("BotLogError")]
	public class BotLogError
	{
		[Key]
		public DateTime TimeStamp { get; set; }
		public long SessionId { get; set; }
		public Guid UserId { get; set; }
		public string? Message { get; set; }
		public string? Reason { get; set; }
	}
}

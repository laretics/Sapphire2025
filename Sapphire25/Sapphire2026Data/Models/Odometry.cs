using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sapphire2026Data.Models
{
[Table("Odometry")]
	public class Odometry
	{
		[Key]
		public Guid Guid{ get; set; }
		[Display(Name ="Train Id")]
		public Guid TrainId{ get; set; }
		[Display(Name ="Moment")]
		public DateTime TimeSpan{ get; set; }
		[Display(Name ="Value")]
		public long Odometer{ get; set; }
	}
}

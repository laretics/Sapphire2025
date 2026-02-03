using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Sapphire2026.Data.Models
{
    [Table("Festives")]
    public class Festive
    {
        [Key]
        public DateTime Date { get; set; }
    }
}

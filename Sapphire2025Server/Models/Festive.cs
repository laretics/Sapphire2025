using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Sapphire2025Server.Models
{
    [Table("Festives")]
    public class Festive
    {
        [Key]
        public DateTime Date { get; set; }
    }
}

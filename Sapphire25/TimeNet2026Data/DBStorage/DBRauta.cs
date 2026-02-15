using System.ComponentModel.DataAnnotations;
namespace TimeNet2026Data.DBStorage
{
    public class DBRauta
    {
        [Key]
        public int Id { get; set; } //Id interno del Rauta.
        public Guid HeaderId { get; set; } //Id del header.
        public int TopoStorageId { get; set; } //Id interno del topoStorage.

    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeNet2026.DBStorage
{
    internal class DBRauta
    {
        [Key]
        public int Id { get; set; } //Id interno del Rauta.
        public Guid HeaderId { get; set; } //Id del header.
        public int TopoStorageId { get; set; } //Id interno del topoStorage.

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;

namespace TimeNet2026.DBStorage
{
	internal class DBAsimilation
	{
		[Key]
		public int Id { get; set; } //Código interno.
		public int TopoStorageId { get; set; } //Referencia al almacén de topología.
		public string AsimilationId { get; set; }=string.Empty //Referencia a nivel Onice
		public string Name { get; set; } = string.Empty;
		public string Comment { get; set; } = string.Empty;
		public string Color0 { get; set; } = "black";
		public string Color1 { get; set; } = "black";
		public int MaxSpeed { get; set; }
		public int OriginStationId { get; set; } //Referencia de SqLite

	}
}

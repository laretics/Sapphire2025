using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Aeneas
{
	public class NoteSearchRequestModel : BasicRequestModel
	{
		public Guid? ParentId { get; set; } //Tren del que se hace la nota.
		public byte? Type { get; set; } //Tipo de nota a buscar.
		public Guid? UserId { get; set; } //Usuario que hace la nota.
		public DateTime? FromTimeStamp { get; set; } //Momento desde el que se hace la búsqueda.
		public DateTime? ToTimeStamp { get; set; } //Momento hasta el que se hace la búsqueda.
		public List<string>? Keywords { get; set; } //Lista de palabras clave a buscar.
		public int? TakeMax{ get; set; } //Máximo de registros a importar.

		public NoteSearchRequestModel() : base(Guid.Empty) { }
		public NoteSearchRequestModel(Guid token) : base(token) { }
	}
}

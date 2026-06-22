using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace Sapphire2025Models.Aeneas
{
	public class TrainModel
	{
		public Guid id { get; set; }

		public string name { get; set; }
		public string? nameCloud { get; set; } //Colección de cadenas para poder buscar esta unidad.

		public TrainModel()
		{
			name = string.Empty;
			lastUserInfo = Guid.Empty;
			lastNote = string.Empty;
		}

		//Estado actual en el que se encuentra este tren.
		public Common.TrainStatus lastStatus { get; set; }

		public string lastNote { get; set; }

		//Fecha del último cambio
		public DateTime lastUpdateTime { get; set; }

		//Id del último usuario que interactuó con este tren
		public Guid lastUserInfo { get; set; }

		//Vía de estacionamiento asignada
		public int PlatformId { get; set; }

		//Último lavado del tren
		public DateTime? LastWash{ get; set; }

		[JsonIgnore]
		public List<StatusChangeModel>? StatusChanges { get; set; }

	}
}

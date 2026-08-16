using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Authentication
{
	//Esta clase se usa para el inicio de sesión de los usuarios
	public class UserLoginModel
	{
		public string userName {  get; set; }
		public string password { get; set; }

		/// <summary>Cliente de origen (p. ej. <c>tourmaline</c>). Vacío = Zafiro web.</summary>
		public string? Client { get; set; }

		/// <summary>Guid de la unidad (Tourmaline SystemConfig.TrainId).</summary>
		public string? TrainId { get; set; }

		/// <summary>Nombre de la unidad (p. ej. 81-01).</summary>
		public string? TrainName { get; set; }
	}
}

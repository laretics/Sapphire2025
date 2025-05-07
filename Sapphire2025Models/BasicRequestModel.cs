using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models
{
	/// <summary>
	/// Esto es un modelo básico de petición por parte de un usuario que
	/// está autenticado (y por tanto tiene SessionToken válido)
	/// </summary>
	public class BasicRequestModel
	{
		public Guid SessionToken { get; set; } //Token de sesión del usuario que está realizando la operación
		public BasicRequestModel(Guid token)
		{
			this.SessionToken = token;
		}
		public BasicRequestModel()
		{
			this.SessionToken = Guid.Empty;
		}
	}
}

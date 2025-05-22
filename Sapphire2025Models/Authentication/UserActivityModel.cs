using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Authentication
{
	/// <summary>
	/// Contenedor de datos que refleja la actividad del usuario en el sistema.
	/// </summary>
	public class UserActivityModel
	{
		public UserActivityModel()
		{
			activity = new List<UserActivityAtom>();
		}
		public List<UserActivityAtom> activity { get; set; } //Lista de actividades del usuario
		public class UserActivityAtom
		{
			public DateTime timeStamp { get; set; } //Fecha y hora de la actividad
			public byte type { get; set; } //Tipo de actividad
		}
	}
	public class UserActivityRequest: BasicRequestModel
	{
		public UserActivityRequest()
		{
			userId = Guid.Empty.ToString();
			maxRecords = 0;
		}
		public string userId { get; set; } //Guid del usuario que provocó esta entrada
		public int maxRecords { get; set; } //Número máximo de registros a devolver

	}
}

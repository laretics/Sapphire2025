using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Sapphire2025Models.Expert
{
	[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
	[JsonDerivedType(typeof(AgentsViewSpace),"spc")]
	[JsonDerivedType(typeof(AgentsViewAgent),"agn")]
    [JsonDerivedType(typeof(AgentsViewContainer), "ctn")]
    public abstract class AgentsViewRegisterModel
	{
        public string? Name { get; set; }
    }
	//Separador en la lista
	public class AgentsViewSpace:AgentsViewRegisterModel
	{

	}
	//Agente
	public class AgentsViewAgent:AgentsViewRegisterModel
	{
		public string CF { get; set; }
		public Guid Id { get; set; }		
	}
	//Lista colapsable de Agentes
	public class AgentsViewContainer:AgentsViewRegisterModel
	{
		public List<AgentsViewRegisterModel>? RegisterCollection { get; set; } //Lista de elementos
		public bool Show { get; set; } = true; //Elementos colapsados o desplegados.
		public bool ShowHeader { get; set; } //Se usa de forma local... sirve para mostrar u ocultar el encabezado
	}
}

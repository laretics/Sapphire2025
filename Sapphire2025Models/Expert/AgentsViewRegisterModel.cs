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
    [JsonDerivedType(typeof(AgentsViewSubList), "sbl")]
    public abstract class AgentsViewRegisterModel
	{
        public string Name { get; set; }
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
	public class AgentsViewSubList:AgentsViewRegisterModel
	{
		public AgentsViewModel SubList { get; set; }
		
	}
}

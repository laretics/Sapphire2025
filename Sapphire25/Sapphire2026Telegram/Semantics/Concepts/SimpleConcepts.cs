using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2026Telegram.Semantics.Concepts
{

	internal class ReportRequestConcept:GeneralConcept
	{
		internal ReportRequestConcept(IConfiguration config) :base(config) 
		{
			AddTokens(new string[]
			{"disponible","disponibilidad","disponibles","trenes","disposición",
			"informe","lista"});
		}		
	}
	//internal class CancelConcept:GeneralConcept
	//{

	//}


}

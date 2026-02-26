using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.ScriptCompiling
{
	public class XMLCompileResult
	{
		public bool Success { get; set; }
		public List<XMLCompileWarning> Warnings { get; set; }
		public string Message { get; set; }
		public XMLCompileResult() 
		{ 
			Warnings=new List<XMLCompileWarning>();
			Message =string.Empty;
		}
	}
}

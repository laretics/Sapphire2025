using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeNet2026.ScriptCompiling
{
	public class CompileResult:IDisposable
	{
		public bool Success { get; set; }
		public List<CompileWarning> Warnings { get; set; }
		public string Message { get; set; }
		public CompileResult() 
		{ 
			Warnings=new List<CompileWarning>();
			Message =string.Empty;
		}

		public void Dispose()
		{
			Warnings.Clear();
		}
	}
}

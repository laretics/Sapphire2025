using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.ScriptCompiling
{
	public class XMLCompileWarning
	{
		public enum SeverityEnum:byte
		{
			Note,
			Warning,
			Error,
			Severe,
			Fatal
		}
		public SeverityEnum Severity { get; set; }
		public string Message { get; set; } = string.Empty;
		public int Location { get; set; }

	}
}

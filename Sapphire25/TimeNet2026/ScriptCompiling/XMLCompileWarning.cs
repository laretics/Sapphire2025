using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeNet2026.ScriptCompiling
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
		public XMLCompileWarning():this(string.Empty,-1,SeverityEnum.Note){ }
		public XMLCompileWarning(string message, int location, SeverityEnum severity)
		{
			Message = message;
			Location = location;
			Severity = severity;
		}
	}
}

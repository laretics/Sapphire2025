using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeNet2026.ScriptCompiling
{
	public class CompileWarning
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
		public CompileWarning():this(string.Empty,-1,SeverityEnum.Note){ }
		public CompileWarning(string message, int location, SeverityEnum severity)
		{
			Message = message;
			Location = location;
			Severity = severity;
		}
	}
}

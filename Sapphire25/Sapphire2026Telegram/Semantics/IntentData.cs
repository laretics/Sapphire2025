using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2026Telegram.Semantics
{
	public class IntentData
	{
		[LoadColumn(0)]
		public string Text{ get; set; }
		[LoadColumn(1)]
		public string Label{ get; set; }
	}
}

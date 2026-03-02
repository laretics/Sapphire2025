using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using TimeNet2026.Auxiliar;
using TimeNet2026.ScriptCompiling;

namespace TimeNet2026.Topo
{
	/// <summary>
	/// Esto es una cabecera con información sobre un topo o sobre un rauta.
	/// </summary>
	public class Header
	{
		public string Name { get; set; }
		public string Description { get; set; }
		public string Comment { get; set; }
		public string License { get; set; }
		public string Author { get; set; }
		public DateTime FirstDate { get; set; }
		public DateTime LastDate { get; set; } //Esta será la fecha de entrada en vigor de este conjunto de planes.
		public string Version { get; set; }
		public string Bitmap { get; set; }
		public Guid Id { get; set; }
		public Guid ParentId { get; set; } //De momento sólo lo usan los rautatie

		public Header()
		{
			this.Name = string.Empty;
			this.Description = string.Empty;
			this.Comment = string.Empty;
			this.License = string.Empty;
			this.Author = string.Empty;
			this.Bitmap = string.Empty;
			this.Version = string.Empty;
		}
	}
}

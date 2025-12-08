using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml;
using TimeNet2026.Auxiliar;

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
		public DateTime LastDate { get; set; }
		public string Version { get; set; }
		public string Bitmap { get; set; }
		public Guid Id { get; set; }

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
		internal void deserialize(XmlNode root)
		{
			this.Name = XMLUtil.StringParam(root, "name");
			this.Description = XMLUtil.StringParam(root, "description");
			this.Comment = XMLUtil.StringParam(root, "comment");
			this.License = XMLUtil.StringParam(root, "license");
			this.Author = XMLUtil.StringParam(root, "author");
			this.FirstDate = XMLUtil.DateTimeParam(root, "firstdate");
			this.LastDate = XMLUtil.DateTimeParam(root, "lastdate");
			this.Version = XMLUtil.StringParam(root, "version");
			this.Bitmap = XMLUtil.StringParam(root, "bitmap");
			this.Id = XMLUtil.GuidParam(root, "id");
		}
	}
}

using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Aeneas
{
	public class NoteChatRequestModel:BasicRequestModel
	{
		public Guid ParentId { get; set; } = Guid.Empty;
		public string NoteType { get; set; } = string.Empty;
		public int TakeMax { get; set; } = 0;
		public NoteChatRequestModel() : base(Guid.Empty) { }
		public NoteChatRequestModel(Guid token, Guid parent, string type,int takeMax ):base(token) 
		{
			ParentId = parent;
			NoteType = type;
			TakeMax = takeMax;
		}
	}
}

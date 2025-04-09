using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JerarquiaTaller.Logic
{
    /// <summary>
    /// Molde para crear un objeto
    /// </summary>
    public class ClassComponent
    {
        public ClassComponent(string id, string name,string? comment=null)
        {
            Children = new List<ReferenceInstance>();
            Magnitudes = new List<Magnitude>(); 
            this.Name = name.Trim();
            this.StorageId = id.Trim();
            this.Comment = comment;
        }

        public string StorageId { get;private set; } //Referencia de almacén
        public string Name { get; private set; }
        public string? Comment { get; private set; }

        /// <summary>
        /// Componentes que contiene este elemento (puede ser una lista vacía)
        /// </summary>
        public List<ReferenceInstance> Children { get; private set; }
        public List<Magnitude> Magnitudes { get; private set; }


    }
}

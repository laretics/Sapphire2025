using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JerarquiaTaller.Logic
{
    /// <summary>
    /// Contiene una referencia a un elemento de una determinada clase contenido
    /// </summary>
    public class ReferenceInstance
    {
        public ClassComponent Class { get; private set; }
        public string Name { get; private set; } //Rol que adopta este elemento en la referencia
        public string? comment { get; private set; } //Comentario

        public ReferenceInstance(ClassComponent parent, string name, string? comment=null)
        {
            this.Class = parent;
            this.Name = name;
            this.comment = comment;
        }
    }
}

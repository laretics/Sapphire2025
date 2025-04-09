using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JerarquiaTaller.Logic
{
    public class Magnitude
    {
        public string name { get; private set; }
        public string? comment { get; private set; }
        public string units { get; private set; }
        public float min { get; private set; }
        public float max { get; private set; }

        public Magnitude(string name, float min, float max,string units,string? comment = null)
        {
            this.name = name;
            this.min = min;
            this.max = max;
            this.units = units;
            this.comment = comment;
        }
    }
}

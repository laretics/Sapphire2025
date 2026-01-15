using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeNet2026.Topo;

namespace TimeNetComponents.Controls.TimeNetControl
{
    public class TimenetEnvironmentY
    {
        public int Height { get; set; }
        public bool PkMode { get; set; } = true;
        internal int LowerMargin => Height - 40;
        internal int UpperMargin = 20;
        internal int MiddleSize => LowerMargin - UpperMargin;
        internal int DownSize => Height - LowerMargin;
        

        internal long mvarMinPk;
        internal long mvarMaxPk;
        internal AsimilationView? mvarView;

        public TimenetEnvironmentY(int height)
        {
            this.Height = height;

        }
        internal double GetY(Station? station)
        {
            StationViewRef? punto = GetReference(station);
            {
                if (null != punto)
                {
                    System.Diagnostics.Debug.Assert(null != mvarView);
                    double auxValor = -1;
                    if (PkMode && mvarView.MaxPk > 0)
                        auxValor = UpperMargin + (LowerMargin - UpperMargin) * punto.ViewPk / mvarView.MaxPk;
                    else if (mvarView.MaxIndex > 0)
                        auxValor = UpperMargin + (LowerMargin - UpperMargin) * punto.Index / mvarView.MaxIndex;

                    if (auxValor >= 0)
                        return auxValor;
                }
            }
            return -1;
        }
        internal StationViewRef? GetReference(Station? station)
        {
            if (null != mvarView && null != station && mvarView.Elements.ContainsKey(station))
                return mvarView.Elements[station];
            return null;
        }
    }
}

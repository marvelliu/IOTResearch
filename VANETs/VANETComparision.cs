using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VANETs
{
    class VANETComparision
    {
        public static Comparison<RSUEntity> HopComparior =
            delegate(RSUEntity p1, RSUEntity p2)
            {
                return p1.hops.CompareTo(p2.hops);
            };

        public static Comparison<RSUEntity> NeighborComparior =
            delegate(RSUEntity p1, RSUEntity p2)
            {
                return p1.nbs.CompareTo(p2.nbs);
            };

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace HeterogeneousForward
{
    class SWNeighbor:Neighbor
    {
        public ForwardStrategy[] ClaimedForwardStrategy;

        public SWNeighbor(Reader node)
            : base(node)
        {
            this.ClaimedForwardStrategy = null;
        }
    }
}

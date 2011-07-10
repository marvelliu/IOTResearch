using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace HeterogeneousForward
{
    class HFNeighbor:Neighbor
    {
        public ForwardStrategy[] ClaimedForwardStrategy;

        public HFNeighbor(Reader node)
            : base(node)
        {
            this.ClaimedForwardStrategy = null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace LocationPrivacy
{
    public class PrivacyNeighbor:Neighbor
    {
        public double angle;
        public double dist;

        public PrivacyNeighbor(Reader node): base(node)
        {
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;
using System.IO;
using System.Diagnostics;

namespace HeterogeneousForward
{
    class HFOrganization:Organization
    {
        new public static HFOrganization ProduceOrganization(int id, string name)
        {
            return new HFOrganization(id, name);
        }

        public HFOrganization(int id, string name)
            : base(id, name)
        {
        }


    }
}

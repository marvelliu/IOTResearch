using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrustArch
{
    class IOTMonitorReader:IOTReader
    {
        new public static IOTReader ProduceReader(int id, int org)
        {
            return new IOTReader(id, org);
        }
        public IOTMonitorReader(int id, int org)
            : base(id, org)
        { }

    }
}

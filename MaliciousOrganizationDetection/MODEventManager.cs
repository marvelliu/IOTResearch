using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace MaliciousOrganizationDetection
{
    class MODEventManager:EventManager
    {
        public override void ParseEventArgs(string[] array)
        {
            MODGlobal global = (MODGlobal)Global.getInstance();
            if (array[0] == "SND_DATA")
            {
                //TODO
            }
            else
            {
                base.ParseEventArgs(array);
            }
        }

    }
}

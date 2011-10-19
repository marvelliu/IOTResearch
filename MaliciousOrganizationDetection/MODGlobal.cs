using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace MaliciousOrganizationDetection
{
    public class MODGlobal:Global
    {
        public new MainForm mainForm = null;


        new public static MODGlobal ProduceGlobal()
        {
            return new MODGlobal();
        }

        protected MODGlobal()
        {
        }

        override public void ParseArgs(string[] v)
        {
            if (v[0] == "minSrcDstDist")
            { 
            }
            else
                base.ParseArgs(v);
        }

    }
}

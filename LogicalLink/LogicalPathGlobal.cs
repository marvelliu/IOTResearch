using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocForwardGUI;

namespace LogicalPath
{
    class LogicalPathGlobal : AdHocForwardGUI.Global
    {
        public float ReversePathCacheTimeout = 10;

        new protected static LogicalPathGlobal instance;

        protected LogicalPathGlobal()
        { }

        new public static LogicalPathGlobal getInstance()
        {
            if (instance == null)
                instance = new LogicalPathGlobal();
            return instance;
        }
    }
}

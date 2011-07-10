using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrustArch
{

    public delegate IOTServer ServerConstructor();

    public class IOTServer
    {
        public List<IOTReader> BackboneNodeDB;
        public Dictionary<int, int> BackboneNodeMapping;

        private IOTServer()
        {
            this.BackboneNodeDB = new List<IOTReader>();
            this.BackboneNodeMapping = new Dictionary<int, int>();
        }

        private static IOTServer instance;
        public static IOTServer getInstance()
        {
            if (instance == null)
                instance = new IOTServer();
            return instance;
        }
    }
}

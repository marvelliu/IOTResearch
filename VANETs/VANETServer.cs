using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VANETs
{

    public delegate VANETServer ServerConstructor();

    public class VANETServer
    {
        public List<VANETReader> BackboneNodeDB;
        public Dictionary<int, int> BackboneNodeMapping;

        private VANETServer()
        {
            this.BackboneNodeDB = new List<VANETReader>();
            this.BackboneNodeMapping = new Dictionary<int, int>();
        }

        private static VANETServer instance;
        public static VANETServer getInstance()
        {
            if (instance == null)
                instance = new VANETServer();
            return instance;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace VANETs
{
    public enum NetworkGenMethods
    {
        Random,
        MaxHops,
        HalfHops,
        MaxNeigbhors
    }

    public class VANETGlobal:Global
    {
        public new MainForm mainForm = null;
        public NetworkGenMethods vanetNetworkGenMethod = NetworkGenMethods.Random;


        new public static VANETGlobal ProduceGlobal()
        {
            return new VANETGlobal();
        }

        protected VANETGlobal()
        {
        }

        override public void ParseArgs(string[] v)
        {
            if (v[0] == "vanet_network_size")
                vanetNetworkSize = int.Parse(v[1]);
            else if (v[0] == "ca_forward")
                vanetCaForward = bool.Parse(v[1]);
            else if (v[0] == "wired_proportion")
                wiredProportion = float.Parse(v[1]);
            else if (v[0] == "check_cert_delay")
                checkCertDelay = float.Parse(v[1]);
            else if (v[0] == "vanet_network_generation_method")
                vanetNetworkGenMethod = (NetworkGenMethods)Enum.Parse(typeof(NetworkGenMethods), v[1]);
            else
                base.ParseArgs(v);
        }

    }
}

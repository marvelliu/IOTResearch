using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace LocationPrivacy
{
    public class PrivacyGlobal:Global
    {
        public new MainForm mainForm = null;
        public double includedAngle = 3.14 / 3;
        public float waitChildDelay = 0.2f;
        public float checkNewGroupTimeout = 3f;
        public int longTTL = 6;

        public float native2WaitingTimeout = 3f;

        public int nativeMethod = 0;


        new public static PrivacyGlobal ProduceGlobal()
        {
            return new PrivacyGlobal();
        }

        protected PrivacyGlobal()
        {
        }

        override public void ParseArgs(string[] v)
        {
            if (v[0] == "longTTL")
                longTTL = int.Parse(v[1]);
            else if (v[0] == "checkNewGroupTimeout")
                checkNewGroupTimeout = float.Parse(v[1]);
            else if (v[0] == "native")
                nativeMethod = int.Parse(v[1]);
            else
                base.ParseArgs(v);
        }

    }
}

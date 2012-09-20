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

        //1: native
        //2: native2
        //3: our improved approach
        public int method = 1;

        public bool isBuildGroupPolicyStrict = false;


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
            else if (v[0] == "build_group_policy_strict")
                isBuildGroupPolicyStrict = bool.Parse(v[1]);
            else if (v[0] == "method")
                method = int.Parse(v[1]);
            else
                base.ParseArgs(v);
        }

    }
}

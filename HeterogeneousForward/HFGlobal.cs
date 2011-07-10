using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace HeterogeneousForward
{
    public class HFGlobal:Global
    {
        public new MainForm mainForm = null;

        public bool ignoreForwardStatagy = false;
        public int tagNameNum = 10;
        public int maxNearCandidateNum = 5;
        public int maxStoredNearCandidateNum = 15;
        public int choosenNearCandidateNum = 5;

        public float checkCandidateInterval = 5;

        public int maxSwHubHops = 4;
        public int minSWHubRequiredTags = 5;

        public ForwardStrategyAction defaultForwardStrategyAction;

        new public static HFGlobal ProduceGlobal()
        {
            return new HFGlobal();
        }

        protected HFGlobal()
        {
            routeMethod = RouteMethod.SmallWorld;
        }

        override public void ParseArgs(string[] v)
        {
            if (v[0] == "defaultForwardAction")
            {
                if (v[1] == "accept")
                    defaultForwardStrategyAction = ForwardStrategyAction.ACCEPT;
                else
                    defaultForwardStrategyAction = ForwardStrategyAction.REFUSE;
            }
            else if (v[0] == "ignore_forward_statagy")
                ignoreForwardStatagy = bool.Parse(v[1]);
            else
                base.ParseArgs(v);
        }

    }
}

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

        public float checkSWHubCandidateInterval = 6;

        public int maxSwHubHops = 4;
        public int minSWHubRequiredTags = 5;
        public int minSwHubAvailTagThrethold = 9;
        public int minSwHubNeighbors = 5;
        public int maxSwHubs = 10;

        public int innerSWTTL = 3;
        public int outerSWTTL = 4;

        public int swTTL = 3;

        public double swHubRatio = 0.1;
        public int currentSWHubNumber = 0;

        public bool aggressivelyLookForSwHub = false;

        public uint currentSendingTags = 0; //用于画图时将一些允许标签的节点画出来

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
            else if (v[0] == "minSwHubAvailTagThrethold")
                minSwHubAvailTagThrethold = int.Parse(v[1]);
            else if (v[0] == "innerSWTTL")
                innerSWTTL = int.Parse(v[1]);
            else if (v[0] == "outterSWTTL")
                outerSWTTL = int.Parse(v[1]);
            else if (v[0] == "aggressivelyLookForSwHub")
                aggressivelyLookForSwHub = bool.Parse(v[1]);
            else
                base.ParseArgs(v);
        }

    }
}

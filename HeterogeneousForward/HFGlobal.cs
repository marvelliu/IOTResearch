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

        public int clusterHops = 3;


        public bool ignoreForwardStatagy = false;
        public int tagNameNum = 10;
        public int maxNearCandidateNum = 5;
        public int maxStoredNearCandidateNum = 15;
        public int choosenNearCandidateNum = 5;

        public float checkSWHubCandidateInterval = 6;

        public int maxSwHubHops = 4;
        public int minSWHubRequiredTags = 5;
        public int minSwHubAvailTagThrethold = 9;
        public int minSwHubNeighbors = 1;
        public int maxSwHubs = 10;
        public double maxSwHubRatio = 0.2;

        public int innerSWTTL = 3;
        public int outerSWTTL = 4;

        public int CBTTL = 3;

        public int swTTL = 3;

        public int clusterRadius = 2;

        public int currentSWHubNumber = 0;

        //如果swhub找不到对方的swhub，是否发送aodv寻找
        public bool aggressivelyLookForSwHub = false;

        //最多发送的标签数
        public int SendEventMinTagNum = 2;
        public int SendEventMaxTagNum = 2;

        public uint currentSendingTags = 0; //用于画图时将一些允许标签的节点画出来

        public bool printTopology = false;
        public bool printIdealSucc = false;
        public bool smartBeacon = true;

        public double minSrcDstDist = 800;

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
            else if (v[0] == "minSrcDstDist")
                minSrcDstDist = double.Parse(v[1]);
            else if (v[0] == "ignore_forward_statagy")
                ignoreForwardStatagy = bool.Parse(v[1]);
            else if (v[0] == "minSwHubAvailTagThrethold")
                minSwHubAvailTagThrethold = int.Parse(v[1]);
            else if (v[0] == "innerSWTTL")
                innerSWTTL = int.Parse(v[1]);
            else if (v[0] == "outerSWTTL")
                outerSWTTL = int.Parse(v[1]);
            else if (v[0] == "swTTL")
                swTTL = int.Parse(v[1]);
            else if (v[0] == "maxSwHubRatio")
                maxSwHubRatio = double.Parse(v[1]);
            else if (v[0] == "maxSwHubs")
                maxSwHubs = int.Parse(v[1]);
            else if (v[0] == "aggressivelyLookForSwHub")
                aggressivelyLookForSwHub = bool.Parse(v[1]);
            else if (v[0] == "smartBeacon")
                smartBeacon = bool.Parse(v[1]);
            else if (v[0] == "printTopology")
                printTopology = bool.Parse(v[1]);
            else if (v[0] == "printIdealSucc")
                printIdealSucc = bool.Parse(v[1]);
            else if (v[0] == "clusterHops")
                clusterHops = int.Parse(v[1]);                
            else
                base.ParseArgs(v);
        }

    }
}

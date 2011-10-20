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


        public double SmallValue = 0.001;

        public double recvLikehood = 0.7;//如果一个节点发出数据包，那么对方成功接收的似然值
        public double sendLikehood = 0.9;//如果接收到一个数据包，那么源节点发出的似然值
        public double checkTimeoutPhenomemonLikehood = 0.7; //如果检测到一个超时的数据包未被发送，其似然值

        public float checkEventTimeout = 4;
        public float checkPhenomemonTimeout = 2;
        public float checkNodeTimeout = 8;
        public float checkNodeTypeTimeout = 16;

        public double totalPacketThreahold = 1000;
        public double nodeSpeedThreahold = 15;
        public double sendPacketTimeout = 1.5f;

        public double NormalBelief = 0.3;
        public double NormalPlausibility = 0.7;


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

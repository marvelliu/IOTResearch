using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace MaliciousOrganizationDetection
{
    public enum DeduceMethod
    {
        Native = 0,
        Game,
        OrgGame,
    }
    public class MODGlobal:Global
    {
        public new MainForm mainForm = null;


        public double SmallValue = 0.001;

        public double recvLikehood = 0.7;//如果一个节点发出数据包，那么对方成功接收的似然值
        public double sendLikehood = 0.9;//如果接收到一个数据包，那么源节点发出的似然值
        public double checkTimeoutPhenomemonLikehood = 0.7; //如果检测到一个超时的数据包未被发送，其似然值

        public float checkEventTimeout = 4;
        public float checkPhenomemonTimeout = 2;
        public float checkReceivedPacketTimeout = 2;
        public float checkNodeTimeout = 8;
        public float checkNodeTypeTimeout = 16;

        public double totalPacketThreahold = 1000;
        public double nodeSpeedThreahold = 15;
        public double sendPacketTimeout = 1.5f;

        public double NormalBelief = 0.3;
        public double NormalPlausibility = 0.7;

        //public DeduceMethod deduceMethod = DeduceMethod.Native;
        public DeduceMethod deduceMethod = DeduceMethod.Game;


        //对于恶意的报告节点所得
        public double uA1MaliciousAndSupportAndAccept = -1f;
        public double uA1MaliciousAndSupportAndReject = 0.5f;
        public double uA1MaliciousAndNonsupportAndAccept = -0.5f;
        public double uA1MaliciousAndNonsupportAndReject = 0.5f;


        //对于检测节点所得
        public double uA2NormalAndSupportAndAccept = 1f;
        public double uA2NormalAndSupportAndReject = 0.3f;
        public double uA2NormalAndNonsupportAndAccept = 0.5f;
        public double uA2NormalAndNonsupportAndReject = 0.2f;
        public double uA2MaliciousAndSupportAndAccept = -1f;
        public double uA2MaliciousAndSupportAndReject = 0.5f;
        public double uA2MaliciousAndNonsupportAndAccept = -0.5f;
        public double uA2MaliciousAndNonsupportAndReject = 0.5f;

        public int BufSize = 2048;


        public double pInitNormal = 0.5f;
        public double pInitSupportByNormal = 0.2f;
        public double pInitNonsupportByNormal = 0.8f;
        public double pInitSupportByMalicious = 0.8f;
        public double pInitNonsupportByMalicious = 0.2f;

        public double PunishmentFactor = 0.8f;
        public double RewardFactor = 1.0f;

        //某机构报告与自己最大的差异，超过则可疑
        public double MaxReportDistance = 1f;
        //机构之间一致性最大值，超过则可能出现恶意机构
        public double MaxTotalOrgVariance = 1f;

        public int MaxSuspectedCount = 3;

        public double SuspectedPunishFactor = 0.8f;

        public int MaxHistoryCount = 5;

        //用最小二乘法预测节点方差，如果超过阈值则认为可能是有问题的
        public double MaxNormalVariance = 1f;


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

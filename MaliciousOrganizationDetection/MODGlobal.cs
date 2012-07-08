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
        CoOrgGame,
    }
    public class MODGlobal:Global
    {
        public new MainForm mainForm = null;


        public double SmallValue = 0.001;

        public double recvLikehood = 0.7;//如果一个节点发出数据包，那么对方成功接收的似然值
        public double sendLikehood = 0.9;//如果接收到一个数据包，那么源节点发出的似然值
        public double checkTimeoutPhenomemonLikehood = 0.7; //如果检测到一个超时的数据包未被发送，其似然值

        public float checkEventTimeout = 4;
        public float checkPhenomemonTimeout = 1;
        public float checkReceivedPacketTimeout = 2;
        public float checkNodeTimeout = 8;
        public float checkNodeTypeTimeout = 16;

        public double totalPacketThreahold = 1000;
        public double nodeSpeedThreahold = 20;
        public double sendPacketTimeout = 1.5f;

        public double NormalBelief = 0.4;
        public double NormalPlausibility = 0.7;

        //public DeduceMethod deduceMethod = DeduceMethod.Native;
        //public DeduceMethod deduceMethod = DeduceMethod.Game;
        public DeduceMethod Step2DeduceMethod = DeduceMethod.OrgGame;
        public DeduceMethod Step1DeduceMethod = DeduceMethod.OrgGame;

        public bool Interactive = false;



        //对于恶意的报告节点所得
        //由于使用博弈的必定是恶意节点，故我本身是恶意节点
        //恶意节点的收益有两部分，一部分是信誉值的变化(+0.2,-0.3)，另一部分是恶意行为所带来的收益(0.5,0)
        public double uA1MaliciousAndSupportAndAccept = 0.7f;//事件是恶意的，我认为是正常的，检测节点接受(本次作恶赢得了额外的利益)
        public double uA1MaliciousAndSupportAndReject = -0.5f;//事件是恶意的，我认为是正常的，检测节点拒绝(本次作恶没有利益，相反被降低了信誉值)
        public double uA1MaliciousAndNonsupportAndAccept = 0.2f;//事件是恶意的，我认为也是恶意的，检测节点接受(本次没有作恶，增加了信誉值）
        public double uA1MaliciousAndNonsupportAndReject = -1f;//事件是恶意的，我认为也是恶意的，检测节点拒绝(本次没有作恶，相反被降低了信誉值，所以最不可能做)
        
        public double uA1NormalAndSupportAndAcceptAndISupport = 0.2f;//事件是正常的，整体报告认为也是正常的，检测节点接受，我也认为是正常的(本次没有作恶，收益是信誉值增加了一点点)
        public double uA1NormalAndSupportAndAcceptAndINonsupport = 0.2f;//事件是正常的，整体报告认为也是正常的，检测节点接受，我认为是异常的(本次作恶，有收益，但惩罚信誉值降低)
        public double uA1NormalAndSupportAndReject = -1f;//事件是正常的，我认为也是正常的，检测节点拒绝整体(本次没有作恶，相反被降低了信誉值，所以最不可能做)
        public double uA1NormalAndNonsupportAndAccept = 0.7f;//事件是正常的，我认为是恶意的，检测节点接受(本次作恶赢得了额外的利益）
        public double uA1NormalAndNonsupportAndReject = -0.5f;//事件是正常的，我认为是恶意的，检测节点拒绝(本次作恶没有成功)



        //对于检测节点所得
        //此处SupportM为"支持事件为恶意节点"
        public double uA2DropAndNormalAndSupportMDAndAccept = 0.5f;//恶意事件，报告节点为正常节点，支持是恶意报告，接受风险较少
        public double uA2FwrdAndNormalAndSupportMDAndAccept = -0.5f;//接受正常报告，风险较少
        public double uA2DropAndNormalAndSupportMDAndReject = -0.5f;
        public double uA2FwrdAndNormalAndSupportMDAndReject = 0.5f;
        public double uA2DropAndNormalAndNonsupportMDAndAccept = -0.5f;
        public double uA2FwrdAndNormalAndNonsupportMDAndAccept = 0.5f;
        public double uA2DropAndNormalAndNonsupportMDAndReject = 0.5f;
        public double uA2FwrdAndNormalAndNonsupportMDAndReject = -0.5f;
        public double uA2DropAndMaliciousAndSupportMDAndAccept = 0.5f;
        public double uA2FwrdAndMaliciousAndSupportMDAndAccept = -0.5f;
        public double uA2DropAndMaliciousAndSupportMDAndReject = -0.5f;
        public double uA2FwrdAndMaliciousAndSupportMDAndReject = 0.5f;
        public double uA2DropAndMaliciousAndNonsupportMDAndAccept = -0.5f;
        public double uA2FwrdAndMaliciousAndNonsupportMDAndAccept = 0.5f;
        public double uA2DropAndMaliciousAndNonsupportMDAndReject = 0.5f;
        public double uA2FwrdAndMaliciousAndNonsupportMDAndReject = -0.5f;

        public double pInitDropBySupportM = 0.5;
        public double pInitDropByNonsupportM = 0.5;
        public double pMaxDropBySupportM = 0.9;
        public double pMinDropByNonsupportM = 0.1;
        public double pDropBySupportMFactor = 0.5;
        
        public int BufSize = 2048;


        public double pInitDrop = 0.5f; //此处真实设置在机构比例处设置
        public double pInitNormal = 0.6f;
        public int pInitIterationNormal = 1;
        public int pInitIterationMalicious = 1;
        public int pInitIterationSupportM = 1;
        public int pInitIterationNonsupportM = 1;

        public double PunishmentFactor = 0.8f;
        public double RewardFactor = 1.0f;

        //某机构报告与自己最大的差异，超过则可疑
        public double MaxReportDistance = 0.10f;
        //某个机构内部的一致性
        public double MinReportVariance = 0.12f;
        //机构之间一致性最大值，超过则可能出现恶意机构
        public double MaxTotalOrgVariance = 0.1f;

        public int MaxSuspectedCount = 3;

        public double SuspectedPunishFactor = 0.8f;

        public double AdjustFactor = 0.8;
        public double SuspectedCountBase = 6f;
        public double VarianceBase = 1.1;
        public double HistoryVSDBase = 6f;


        //默认情况下恶意节点是否抛弃数据包
        public bool DropData = false;

        public int MaxHistoryCount = 5;

        //用最小二乘法预测节点方差，如果超过阈值则认为可能是有问题的
        public double MaxNormalVariance = 1f;

        //统计过去一段时间内某节点的恶意事件频率
        public int maxCountPeriod = 5;

        public int MaxReportCount = 15;


        //每个时期只能判断一个恶意事件
        public string currentPkgIdent = "";
        public double currentPkgIdentUpdate = -10;

        public double ReportMinDist = 0.1f;

        //为了缩小范围，只检测特定的节点，如检测节点1，则会考察节点1发送到其他节点时，其他节点是否drop
        public HashSet<int> monitoredNodes = new HashSet<int>();



        new public static MODGlobal ProduceGlobal()
        {
            return new MODGlobal();
        }

        protected MODGlobal()
        {
        }

        override public void ParseArgs(string[] v)
        {
            if (v[0] == "Step1DeduceMethod")
            {
                Step1DeduceMethod = (DeduceMethod)Enum.Parse(typeof(DeduceMethod), v[1]);
            }
            else if (v[0] == "Step2DeduceMethod")
            {
                Step2DeduceMethod = (DeduceMethod)Enum.Parse(typeof(DeduceMethod), v[1]);
            }
            else if (v[0] == "AdjustFactor")
            {
                AdjustFactor = double.Parse(v[1]);
            }
            else if (v[0] == "SuspectedCountBase")
            {
                SuspectedCountBase = double.Parse(v[1]);
            }
            else if (v[0] == "VarianceBase")
            {
                VarianceBase = double.Parse(v[1]);
            }
            else if (v[0] == "HistoryVSDBase")
            {
                HistoryVSDBase = double.Parse(v[1]);
            }
            else if (v[0] == "DropData")
            {
                DropData = bool.Parse(v[1]);
            }
            else if (v[0] == "Interactive")
            {
                Interactive = bool.Parse(v[1]);
            }
            else if (v[0] == "org_func")
            {
                if (v[1] == "Poisson")
                    orgGenType = OrgGenType.Poisson;
                else if (v[1] == "AVG")
                    orgGenType = OrgGenType.AVG;
                else if (v[1] == "CUS1")
                {
                    orgGenType = OrgGenType.CUS1;
                    orgRatio = new double[orgNum];

                    for (int i = 0; i < v.Length - 2; i++)
                    {
                        double ratio = double.Parse(v[i + 2]);
                        orgRatio[i] = ratio;
                    }
                }
            }
            else
            {
                base.ParseArgs(v);
                return;
            }
            Console.WriteLine(v[0] + ":" + v[1]);
        }

    }
}

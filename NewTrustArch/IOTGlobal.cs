using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;
using System.IO;
using System.Diagnostics;

namespace NewTrustArch
{
    class IOTGlobal:Global
    {
        
        new public static Global ProduceGlobal()
        {
            return new IOTGlobal();
        }

        protected IOTGlobal() {
        }
        

        public TrustManagerConstructor trustManagerConstructor;

        public IOTTrustManager trustManager = null;
        
        public float checkEventTimeout = 4;
        public float checkPhenomemonTimeout = 2;
        public float checkNodeTimeout = 8;
        public float checkNodeTypeTimeout = 16;

        public double MaliciouslyBelief = 0.3;
        public double MaliciouslyPlausibility = 0.7;
        public double NodeFaultyBelief = 0.3;
        public double NodeFaultyPlausibility = 0.7;
        public double EnvironmentFaultBelief = 0.3;
        public double EnvironmentFaultPlausibility = 0.7;
        public double NormalBelief = 0.3;
        public double NormalPlausibility = 0.7;

        public double BeliefThrehold = 0.3;
        public double PlausibilityThrehold = 0.7;

        public double ForgetFactor = 0.1;
        public double InitTrust = 0.9;
        public double TrustThrehold = 0.6;

        public int ConfirmedThrehold = 1;//被确定的行为数量阈值
        public double CofirmedRateThrehold = 0.2;//被确定的行为占总的行为的比例阈值

        public double recvLikehood = 0.7;//如果一个节点发出数据包，那么对方成功接收的似然值
        public double sendLikehood = 0.9;//如果接收到一个数据包，那么源节点发出的似然值
        public double checkTimeoutPhenomemonLikehood = 0.7; //如果检测到一个超时的数据包未被发送，其似然值

        public double totalPacketThreahold = 1000;
        public double nodeSpeedThreahold = 15;
        public double sendPacketTimeout = 1.5f;

        public double SmallValue = 0.001;
        public int BufSize = 2048;

        //恶意机构的编号、每个恶意机构的恶意节点数量
        public int[] badOrgs;
        public double[] badNodeRates;
        public ReaderType[] badNodeTypes;
        public int badOrgNum;

        public override bool LoadConfigFile(string filename)
        {
            base.LoadConfigFile(filename);

            string line = null;
            StreamReader sr = null;
            string[] seperators = { "\t", " ", ":" };

            sr = new StreamReader(filename);
            for (line = sr.ReadLine(); line != null; line = sr.ReadLine())
            {
                if (line[0] == '#')
                    continue;
                string[] v = line.Split(seperators, StringSplitOptions.RemoveEmptyEntries);                
                ParseArgs(v);
            }
            if (sr != null)
                sr.Close();
            return true;

        }
        override public void ParseArgs(string[] v)
        {
            if (v[0] == "check_event_timeout")
                checkEventTimeout = float.Parse(v[1]);
            else if (v[0] == "check_phenomemon_timeout")
                checkPhenomemonTimeout = float.Parse(v[1]);
            else if (v[0] == "check_node_timeout")
                checkNodeTimeout = float.Parse(v[1]);
            else if (v[0] == "set_bad_nodes")
            {
                if (orgNum == 0)
                    throw new Exception("Org num not correct");
                if (badOrgs == null)
                {
                    badOrgs = new int[orgNum];
                    badNodeRates = new double[10];
                    badNodeTypes = new ReaderType[10];
                }
                string[] a = v[1].Split(new char[]{'-'});
                badOrgs[badOrgNum] = int.Parse(a[0]);
                badNodeRates[badOrgNum] = double.Parse(a[1]);
                badNodeTypes[badOrgNum] = IOTReader.ParseReaderType(a[2]);
                badOrgNum++;
            }
            else
                base.ParseArgs(v);
        }
    }
}

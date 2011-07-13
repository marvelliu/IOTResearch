using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using TrustArch;

namespace NewTrustArch
{

    public delegate IOTTrustManager TrustManagerConstructor();

    public class IOTTrustManager:Node
    {
        private IOTGlobal global;
        Dictionary<int, IOTOrganizationTrust> orgReputations;
        List<IOTOrganizationTrust> sortedOrgReputations;

        List<IOTNodeTrustTypeResult> cachedNodeTrustTypeResult;

        protected IOTTrustManager(int id)
            : base(id)
        {
            this.global = (IOTGlobal)Global.getInstance();
            this.type = NodeType.TRUST_MANAGER;
            this.cachedNodeTrustTypeResult = new List<IOTNodeTrustTypeResult>();
            this.orgReputations = new Dictionary<int,IOTOrganizationTrust>();
            this.sortedOrgReputations = new List<IOTOrganizationTrust>();

            double initTrust = global.InitTrust;
            foreach (Organization org in global.orgs)
            {
                this.orgReputations.Add(org.Id, new IOTOrganizationTrust(org.Id, initTrust));
            }
            this.sortedOrgReputations.AddRange(this.orgReputations.Values);
            CheckRoutine();
        }

        protected static IOTTrustManager instance;
        public static IOTTrustManager getInstance()
        {
            if (instance == null)
                instance = new IOTTrustManager(0);
            return instance;
        }


        public override void Recv(Packet pkg)
        {

            switch (pkg.Type)
            {
                case PacketType.NODE_TYPE_REPORT:
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    ReceiveNodeTypeTrustReport(pkg);
                    break;
                default:
                    throw new Exception("Org receives unknown packet type: " + pkg.Type);
            }
        }

        public void ReceiveNodeTypeTrustReport(Packet pkg)
        {
            MemoryStream ms = new MemoryStream(pkg.TrustReport.result);
            BinaryFormatter formatter = new BinaryFormatter();
            List<IOTNodeTrustTypeResult> result = (List<IOTNodeTrustTypeResult>)formatter.Deserialize(ms);
            foreach (IOTNodeTrustTypeResult r in result)
                this.cachedNodeTrustTypeResult.Add(r);
        }

        public void CheckRoutine()
        {
            IOTGlobal g = (IOTGlobal)global;
            double forgetFactor = g.ForgetFactor;
            for (int i = 0; i < g.orgNum; i++)
            {
                double v = this.orgReputations[i].trustValue;
                v = Math.Min(v + forgetFactor, g.InitTrust);
                this.orgReputations[i].trustValue = v;
            }
            if (this.cachedNodeTrustTypeResult.Count > 0)
            {
                //先将节点报告根据节点分类
                Dictionary<int, List<IOTNodeTrustTypeResult>> hashedNodeTrustTypes =
                    new Dictionary<int, List<IOTNodeTrustTypeResult>>();
                foreach (IOTNodeTrustTypeResult nodeTrustType in this.cachedNodeTrustTypeResult)
                {
                    if (!hashedNodeTrustTypes.ContainsKey(nodeTrustType.nodeId))
                        hashedNodeTrustTypes.Add(nodeTrustType.nodeId,
                            new List<IOTNodeTrustTypeResult>());
                    hashedNodeTrustTypes[nodeTrustType.nodeId].Add(nodeTrustType);
                }
                //对每一个节点分析其最可能的类型
                List<IOTNodeTrustTypeResult> combinedNodeTrustNodeTypes =
                    new List<IOTNodeTrustTypeResult>();
                foreach (KeyValuePair<int, List<IOTNodeTrustTypeResult>> k in hashedNodeTrustTypes)
                {
                    int[] t = new int[(int)IOTNodeType.COUNT];
                    int node = k.Key;
                    List<IOTNodeTrustTypeResult> hashedNodeTrustType = k.Value;
                    int org = global.readers[node].OrgId;
                    foreach (IOTNodeTrustTypeResult r in hashedNodeTrustType)
                    {
                        switch (r.type)
                        {
                            case IOTNodeType.NORMAL:
                                t[(int)IOTNodeType.NORMAL]++;
                                break;
                            case IOTNodeType.NODE_FAULTY:
                                t[(int)IOTNodeType.NODE_FAULTY]++;
                                break;
                            case IOTNodeType.ENV_FAULTY:
                                t[(int)IOTNodeType.ENV_FAULTY]++;
                                break;
                            case IOTNodeType.MALICIOUS:
                                t[(int)IOTNodeType.MALICIOUS]++;
                                break;
                            default:
                                break;
                        }
                    }
                    int max = 0;
                    for (int i = 0; i < (int)IOTNodeType.COUNT; i++)
                    {
                        if (t[i] > t[max])
                            max = i;
                    }
                    if ((IOTNodeType)max == IOTNodeType.NORMAL)
                        continue;

                    //只讲异常的节点放入待处理列表中
                    IOTNodeTrustTypeResult finalNodeTrustType =
                        new IOTNodeTrustTypeResult(node, org, (IOTNodeType)max);
                    combinedNodeTrustNodeTypes.Add(finalNodeTrustType);
                }

                //按照机构划分节点报告
                Dictionary<int, List<IOTNodeTrustTypeResult>> hashedCombinedNodeTrustNodeTypes =
                    new Dictionary<int, List<IOTNodeTrustTypeResult>>();
                foreach (IOTNodeTrustTypeResult r in combinedNodeTrustNodeTypes)
                {
                    if (!hashedCombinedNodeTrustNodeTypes.ContainsKey(r.orgId))
                        hashedCombinedNodeTrustNodeTypes.Add(r.orgId, new List<IOTNodeTrustTypeResult>());
                    hashedCombinedNodeTrustNodeTypes[r.orgId].Add(r);
                }

                //对同一机构的节点归纳，计算该机构的信誉
                CalculateOrganizationReputation(hashedCombinedNodeTrustNodeTypes);
                this.cachedNodeTrustTypeResult.Clear();
            }
            OutputReputations();
            float time = scheduler.currentTime + global.checkNodeTypeTimeout;
            Event.AddEvent(new Event(time, EventType.CHK_RT_TIMEOUT, this, null));
        }

        //一个机构的节点的报告，计算该机构的信誉值
        void CalculateOrganizationReputation(Dictionary<int, List<IOTNodeTrustTypeResult>> hashedCombinedNodeTrustNodeTypes)
        {
            IOTGlobal g = (IOTGlobal)global;
            foreach (KeyValuePair<int, List<IOTNodeTrustTypeResult>> k in hashedCombinedNodeTrustNodeTypes)
            {
                int org = k.Key;
                int mal = 0;
                int fau = 0;
                List<IOTNodeTrustTypeResult> nodeTrustTypes = k.Value;
                if (!this.orgReputations.ContainsKey(org))
                    this.orgReputations.Add(org, new IOTOrganizationTrust(org, g.InitTrust));
                foreach (IOTNodeTrustTypeResult nodeTrustType in nodeTrustTypes)
                {
                    switch (nodeTrustType.type)
                    {
                        case IOTNodeType.ENV_FAULTY:
                        case IOTNodeType.NODE_FAULTY:
                            fau++;
                            break;
                        case IOTNodeType.MALICIOUS:
                            mal++;
                            break;
                        case IOTNodeType.NORMAL: //不可能
                        default:
                            break;
                    }
                }
                //这里还需要再确定一下惩罚因子，还有一个遗忘因子,可以建模
                double v = this.orgReputations[org].trustValue;
                //Console.WriteLine("v:"+v);
                this.orgReputations[org].trustValue = Math.Max(v - (0.1 * fau + 0.2 * mal), 0);
            }
            //对org按照信誉值进行排序
            this.sortedOrgReputations.Clear();
            this.sortedOrgReputations.AddRange(this.orgReputations.Values);
            this.sortedOrgReputations.Sort();
        }

        public void OutputReputations()
        {
            Console.WriteLine("{0:F4} Reputation results:", scheduler.currentTime);
            foreach (IOTOrganizationTrust o in this.sortedOrgReputations)
            {
                Console.WriteLine("Org{0} value:{1}", o.id, o.trustValue);
            }
        }

        public List<IOTOrganizationTrust> getSortedOrgReputations()
        {
            return sortedOrgReputations;
        }

        public Dictionary<int, IOTOrganizationTrust> getOrgReputations()
        {
            return orgReputations;
        }
    }
}

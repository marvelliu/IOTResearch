using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrustArch
{
    public enum IOTNodeType
    {
        NORMAL,
        NODE_FAULTY,
        ENV_FAULTY,
        MALICIOUS,
        COUNT
    }

    [Serializable]
    public class IOTNodeTrustTypeResult
    {
        public int nodeId;
        public int orgId;
        public IOTNodeType type;

        public IOTNodeTrustTypeResult(int node, int org, IOTNodeType type)
        {
            this.nodeId = node;
            this.orgId = org;
            this.type = type;
        }
    }

    [Serializable]
    public class IOTNodeTrustResult
    {
        public int nodeId;
        public int orgId;
        public DSClass ds;
        public int[] confirmed;
        public double[] confirmedRate;
        public IOTNodeTrustResult(int node, int org)
        {
            this.nodeId = node;
            this.orgId = org;
            this.ds = null;
            this.confirmed = new int[(int)IOTNodeType.COUNT];
            this.confirmedRate = new double[(int)IOTEventType.COUNT];            
        }
    }

    public class IOTNodeTrust
    {
        static Dictionary<IOTEventType, IOTNodeType> nodeTypeMapping = new Dictionary<IOTEventType, IOTNodeType>()
            {
                {IOTEventType.NotDropPacket, IOTNodeType.NORMAL},
                {IOTEventType.NotDropPacketButNotReceivePacket, IOTNodeType.NORMAL},
                {IOTEventType.NotModifyPacket, IOTNodeType.NORMAL},
                {IOTEventType.NotMakePacket, IOTNodeType.NORMAL},
                {IOTEventType.NotDropCommand, IOTNodeType.NORMAL},
                {IOTEventType.NotDropCommandButNotDetected, IOTNodeType.NORMAL},
                {IOTEventType.NotModifyCommand, IOTNodeType.NORMAL},
                {IOTEventType.NotMakeCommand, IOTNodeType.NORMAL},
                {IOTEventType.NotMakeCommandButMove, IOTNodeType.NORMAL},
                {IOTEventType.NotMakeCommandButNetworkDelay, IOTNodeType.NORMAL},
                {IOTEventType.CorrectDeclaredRegionInfo, IOTNodeType.NORMAL},
                {IOTEventType.DropPacketMaliciously, IOTNodeType.MALICIOUS},
                {IOTEventType.ModifyPacketMaliciously, IOTNodeType.MALICIOUS},
                {IOTEventType.MakePacketMaliciously, IOTNodeType.MALICIOUS},
                {IOTEventType.BadTopologyMaliciously, IOTNodeType.MALICIOUS},
                {IOTEventType.DropCommandMaliciously, IOTNodeType.MALICIOUS},
                {IOTEventType.ModifyCommandMaliciously, IOTNodeType.MALICIOUS},
                {IOTEventType.MakeCommandMaliciously, IOTNodeType.MALICIOUS},
                {IOTEventType.BadDeclaredRegionInfo, IOTNodeType.MALICIOUS},
                {IOTEventType.ModifyPacketDueToNodeFaulty, IOTNodeType.NODE_FAULTY},
                {IOTEventType.BadTopologyDueToMove, IOTNodeType.NODE_FAULTY},
                {IOTEventType.ModifyCommandDueToNodeFaulty, IOTNodeType.NODE_FAULTY},
                {IOTEventType.DropPacketDueToBandwith, IOTNodeType.ENV_FAULTY},
                {IOTEventType.NotModifyPacketButNetworkFaulty, IOTNodeType.ENV_FAULTY},
                {IOTEventType.BadTopologyDueToNetwork, IOTNodeType.ENV_FAULTY},

            };
        static Dictionary<IOTEventType, double> rateThrethods = new Dictionary<IOTEventType, double>() {
                {IOTEventType.DropCommandMaliciously, 0.1},
                {IOTEventType.DropPacketDueToBandwith, 0.1},
                {IOTEventType.NotDropPacketButNotReceivePacket, 0.1},
        };

        public IOTNodeTrust()
        {
        }


        /* 使用一个惩罚奖励函数，如果指定类型的事件发生率大于阈值，则惩罚之，否则奖励
         * 使用指数函数作为系数，返回奖励或惩罚系数，取值在[1/p, p]
        */
        static double f(double rate, IOTEventType type)
        {
            double p = 2;
            if (!rateThrethods.ContainsKey(type))
                return 1;
            double rateThrethod = rateThrethods[type];
            return Math.Pow(p, (rateThrethod - rate));
        }

        /*
         * 在监督节点一级，将某一个节点的所有事件归类（正交求出最后的结果，不同类型的事件不能正交），然后推导出节点的性质
         * 输入为一段时间内接收到的事件
         * 输出为针对每一个机构的输出列表，输出列表为需要上传的节点可信报告
         * 这里有个问题：是先将节点的事件按照机构分类，然后在按照机构的数据包计算可信度；还是先计算可信度，然后在分类
         * 因为存在一个节点连续攻击不同机构的数据包的现象
         * 前者的话，可以及时更新节点的可信度，但是在上层信任汇聚的时候重新增加了一次；后者在汇聚的时候更准确，但是反应过慢
         */
        public static Dictionary<int, List<IOTNodeTrustResult>> DeduceAllNodeTrusts(int selfId, List<IOTEventTrustResult> events, float interval)
        {
            Dictionary<int, List<IOTNodeTrustResult>> orgNodeTrusts = new Dictionary<int, List<IOTNodeTrustResult>>();

            //这里采用先分类，再推导的方法
            /*将events按照应用和节点分类，第一个int为机构，第二个int为节点
            即先按机构分类，再按节点分类。
             * */
            Dictionary<int, Dictionary<int, List<IOTEventTrustResult>>> t1 =
                new Dictionary<int, Dictionary<int, List<IOTEventTrustResult>>>();
            for (int i = 0; i < events.Count; i++)
            {
                IOTEventTrustResult e = events[i];
                if (!t1.ContainsKey(e.app))
                    t1.Add(e.app, new Dictionary<int, List<IOTEventTrustResult>>());
                if (!t1[e.app].ContainsKey(e.node))
                    t1[e.app].Add(e.node, new List<IOTEventTrustResult>());
                t1[e.app][e.node].Add(events[i]);
            }

            /* 
             * 对每一个机构中的每一个节点计算其可信度
             */
            foreach (KeyValuePair<int, Dictionary<int, List<IOTEventTrustResult>>> k1 in t1)
            {
                //某个机构的所有节点的事件
                int org = k1.Key;
                Dictionary<int, List<IOTEventTrustResult>> eventTrusts = k1.Value;
                List<IOTNodeTrustResult> nodeTrusts = new List<IOTNodeTrustResult>();
                foreach (KeyValuePair<int, List<IOTEventTrustResult>> k2 in eventTrusts)
                {
                    //某个机构的某个节点的所有事件
                    int node = k2.Key;
                    List<IOTEventTrustResult> nodeEvents = k2.Value;

                    //从单个节点的事件的可信度，获得该节点的可信度
                    IOTNodeTrustResult nodeTrust = DeduceNodeTrust(node, selfId, nodeEvents, interval);
                    nodeTrust.ds.Output();

                    nodeTrusts.Add(nodeTrust);
                }
                orgNodeTrusts.Add(org, nodeTrusts);
            }
            return orgNodeTrusts;
        }

        static IOTNodeTrustResult DeduceNodeTrust(int node, int selfId, List<IOTEventTrustResult> nodeEvents, float interval)
        {
            //1 先根据事件的编号分类，对由多个节点观察到的同一个事件进行正交分析，得出多个节点对该事件的总体结论
            Dictionary<string, List<IOTEventTrustResult>> temp = new Dictionary<string, List<IOTEventTrustResult>>();
            for (int i = 0; i < nodeEvents.Count; i++)
            {
                IOTEventTrustResult e = nodeEvents[i];
                if (!temp.ContainsKey(e.eventIdent))
                    temp.Add(e.eventIdent, new List<IOTEventTrustResult>());
                temp[e.eventIdent].Add(e);
            }

            List<IOTEventTrustResult> combinedEvents = new List<IOTEventTrustResult>();
            foreach (KeyValuePair<string, List<IOTEventTrustResult>> pair in temp)
            {
                string eventIdent = pair.Key;
                List<IOTEventTrustResult> events1 = pair.Value;
                IOTEventTrustResult r = CombineToEvent(node, selfId, eventIdent, events1);
                combinedEvents.Add(r);
            }
            //combinedEvents中存放的是针对node的所有事件
            //2 将combinedEvents中的事件分为几类节点的性质，如正常、异常、恶意等。
            return DeduceNodeTrustByDefault(node, combinedEvents, interval);
        }

        //针对某一个节点的不同事件可信度，计算其可信度
        static IOTNodeTrustResult DeduceNodeTrustByDefault(int node, List<IOTEventTrustResult> events, float interval)
        {
            //将事件按照类型分类
            Dictionary<IOTEventCategoryType, List<IOTEventTrustResult>> temp = new Dictionary<IOTEventCategoryType, List<IOTEventTrustResult>>();
            foreach (IOTEventTrustResult e in events)
            {
                if (!temp.ContainsKey(e.category))
                    temp.Add(e.category, new List<IOTEventTrustResult>());
                temp[e.category].Add(e);
            }
            //计算每个类型的可信度，每个类型的可信度由多次该类型的事件组成，可使用正交的方法进行推导
            List<IOTEventTrustCategoryResult> combinedCategories = new List<IOTEventTrustCategoryResult>();
            foreach (KeyValuePair<IOTEventCategoryType, List<IOTEventTrustResult>> pair in temp)
            {
                IOTEventCategoryType category = pair.Key;
                List<IOTEventTrustResult> events1 = pair.Value;
                IOTEventTrustCategoryResult r = CombineToCategory(node, node + "-" + category, category, events1);
                combinedCategories.Add(r);
            }

            //分析每个类型的可信度，以及每个类型事件的频率，来推导节点的可信度
            //先分析频率，更新类型的可信度
            HashDSClass hashedCaterogies = new HashDSClass(IOTEventTrust.pow(IOTEventType.COUNT));
            int[] confirmedCount = new int[(int)IOTEventType.COUNT];
            int[] totalCount = new int[(int)IOTEventCategoryType.COUNT];
            for (int i = 0; i < combinedCategories.Count; i++)
            {
                IOTEventTrustCategoryResult r = combinedCategories[i];
                int start = IOTEventTrustResult.GetStartTypeByCategory(r.category);
                int count = IOTEventTrustResult.GetCountByCategory(r.category);
                totalCount[i] = r.totalEventCount;
                for (int j = 0; j < count; j++)
                    confirmedCount[start + j] = r.confirmedEventNums[j];
                MergeToHashDS(hashedCaterogies, r);
            }
            hashedCaterogies.Normalize();
            IOTNodeTrustResult nodeTrust = DeduceNodeTrustByCategories(node, combinedCategories[0].app, hashedCaterogies, confirmedCount, totalCount);
            return nodeTrust;
        }

        static void MergeToHashDS(HashDSClass ds, IOTEventTrustCategoryResult r)
        {
            int start = IOTEventTrustResult.GetStartTypeByCategory(r.category);
            for (int i = start; i < r.ds.length; i++)
                ds.SetM(i, r.ds.m[i]);
        }

        //根据节点事件的类型来推导节点的可信度
        static IOTNodeTrustResult DeduceNodeTrustByCategories(int node, int app, HashDSClass hashedCaterogies, int[] confirmedCount, int[] totalCount)
        {
            IOTGlobal global = (IOTGlobal)IOTGlobal.getInstance();
            IOTNodeTrustResult nodeTrust = new IOTNodeTrustResult(node, app);
            double c1 = DSClass.OR(hashedCaterogies.GetM(IOTEventTrust.pow(IOTEventType.NotDropPacket)),
                hashedCaterogies.GetM(IOTEventTrust.pow(IOTEventType.NotModifyPacket))); //Normal
            double c2 = DSClass.OR(hashedCaterogies.GetM(IOTEventTrust.pow(IOTEventType.NotDropPacketButNotReceivePacket)),
                hashedCaterogies.GetM(IOTEventTrust.pow(IOTEventType.ModifyPacketDueToNodeFaulty))); //Node faulty
            double c3 = DSClass.OR(hashedCaterogies.GetM(IOTEventTrust.pow(IOTEventType.DropPacketDueToBandwith)),
                hashedCaterogies.GetM(IOTEventTrust.pow(IOTEventType.NotModifyPacketButNetworkFaulty))); //Env faulty
            double c4 = DSClass.OR(hashedCaterogies.GetM(IOTEventTrust.pow(IOTEventType.DropPacketMaliciously)),
                hashedCaterogies.GetM(IOTEventTrust.pow(IOTEventType.ModifyPacketMaliciously))); //Maliciously
            DSClass ds = new DSClass(pow(IOTNodeType.COUNT));
            ds.SetM(DSClass.pow((int)IOTNodeType.NORMAL), c1);
            ds.SetM(DSClass.pow((int)IOTNodeType.NODE_FAULTY), c2);
            ds.SetM(DSClass.pow((int)IOTNodeType.ENV_FAULTY), c3);
            ds.SetM(DSClass.pow((int)IOTNodeType.MALICIOUS), c4);
            ds.Normalize();
            ds.Cal();

            nodeTrust.ds = ds;
            
            for (int i = 0; i < totalCount.Length; i++)
            {
                IOTEventCategoryType categoryType = (IOTEventCategoryType)i;
                int start = IOTEventTrustResult.GetStartTypeByCategory(categoryType);
                int count = IOTEventTrustResult.GetCountByCategory(categoryType);
                for (int j = 0; j < count; j++)
                {
                    int iType = start + j;
                    IOTEventType eventType = (IOTEventType)iType;
                    int confirmed = confirmedCount[iType];
                    if (!nodeTypeMapping.ContainsKey(eventType))
                        throw new Exception("No such a event type defined");

                    if (totalCount[i] == 0)
                        continue;

                    nodeTrust.confirmed[(int)nodeTypeMapping[eventType]] += confirmed;
                    if (ds.b[DSClass.pow(iType)] > global.BeliefThrehold && ds.p[DSClass.pow(iType)] > global.PlausibilityThrehold)
                        nodeTrust.confirmedRate[iType] = (double)confirmed / totalCount[i];
                }
            }
            return nodeTrust;
        }
        /*
        //根据事件发生的频率调整该事件的可信度
        static void UpdateM(IOTEventTrustCategoryResult r, IOTEventCategoryType category)
        {
            int count = IOTEventTrustResult.GetCountByCategory(category);
            int start = IOTEventTrustResult.GetStartTypeByCategory(category);
            for (int i = 0; i < count; i++)
            {
                int t = i + start;
                IOTEventType type = (IOTEventType)t;
                double confirmRate = (double)r.confirmedEventNums[i] / r.totalEventCount;
                r.ds.m[DSClass.pow(i)] = r.ds.m[DSClass.pow(i)] * f(confirmRate, type);
            }
        }*/

        //将不同节点对同一个事件的报告正交化为一个事件的报告
        static IOTEventTrustResult CombineToEvent(int node, int selfId, string eventIdent, List<IOTEventTrustResult> events)
        {
            int len = events[0].ds.m.Length;
            DSClass ds = null;
            int totalCount = 0;
            IOTEventCategoryType category = events[0].category;
            for (int i = 0; i < events.Count; i++)
            {
                IOTEventTrustResult e0 = events[0];
                IOTEventTrustResult e = events[i];
                if (e.app != e0.app || e.node != e0.node || e.category != category)
                {
                    Console.WriteLine("Error: app and node not match: {0}-{1}->{2}-{3}", e0.app, e0.node, e.app, e.node);
                    Console.ReadLine();
                    return null;
                }
                if (totalCount < e.totalEventCount)
                    totalCount = e.totalEventCount;

                if (ds == null)
                    ds = e.ds;
                else
                    ds = DSClass.Combine(ds, e.ds);//进行正交运算
            }
            ds.Cal();
            IOTEventTrustResult r = new IOTEventTrustResult(node, selfId, node.ToString(), category, ds);
            r.totalEventCount = totalCount;
            r.nodeReportCount = events.Count;
            return r;
        }


        static IOTEventTrustCategoryResult CombineToCategory(int node, string ident, IOTEventCategoryType category, List<IOTEventTrustResult> events)
        {
            IOTEventTrustResult e0 = events[0];
            int len = e0.ds.m.Length;
            int app = e0.app;
            DSClass ds = null;
            IOTEventTrustCategoryResult r = new IOTEventTrustCategoryResult(node, app, category, null);
            int categoryCount = IOTEventTrustResult.GetCountByCategory(category);

            foreach (IOTEventTrustResult e in events)
            {
                if (e.app != e0.app || e.node != e0.node || e.category != e0.category)
                    throw new Exception(string.Format(
                "Error: app and node not match: {0}-{1}->{2}-{3}", e0.app, e0.node, e.app, e.node));
            }

            //先计算归一化因子
            int totalweight = 0;
            double[] weights = new double[events.Count];

            foreach (IOTEventTrustResult e in events)
            {
                totalweight += e.nodeReportCount;
            }
            //调整权重
            DSClass[] d = new DSClass[events.Count];
            for (int i = 0; i < events.Count; i++)
            {
                weights[i] = (double)events[i].nodeReportCount / totalweight;
                d[i] = events[i].ds;
            }

            //进行正交运算
            ds = DSClass.CombineWithWeight(d, weights);

            //计算每种类型在总的事件中确认的比例
            ds.Normalize();
            ds.Cal();
            r.ds = ds;

            foreach (IOTEventTrustResult e in events)
            {
                for (int i = 0; i < categoryCount; i++)
                {
                    IOTEventType type = (IOTEventType)i;
                    /*
                    if (!IOTEventTrustResult.confirmBeliefThrehold.ContainsKey(type))
                        continue;
                    double rateThrehold = IOTEventTrustResult.confirmBeliefThrehold[type];
                     **/
                    IOTGlobal global = (IOTGlobal)IOTGlobal.getInstance();
                    double beliefThrehold = global.BeliefThrehold;
                    double plausibilityThrehold = global.PlausibilityThrehold;
                    int offset = IOTEventTrustResult.GetOffsetByCategory(e.category, type);
                    ////计算某类型的事件发生的次数，前提是该事件正交结果可能是发生的
                    if (e.ds.b[DSClass.pow(offset)] > beliefThrehold
                        && e.ds.p[DSClass.pow(offset)] > plausibilityThrehold
                        //&& ds.b[DSClass.pow(offset)] > beliefThrehold
                        //&& ds.p[DSClass.pow(offset)] > plausibilityThrehold
                        )
                        r.confirmedEventNums[i]++;
                }

                if (e.totalEventCount > r.totalEventCount)
                    r.totalEventCount = e.totalEventCount;
            }
            return r;
        }

        public static int pow(IOTNodeType type)
        {
            return DSClass.pow((int)type);
        }
    }
}
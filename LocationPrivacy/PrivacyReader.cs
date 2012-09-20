using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace LocationPrivacy
{
    public enum SubNodeStatus
    {
        OUTSIDE = 0,
        RETRIEVING,
        NORMAL,
        NORMAL1
    }

    class DistanceEntry
    {
        public int origId;
        public double dist;
        public float time;

        public DistanceEntry(int origId, double dist, float time)
        {
            this.origId = origId;
            this.dist = dist;
            this.time = time;
        }
    }

    class AnonyRegionEntry
    {
        public int rootId;
        public int hops;

        public AnonyRegionEntry(int rootId, int hops)
        {
            this.rootId = rootId;
            this.hops = hops;
        }
    }

    public class SubTreeEntry //子节点
    {
        public HashSet<int> subnodes = new HashSet<int>(); //子树节点
        public int subcn = 0; //子树中可用的匿名区域外的节点
        public SubNodeStatus status = SubNodeStatus.NORMAL; //子节点当前状态         
        public double subnodeLastPing = Scheduler.getInstance().currentTime;//子节点最后响应
        public int hops = 1;
    }

    [Serializable]
    public class NativeGroupResponseEntry
    {
        public int nodeId;
        public int h; //当前节点到目的节点的跳数+1
        public int origId;
        public int k;
        public int h0;//设定的跳数阈值
        public bool avail; //在返回信息的时候，指示该节点是否可被加入k-组
        public NativeGroupResponseEntry(int nodeId, int h, int h0, int origId, int k)
        {
            this.nodeId = nodeId;
            this.h = h;
            this.origId = origId;
            this.h0 = h0;
            this.k = k;
            this.avail = true;
        }

        public NativeGroupResponseEntry(int nodeId, int h, int h0, int origId, int k, bool avail)
        {
            this.nodeId = nodeId;
            this.h = h;
            this.origId = origId;
            this.h0 = h0;
            this.k = k;
            this.avail = avail;
        }
    }

    public class AnonyTreeEntry
    {
        public int rootId; //根节点
        public Reader parent; //父节点
        public SubNodeStatus status;
        public Dictionary<int, SubTreeEntry> subtree; //子树        
        //public bool regionReqConfirmed;
        public int m; //至少要添加的节点
        public int cn; //总共可用的匿名区域外的节点
        public Dictionary<string, AnonyRequest> anonyRequests; //每个匿名组至少需要的节点数
        public Dictionary<string, float> pingRecords;
        public Dictionary<string, Dictionary<int, HashSet<int>>> pendingNewGroupNodes; //收到但尚未转发的新建组节点
        public Dictionary<string, HashSet<int>> pendingCandidates; //尚未加入匿名组的候选节点
        public float checkSubTreeTime = -1;
        public float lastSubTreeUpdateTime = -1;


        public AnonyTreeEntry(int rootId, Reader father, int m)
        {
            this.rootId = rootId;
            this.parent = father;
            //this.parenthops = 1;
            //this.parentconfirmed = Scheduler.getInstance().CurrentTime;
            //this.regionReqConfirmed = regionReqConfirmed;
            this.m = m;
            this.cn = 0;
            this.subtree = new Dictionary<int, SubTreeEntry>();

            this.anonyRequests = new Dictionary<string, AnonyRequest>();
            this.pingRecords = new Dictionary<string, float>();
            this.pendingNewGroupNodes = new Dictionary<string, Dictionary<int, HashSet<int>>>();
            this.pendingCandidates = new Dictionary<string, HashSet<int>>();
        }
    }

    public class AnonyGroupEntry
    {
        public List<int> ks; //正在处理的k组

        public Dictionary<int, HashSet<int>> groups;
        //public Dictionary<string, HashSet<int>> subgroup;//子树中的匿名组，key为id+k
        public Dictionary<string, HashSet<int>> subUnavailAnonyNodes; //子节点每个k对应已加入的子树节点，key为"子节点id+k",value为这个子节点下k值不可用的节点
        public AnonyGroupEntry()
        {
            this.ks = new List<int>();
            this.groups = new Dictionary<int, HashSet<int>>();
            //this.subgroup = new Dictionary<string, HashSet<int>>();
            this.subUnavailAnonyNodes = new Dictionary<string, HashSet<int>>();
        }
    }

    public class AnonyRequest
    {
        public int prevNode; //最后上一级的节点，最终收敛到本节点或父亲节点
        public int requiredCount;
        public float responseTime;
        public SubNodeStatus responseStatus;
        public float recvParentTime;

        public AnonyRequest(int prevNode, int requiredCount)
        {
            this.prevNode = prevNode;
            this.requiredCount = requiredCount;
            this.responseTime = -1;
            this.recvParentTime = -1;
            this.responseStatus = SubNodeStatus.OUTSIDE;
        }
    }

    class AnonGroupArg
    {
        public int rootId;
        public int origId;
        public int k;
        public AnonGroupArg(int rootId, int origId, int k)
        {
            this.rootId = rootId;
            this.origId = origId;
            this.k = k;
        }
    }

    public class PrivacyReader : Reader
    {

        Dictionary<string, AnonyRegionEntry> CachedRegionEntries = new Dictionary<string, AnonyRegionEntry>();
        public Dictionary<string, AnonyTreeEntry> CachedTreeEntries = new Dictionary<string, AnonyTreeEntry>();
        Dictionary<int, DistanceEntry> CachedDistEntries = new Dictionary<int,DistanceEntry>();
        public AnonyGroupEntry anonGroups = new AnonyGroupEntry();

        static public PrivacyReader rootReader;
        
        //设框结构是全局的，事实上节点间共享该结构，我们为了简化设为static的
        public static FrameList AnonFrames = new FrameList();

        public Dictionary<string, Dictionary<int, NativeGroupResponseEntry>> pendingNativeGroupResponses = new Dictionary<string, Dictionary<int, NativeGroupResponseEntry>>();
        public int requestNode = -1;
        public int lastesth = 2;
        public Dictionary<string, double> cachedRecvRequest = new Dictionary<string, double>();
        public Dictionary<string, double> cachedRecvResponse = new Dictionary<string, double>();
        public Dictionary<string, double> cachedCheckedRecvResponse = new Dictionary<string, double>();
        public Dictionary<int, HashSet<int>> cachedCandidateNodes = new Dictionary<int, HashSet<int>>();//native2 使用，节点缓存返回消息的节点
        private PrivacyGlobal global;

        new public static PrivacyReader ProduceReader(int id, int org)
        {
            return new PrivacyReader(id, org);
        }

        public PrivacyReader(int id, int org)
            : base(id, org)
        {
            this.global = (PrivacyGlobal)Global.getInstance();
        }


        int SelectOneFromGroup()
        {
            if (this.anonGroups.groups.Count == 0)//选择最小的
                return -1;
            int maxg = this.anonGroups.groups.First().Key;
            foreach (KeyValuePair<int, HashSet<int>> pair in this.anonGroups.groups)
            {
                int k = pair.Key;
                HashSet<int> group = pair.Value;
                if (group.Count > this.anonGroups.groups[maxg].Count)
                    maxg = k;
            }
            Random r = new Random((int)scheduler.currentTime+this.Id);
            int count = this.anonGroups.groups[maxg].Count;
            int x = r.Next(count);
            int i = 0;
            foreach (int nodeId in this.anonGroups.groups[maxg])
            {
                if (i >= x)
                    return nodeId;
                i++;
            }
            return -1;

            /*//选择组内最小的吧
            int min = 1000;
            foreach (int nodeId in this.anonGroups.groups[maxg])
            {
                if (nodeId < min)
                    min = nodeId;
            }
            return min;
             * */
            /*
            foreach (HashSet<int> group in this.AnonGroups.group.Values)
            {
                int count = group.Count;
                int n = (int)Utility.U_Rand(count);
                int i = 0;
                //Console.WriteLine("count:{0}", count);
                //Console.WriteLine("n:{0}", n);
                foreach (int nodeId in group)
                {
                    if (i >= n - 1)
                        return nodeId;
                    i++;
                }
            }

            foreach (AnonyTreeEntry subTreeInfo in this.CachedTreeEntries.Values)
            {
                int count = getSubTreeCount(subTreeInfo.subtree);
                int n = (int)Utility.U_Rand(count);
                int i = 0;

                foreach (int c in subTreeInfo.subtree.Keys)
                {
                    SubTreeEntry subtree = subTreeInfo.subtree[c];
                    if (i >= n - 1)
                        return c;
                    i++;
                    foreach (int nodeId in subtree.subnodes)
                    {
                        if (i >= n - 1)
                            return nodeId;
                        i++;
                    } 
                }
            }
            return -2;
             * */
        }

        //节点开始新建一个匿名组
        public void JoinAnonyGroup(object o)
        {
            GroupArgs args = (GroupArgs)o;
            int k = args.k;
            int L = args.l;
            int random = args.random;

            if (random > 0)//需要从匿名组中随机选择一个节点
            {
                int nodeId = PrivacyReader.rootReader.SelectOneFromGroup();
                PrivacyReader reader = (PrivacyReader)Node.getNode(nodeId, NodeType.READER);
                if(reader !=null)
                    reader.JoinAnonyGroup(new GroupArgs(k, L, 0));
                return;
            }

            if (!this.anonGroups.ks.Contains(k))
                this.anonGroups.ks.Add(k);
            //this.AnonGroups.group.Add(k, new HashSet<int>());


            //native 方法
            if (global.method == 1)
            {
                this.lastesth = 2;
                Console.WriteLine("{0:F4} [JOIN_ANON_GROUP] {1}{2} start to join group k={3}, l={4}. [IN]", scheduler.currentTime, this.type, this.Id, k, this.lastesth);
                SendNativeGroupRequest(this.lastesth, this.lastesth, k, this.Id);
                string groupident = this.Id + "-" + k + "-" + this.lastesth;
                this.pendingNativeGroupResponses.Add(groupident, new Dictionary<int, NativeGroupResponseEntry>());
                this.cachedRecvRequest.Add(groupident, scheduler.currentTime);
                Event.AddEvent(new Event(scheduler.currentTime + 0.3f, EventType.CHK_NATGROUP, this, groupident));
                return;
            }

            //是否建立了匿名树
            int groupTreeRoot = -1;
            foreach (KeyValuePair<string, AnonyTreeEntry> pair in this.CachedTreeEntries)
            {
                AnonyTreeEntry subTreeInfo = pair.Value;
                if (subTreeInfo.rootId > 0 && subTreeInfo.status != SubNodeStatus.OUTSIDE)
                {
                    groupTreeRoot = subTreeInfo.rootId;
                    break;
                }
            }

            //如果建立了匿名树，则不是第一次，或者是native2的方法，那也先获取频繁子集
            HashSet<int> freqSet = new HashSet<int>();
            //if (groupRoot >= 0 || global.nativeMethod == 2)            
            //第二种方法， native2
            if (global.method == 2)
            {
                Console.WriteLine("{0:F4} [JOIN_ANON_GROUP] {1}{2} start to join group k={3}, l={4}. [IN]", scheduler.currentTime, this.type, this.Id, k, this.lastesth);

                //比较现有匿名组中出现次数最多的节点
                Dictionary<int, int> h = new Dictionary<int, int>();
                foreach (KeyValuePair<int, HashSet<int>> pair in this.anonGroups.groups)
                {
                    int k1 = pair.Key;
                    HashSet<int> g = pair.Value;
                    foreach (int n in g)
                    {
                        if (!h.ContainsKey(n))
                            h.Add(n, 0);
                        h[n]++;
                    }
                }

                int[] sortedh = Utility.SortDictionary(h);
                int num = 0;
                for (int i = 0; i < sortedh.Length; i++)
                {
                    //选出最大的若干项
                    //为了增加概率，增加1.5倍候选节点
                    if (num >= k)
                        break;
                    //如果出现的次数小于阈值，则忽略
                    if (h[sortedh[i]] < 1)
                        continue;
                    //查找该节点是否已在其他k-匿名组中了
                    if (groupTreeRoot >= 0)
                    {
                        bool found = false;
                        foreach (int c in this.CachedTreeEntries[groupTreeRoot + ""].subtree.Keys)
                        {
                            string key1 = c + "-" + k;
                            if (!this.anonGroups.subUnavailAnonyNodes.ContainsKey(key1))
                            {
                                found = true;
                                break;
                            }
                        }
                        if (found == true)
                            continue;
                    }
                    freqSet.Add(sortedh[i]);
                    num++;
                }
                if (freqSet.Count > 0 && !freqSet.Contains(this.Id))
                {
                    freqSet.Remove(freqSet.Last());
                    freqSet.Add(this.Id);
                }

                if (freqSet.Count == 0)
                {
                    this.lastesth = 2;
                    SendNativeGroupRequest(this.lastesth, this.lastesth, k, this.Id);
                    string groupident = this.Id + "-" + k + "-" + this.lastesth;
                    this.pendingNativeGroupResponses.Add(groupident, new Dictionary<int, NativeGroupResponseEntry>());
                    this.cachedRecvRequest.Add(groupident, scheduler.currentTime);
                    Event.AddEvent(new Event(scheduler.currentTime + 0.3f, EventType.CHK_NATGROUP, this, groupident));
                    return;
                }
                else
                {
                    this.cachedCandidateNodes.Add(k, new HashSet<int>());
                    foreach (int x in freqSet)
                        SendSetLongNativeGroupRequest(this.Id, k, 0, x);

                    string groupident = this.Id + "-" + k;
                    this.pendingNativeGroupResponses.Add(groupident, new Dictionary<int, NativeGroupResponseEntry>());
                    this.cachedRecvRequest.Add(groupident, scheduler.currentTime);
                    Event.AddEvent(new Event(scheduler.currentTime + global.native2WaitingTimeout, EventType.CHK_NATGROUP1, this, groupident));
                }
                return;
            }

            //否则，为我们改进的方法
            int rootId = this.Id;
            string key = rootId+"";
            if (groupTreeRoot < 0)//本节点未在匿名树中，新建树，再建组
            {
                Console.WriteLine("{0:F4} [JOIN_ANON_GROUP] {1}{2} start to join group k={3}, l={4}. [IN]", scheduler.currentTime, this.type, this.Id, k, L);
                
                int hops = 0;
                int m = -1;

                //if (!this.CachedRegionEntries.ContainsKey(key))
                    this.CachedRegionEntries.Add(key, new AnonyRegionEntry(rootId, hops));
                //if(!this.CachedDistEntries.ContainsKey(this.Id))
                    this.CachedDistEntries.Add(this.Id, new DistanceEntry(this.Id, 0, scheduler.currentTime));
                //init
                double includedAngle = global.includedAngle;
                SendTreeGroupRequest(rootId,m, L, 0, 0, 0);
                //if(!this.CachedTreeEntries.ContainsKey(key))
                    this.CachedTreeEntries.Add(key, new AnonyTreeEntry(rootId, null, m));
                this.CachedTreeEntries[key].status = SubNodeStatus.NORMAL;
            }
            else
            {
                Console.WriteLine("{0:F4} [JOIN_ANON_GROUP] {1}{2} start to join group k={3}, l={4}. [OUT]", scheduler.currentTime, this.type, this.Id, k, L);
                //之前建立过匿名组，在原有的匿名树的基础上新建一个
                ProcessNewGroupRequest(groupTreeRoot, k, this.Id, L, 0, 0, 0, null);
                //ProcessNewGroup(groupRoot, k, k, this.id, id);
            }
        }

        /*
        public void SendLongNativeGroupRequest(int h0, int h, int k, int origId)
        {
            Packet pkg = new Packet(this, BroadcastNode.Node, PacketType.NATIVE_LONG_GROUP_REQUEST);
            pkg.Data = new NativeGroupRequestField(origId, k, h, h0);
            SendPacketDirectly(scheduler.CurrentTime, pkg);
        }*/


        public void SendSetLongNativeGroupRequest(int origId, int k, int hop, int dstId)
        {
            this.retryOnSendingFailture = true;
            Packet pkg = new Packet(this, Node.getNode(dstId, NodeType.READER), PacketType.NATIVE_LONG_GROUP_REQUEST);
            pkg.Data = new SetLongNativeGroupRequestField(k, hop);
            SendData(pkg);
            this.retryOnSendingFailture = false;
        }

        public void RecvSetLongNativeGroupRequest(Packet pkg)
        {
            if (pkg.Next != this.Id && pkg.Next!= BroadcastNode.Node.Id)
                return;

            int srcNodeId = pkg.Src;
            SetLongNativeGroupRequestField req = (SetLongNativeGroupRequestField)pkg.Data;
            int k = req.k;
            req.hop++;

            //在路由表缓存中可以添加，因为是实时的
            if (this.routeTable.ContainsKey(srcNodeId))
            {
                routeTable[srcNodeId].next = pkg.Prev;
                routeTable[srcNodeId].hops = req.hop;
                routeTable[srcNodeId].localLastUpdatedTime = scheduler.currentTime;
                routeTable[srcNodeId].remoteLastUpdatedTime = scheduler.currentTime;
            }
            else
                routeTable.Add(srcNodeId, new RouteEntity(srcNodeId, pkg.Prev, req.hop, scheduler.currentTime, scheduler.currentTime));
            

            if (this.Id != pkg.Dst && pkg.Dst != BroadcastNode.Node.Id)
            {
                this.retryOnSendingFailture = true;
                RoutePacket(pkg);
                this.retryOnSendingFailture = false;
                return;
            }

            if (this.anonGroups.groups.ContainsKey(k))
                SendSetLongNativeGroupResponse(false, k, pkg.Src);
            else
                SendSetLongNativeGroupResponse(true, k, pkg.Src);
        }

        public void SendSetLongNativeGroupResponse(bool avail, int k, int dstId)
        {
            this.retryOnSendingFailture = true;
            Packet pkg = new Packet(this, Node.getNode(dstId, NodeType.READER), PacketType.NATIVE_LONG_GROUP_RESPONSE);
            pkg.Data = new SetLongNativeGroupResponseField(k, avail);
            SendData(pkg);
            this.retryOnSendingFailture = false;
        }

        
        public void RecvSetLongNativeGroupResponse(Packet pkg)
        {
            if (pkg.Next != this.Id && pkg.Next!= BroadcastNode.Node.Id)
                return;
            if (this.Id != pkg.Dst && pkg.Dst != BroadcastNode.Node.Id)
            {
                this.retryOnSendingFailture = true;
                RoutePacket(pkg);
                this.retryOnSendingFailture = false;
                return;
            }
            SetLongNativeGroupResponseField s = (SetLongNativeGroupResponseField)pkg.Data;
            bool avail = s.avail;
            int k = s.k;

            if (!this.cachedCandidateNodes.ContainsKey(k))
            {
                Console.WriteLine("Reader{0} has no cached {1} candidate group", this.Id, k);
                return;
            }
            else if (avail == false)
            {
                Console.WriteLine("Reader{0} is unavailable for candidate {1}-group", pkg.Src, k);
            }
            else if (!this.cachedCandidateNodes[k].Contains(pkg.Src))
            {
                Console.WriteLine("Reader{0} add reader{1} to cached {2} candidate group", this.Id, pkg.Src, k);
                this.cachedCandidateNodes[k].Add(pkg.Src);
            }
            string groupident = this.Id + "-" + k;
            CheckNativeLargeGroupResponse(groupident);
        }


        public void CheckNativeLargeGroupResponse(object o)
        {
            string groupident = (string)o;
            if (this.pendingNativeGroupResponses.Count == 0)
                return; 
            Console.WriteLine("Reader{0} check large native group response. ", this.Id);

            if (!this.pendingNativeGroupResponses.ContainsKey(groupident))
                return;
            if (!this.cachedCheckedRecvResponse.ContainsKey(groupident))
                this.cachedCheckedRecvResponse.Add(groupident, scheduler.currentTime);
            else
                this.cachedCheckedRecvResponse[groupident] = scheduler.currentTime;

            string[] l = groupident.Split('-');    //string groupident = this.id + "-" + k + "-" + this.lastesth;
            int k = int.Parse(l[1]);

            if (this.anonGroups.groups.ContainsKey(k))
                return;

            if (this.cachedCandidateNodes[k].Count >= k)//可以建立匿名组了
            {
                this.anonGroups.ks.Remove(k);
                HashSet<int> set = new HashSet<int>();
                set.Add(this.Id);
                int i = 1;
                foreach (int nodeId in this.cachedCandidateNodes[k])
                {
                    if (i == k)
                        break;
                    set.Add(nodeId);
                    i++;
                }
                this.anonGroups.groups.Add(k, set);
                PrintGroupNodes(set);
                PrivacyReader.rootReader = this;
                foreach (int nodeId in set)
                    SendSetGroup(this.Id, this.Id, k, nodeId, set, new HashSet<int>(){nodeId});
                return;
            }
            else if (scheduler.currentTime - this.cachedRecvRequest[groupident] >= global.native2WaitingTimeout)//使用native方法
            {
                this.lastesth = 2;
                Console.WriteLine("{0:F4} [FALLBACK] {1}{2} start to use the native method", scheduler.currentTime, this.type, this.Id);
                SendNativeGroupRequest(this.lastesth, this.lastesth, k, this.Id);
                groupident = this.Id + "-" + k + "-" + this.lastesth;
                if(!this.pendingNativeGroupResponses.ContainsKey(groupident))
                    this.pendingNativeGroupResponses.Add(groupident, new Dictionary<int, NativeGroupResponseEntry>());
                if(!this.cachedRecvRequest.ContainsKey(groupident))
                    this.cachedRecvRequest.Add(groupident, scheduler.currentTime);
                Event.AddEvent(new Event(scheduler.currentTime + 0.3f, EventType.CHK_NATGROUP, this, groupident));
                return;
            }
        }


        public void SendNativeGroupRequest(int h0, int h, int k, int origId)
        {
            Packet pkg = new Packet(this, BroadcastNode.Node, PacketType.NATIVE_GROUP_REQUEST);
            pkg.Data = new NativeGroupRequestField(origId, k, h, h0);
            SendPacketDirectly(scheduler.currentTime, pkg);
        }

        public void RecvNativeGroupRequest(Packet pkg)
        {
            if (pkg.Next != this.Id && pkg.Next!= BroadcastNode.Node.Id)
                return;
            if (this.Id != pkg.Dst && pkg.Dst!= BroadcastNode.Node.Id)
            {
                this.retryOnSendingFailture = true;
                RoutePacket(pkg);
                this.retryOnSendingFailture = false;
                return;
            }

            NativeGroupRequestField request = (NativeGroupRequestField)pkg.Data;
            int origId = request.origId;
            int k = request.k;
            int h = request.h;
            int h0 = request.h0;

            string groupident = origId + "-" + k + "-" + h0;
            if (this.cachedRecvRequest.ContainsKey(groupident))
                return;
            this.cachedRecvRequest.Add(groupident, scheduler.currentTime);

            //第一次收到
            this.requestNode = pkg.Prev;
            //Console.WriteLine("request node:{0}, h0:{1}, h:{2}", this.requestNode, h0, h);

            if (h <= 1)
            {
                NativeGroupResponseEntry e = new NativeGroupResponseEntry(this.Id, h, h0, origId, k, !this.anonGroups.groups.ContainsKey(k));
                if (!this.anonGroups.groups.ContainsKey(k))
                {
                    SendNativeGroupResponse(pkg.Prev, new List<NativeGroupResponseEntry>() { e });
                    return;
                }
            }
            else
            {
                //第一次返回-1，表示该节点存在
                NativeGroupResponseEntry e = new NativeGroupResponseEntry(this.Id, -1, h0, origId, k, !this.anonGroups.groups.ContainsKey(k));
                SendNativeGroupResponse(pkg.Prev, new List<NativeGroupResponseEntry> { e });
                h--;
                SendNativeGroupRequest(h0, h, k, origId);
                Event.AddEvent(new Event(scheduler.currentTime + 0.3f, EventType.CHK_NATGROUP, this, groupident));
            }

            //Console.WriteLine("pendingNativeGroupResponses count:{0}", pendingNativeGroupResponses.Count);
            if (!this.pendingNativeGroupResponses.ContainsKey(groupident))
                this.pendingNativeGroupResponses.Add(groupident, new Dictionary<int, NativeGroupResponseEntry>());
            //Console.WriteLine("pendingNativeGroupResponses[{0}] count:{1}", groupident, pendingNativeGroupResponses[groupident].Count);

        }

        public void SendNativeGroupResponse(int prev, List<NativeGroupResponseEntry> list)
        {
            /*
            Console.WriteLine("----------------{0}+{1}", this.id, prev);
            foreach (NativeGroupResponseEntry e in list)
            {
                string groupident = e.origId + "-" + e.k + "-" + e.h0;
                Console.Write("t: {0}*{1}\t", e.nodeId, groupident);
            }*/
            this.retryOnSendingFailture = true;
            Packet pkg = new Packet(this, Node.getNode(prev, NodeType.READER), PacketType.NATIVE_GROUP_RESPONSE);
            pkg.Data = list;
            SendData(pkg);
            this.retryOnSendingFailture = false;
        }

        public void PrintNativeTempGroup()
        {
            foreach (string g in this.pendingNativeGroupResponses.Keys)
            {
                Console.WriteLine();
                Console.Write("g: {0}\t", g);
                foreach (int c in this.pendingNativeGroupResponses[g].Keys)
                    Console.Write("c: {0}\t", c);
            }
            Console.WriteLine();
        }
        public void RecvNativeGroupResponse(Packet pkg)
        {
            if (pkg.Next != this.Id && pkg.Next != BroadcastNode.Node.Id)
                return;
            //PrintNativeTempGroup();

            if (this.Id != pkg.Dst && pkg.Dst != BroadcastNode.Node.Id)
            {
                this.retryOnSendingFailture = true;
                RoutePacket(pkg);
                this.retryOnSendingFailture = false;
                return;
            }

            List<NativeGroupResponseEntry> list = (List<NativeGroupResponseEntry>)pkg.Data;

            /*
            foreach (NativeGroupResponseEntry e in list)
            {
                Console.Write("e: {0}\t", e.nodeId);
            }*/
                       

            HashSet<string> temp = new HashSet<string>();
            foreach (NativeGroupResponseEntry e in list)
            {
                int nodeId = e.nodeId;
                int h = e.h;
                int origId = e.origId;
                int k = e.k;
                int h0 = e.h0;
                bool avail = e.avail;


                //在路由表缓存中可以添加，因为是实时的
                if (this.routeTable.ContainsKey(nodeId))
                {
                    routeTable[nodeId].next = pkg.Prev;
                    routeTable[nodeId].hops = h;
                    routeTable[nodeId].localLastUpdatedTime = scheduler.currentTime;
                    routeTable[nodeId].remoteLastUpdatedTime = scheduler.currentTime;
                }
                else
                    routeTable.Add(nodeId, new RouteEntity(nodeId, pkg.Prev, h, scheduler.currentTime, scheduler.currentTime));

                string groupident = origId + "-" + k + "-" + h0;

                if (!this.pendingNativeGroupResponses.ContainsKey(groupident))
                {
                    Console.WriteLine("Too late, drop.");
                    continue;
                }

                if (!temp.Contains(groupident) && h>0 && this.cachedCheckedRecvResponse.ContainsKey(groupident))
                    temp.Add(groupident);

                if (!this.pendingNativeGroupResponses[groupident].ContainsKey(nodeId))//第一次接到这个节点的响应
                {
                    if (h < 0)//第一次返回
                    {
                        this.pendingNativeGroupResponses[groupident].Add(nodeId, null);
                    }
                    else //最后一个节点返回
                    {
                        this.pendingNativeGroupResponses[groupident].Add(nodeId, e);
                    }
                }
                else
                {
                    //Console.WriteLine("reader{0} pendingNativeGroupResponses[{1}] count: {2}", this.id, groupident, this.pendingNativeGroupResponses[groupident].Count);
                    if (this.pendingNativeGroupResponses[groupident].ContainsKey(nodeId))
                        this.pendingNativeGroupResponses[groupident][nodeId] = e;
                    else
                        this.pendingNativeGroupResponses[groupident].Add(nodeId, e);
                }
                //PrintNativeTempGroup();

                //检查事件
                if (!this.cachedRecvResponse.ContainsKey(groupident))
                {
                    this.cachedRecvResponse.Add(groupident, scheduler.currentTime);
                }
            }            

            foreach (string groupident in temp)
            {
                CheckNativeGroupResponse(groupident);
            }
        }

        public void CheckNativeGroupResponse(object o)
        {
            string groupident = (string)o;
            if (this.pendingNativeGroupResponses.Count == 0)
                return;
            Console.WriteLine("Reader{0} check native group response. ", this.Id);
            
            if (!this.pendingNativeGroupResponses.ContainsKey(groupident))
                return;
            if (!this.cachedCheckedRecvResponse.ContainsKey(groupident))
                this.cachedCheckedRecvResponse.Add(groupident, scheduler.currentTime);
            else
                this.cachedCheckedRecvResponse[groupident] = scheduler.currentTime;


            Dictionary<int, NativeGroupResponseEntry> entrySet = this.pendingNativeGroupResponses[groupident];
            /*
            Console.WriteLine("count:{0}-{1}", this.pendingNativeGroupResponses.Count, this.pendingNativeGroupResponses[groupident].Count);
            foreach (int c in entrySet.Keys)
            {
                Console.Write("ppp:{0}\t", c);
            }*/

            string[] l = groupident.Split('-');
            int origId = int.Parse(l[0]);
            int k = int.Parse(l[1]);
            int h0 = int.Parse(l[2]);

            //检查节点是否全部返回
            int waitingNodes = 0;
            int availCount = 0;
            foreach (int nodeId in entrySet.Keys)
            {
                NativeGroupResponseEntry e = entrySet[nodeId];
                if (e == null)//尚未返回
                {
                    Console.WriteLine("Reader{0} is waiting for reader{1}", this.Id, nodeId);
                    waitingNodes ++;
                    continue;
                }
                else if (e.h < 0)
                {
                    Console.WriteLine("error impossible , abort");
                    return;
                }
                e.h++;
                if (this.cachedCandidateNodes.ContainsKey(k) && this.cachedCandidateNodes[k].Contains(nodeId))
                    continue;
                if (e.avail == true)
                    availCount++;
                //Console.Write("e: {0}\t", e.nodeId);
            }


            if (waitingNodes > 0)
                return;
            if (origId != this.Id)
            {
                if(!entrySet.ContainsKey(this.Id))
                    entrySet.Add(this.Id, new NativeGroupResponseEntry(this.Id, 1, h0, origId, k, !this.anonGroups.groups.ContainsKey(k)));
                SendNativeGroupResponse(this.requestNode, entrySet.Values.ToList());
            }
            else//到了初始节点
            {
                //现有节点可以建组，无需等待或做其他操作
                if (this.cachedCandidateNodes.ContainsKey(k))
                {
                    //if (entrySet.Count - waitingNodes + this.cachedCandidateNodes[k].Count >= k)
                    if (availCount +this.cachedCandidateNodes[k].Count >= k-1)//算上自己
                    {
                        this.anonGroups.ks.Remove(k);
                        HashSet<int> set = new HashSet<int>();
                        set.Add(this.Id);
                        int i = 1;
                        foreach (int nodeId in this.cachedCandidateNodes[k])
                        {
                            if (i == k)
                                break;
                            set.Add(nodeId);
                            i++;
                        }
                        foreach (int nodeId in entrySet.Keys)
                        {
                            if (i == k)
                                break;
                            if (entrySet[nodeId] == null || entrySet[nodeId].avail == false)
                                continue;
                            
                            set.Add(nodeId);
                            i++;
                        }
                        this.anonGroups.groups.Add(k, set);
                        PrintGroupNodes(set);
                        PrivacyReader.rootReader = this;
                        foreach (int nodeId in set)
                            SendSetGroup(this.Id, this.Id, k, nodeId, set, new HashSet<int>() { nodeId });
                    }
                }
                else if (waitingNodes > 0) //需要等待子节点
                    return;
                //之前已经建立成功
                else if (this.anonGroups.groups.ContainsKey(k))
                    return;
                //不满足要求
                else if (entrySet.Count < k - 1)
                {
                    this.lastesth++;
                    Console.WriteLine("New round-----------------------------------------------------------------------");

                    SendNativeGroupRequest(this.lastesth, this.lastesth, k, this.Id);
                    string newgroupident = origId + "-" + k + "-" + this.lastesth;
                    if (!this.pendingNativeGroupResponses.ContainsKey(newgroupident))
                        this.pendingNativeGroupResponses.Add(newgroupident, new Dictionary<int, NativeGroupResponseEntry>());
                    if (!this.cachedRecvRequest.ContainsKey(newgroupident))
                        this.cachedRecvRequest.Add(newgroupident, scheduler.currentTime);
                    Event.AddEvent(new Event(scheduler.currentTime + 0.3f, EventType.CHK_NATGROUP, this, newgroupident));
                }
                else //建立成功
                {
                    this.anonGroups.ks.Remove(k);
                    HashSet<int> set = new HashSet<int>();
                    set.Add(this.Id);
                    int i = 1;
                    foreach (int nodeId in entrySet.Keys)
                    {
                        if (i == k)
                            break;
                        if(entrySet[nodeId].avail == false)
                            continue;
                        set.Add(nodeId);
                        i++;
                    }
                    this.anonGroups.groups.Add(k, set);
                    PrintGroupNodes(set);
                    PrivacyReader.rootReader = this;
                    foreach (int nodeId in set)
                        SendSetGroup(this.Id, this.Id, k, nodeId, set, new HashSet<int>() { nodeId });
                }
            }
            //Console.WriteLine("pendingNativeGroupResponses[{0}] count: {1}", groupident, this.pendingNativeGroupResponses[groupident].Count);

            entrySet.Clear();
            this.pendingNativeGroupResponses.Remove(groupident);
            //Console.WriteLine("pendingNativeGroupResponses count: {0}", this.pendingNativeGroupResponses.Count);
        }

        public int GetTotalUnavailAnonyNodeCout(AnonyTreeEntry subTreeInfo, int k)
        {
            int totalUnavailAnonyNodeCout = 0;
            foreach (KeyValuePair<int, SubTreeEntry> pair in subTreeInfo.subtree)
            {
                int c = pair.Key;
                string key1 = c + "-" + k;
                if (!this.anonGroups.subUnavailAnonyNodes.ContainsKey(key1))
                    continue;
                totalUnavailAnonyNodeCout += this.anonGroups.subUnavailAnonyNodes[key1].Count;
            }
            return totalUnavailAnonyNodeCout;
        }

        private void AdjustPreviousGroupMembers(int origId, List<int> swapList)
        {
            PrivacyReader origNode = (PrivacyReader)global.readers[origId];
            foreach (int swapNodeId in swapList)
            {
                //本来要发请求告诉节点更换匿名组，这里就直接更换了
                PrivacyReader swapNode = (PrivacyReader)global.readers[swapNodeId];

                //修改所有节点中匿名组的信息
                for (int i = 0; i < global.readerNum; i++)
                {
                    PrivacyReader reader = (PrivacyReader)global.readers[i];
                    List<int> groupIds = reader.anonGroups.groups.Keys.ToList();
                    foreach (int k in groupIds)
                    {
                        HashSet<int> group = reader.anonGroups.groups[k];

                        //如果两个节点都存在一个组内，或都不在组内，则无需调整
                        if (group.Contains(swapNodeId) ^ group.Contains(origId) == false)
                            continue;

                        //如果某个节点的匿名组中存在某个节点，那么存在调整的可能
                        if (group.Contains(swapNodeId))
                        {
                            //否则交换这两个匿名组的节点值
                            group.Add(origId);
                            group.Remove(swapNodeId);
                        }
                        else//group.Contains(origId)
                        {
                            group.Add(swapNodeId);
                            group.Remove(origId);
                        }
                    }

                }
                //交换两个节点的匿名组信息
                HashSet<int> l = new HashSet<int>();
                Utility.AddHashSet(l, origNode.anonGroups.groups.Keys.ToList());
                Utility.AddHashSet(l, swapNode.anonGroups.groups.Keys.ToList());

                foreach (int k in l)
                {
                    //将原来swap节点和orig节点的匿名组交换
                    if (swapNode.anonGroups.groups.ContainsKey(k) && origNode.anonGroups.groups.ContainsKey(k))
                    {
                        HashSet<int> temp = swapNode.anonGroups.groups[k];
                        swapNode.anonGroups.groups[k] = origNode.anonGroups.groups[k];
                        origNode.anonGroups.groups[k] = temp;
                    }
                    else if (swapNode.anonGroups.groups.ContainsKey(k))
                    {
                        origNode.anonGroups.groups.Add(k, swapNode.anonGroups.groups[k]);
                        swapNode.anonGroups.groups.Remove(k);
                    }
                    else if (origNode.anonGroups.groups.ContainsKey(k))
                    {
                        swapNode.anonGroups.groups.Add(k, origNode.anonGroups.groups[k]);
                        origNode.anonGroups.groups.Remove(k);
                    }
                }


            }
        }

        public void ProcessNewGroupRequest(int rootId, int k, int origId, double L, double l, double preAngle, int hops, Reader snode)
        {
            string key = rootId + "";
            AnonyTreeEntry subTreeInfo = this.CachedTreeEntries[key];
            string newgroupident = rootId + "-" + origId + "-" + k;

            Console.WriteLine("READER{0} is process new group request...", Id);

            Dictionary<int, SubTreeEntry> subtree = this.CachedTreeEntries[key].subtree;

            //原来的思路是寻找包含所有候选节点的父节点
            //现在通过框添加节点


            PrivacyReader.AnonFrames.DumpFrames(this.Id);

            //如果有的节点被交换，则需要调整其原有的你们匿名组，在我们的方案中，只有两类交换
            //第一类是锚点交换，只在本框内，不会涉及跨组，而第二类在节点不够的条件下，需要跨组交换，这个需要调整
            //由于只与自由框交换，所以只需要调整origId节点即可
            //swapList是指示是否需要交换节点的列表
            List<int> swapList = null;
            HashSet<int> result = PrivacyReader.AnonFrames.AddNewGroup(origId, this.Id, k, this.anonGroups, subtree, ref swapList);

            //建完组，处理后面的事情
            if (result != null)
            {
                PrintGroupNodes(result);
                PrivacyReader.AnonFrames.DumpFrames(this.Id);

                //如果已经在该组内，且匿名组没有变化，则返回
                if (result.Contains(this.Id) && this.anonGroups.groups.ContainsKey(k)
                    && Utility.IsSameHashSet(this.anonGroups.groups[k], result))
                    return;

                if (swapList != null && swapList.Count > 0)
                    AdjustPreviousGroupMembers(origId, swapList);

                //向子节点发送通知
                //PrintGroupNodes(result);
                if (result.Contains(this.Id))
                {
                    HashSet<int> tmp = new HashSet<int>();
                    Utility.CopyHashSet(tmp, result);
                    this.anonGroups.groups.Add(k, tmp);
                }

                foreach (KeyValuePair<int, SubTreeEntry> pair in subTreeInfo.subtree)
                {
                    int c = pair.Key;
                    HashSet<int> list = pair.Value.subnodes;

                    HashSet<int> newsubnode = new HashSet<int>();

                    foreach (int x in result)
                    {
                        if (c == x || list.Contains(x))
                            newsubnode.Add(x);
                    }
                    if (newsubnode.Count > 0)
                        SendSetGroup(rootId, origId, k, c, result, newsubnode);
                }
                //向父亲报告通知
                if (subTreeInfo.parent != null)
                    SendSetGroup(rootId, origId, k, subTreeInfo.parent.Id, result, null);
            }
            else if (this.Id != rootId) //如果建组失败，则向父节点发送
            {
                SendNewGroupRequest(rootId, k, origId, subTreeInfo.parent.Id, L, l, preAngle, hops); //继续发送
                return;
            }
            else //如果建组失败，且自己就是根节点，则放弃建组
            {
                //不重试了，直接返回失败
                Console.WriteLine("{0:F4} [NEW_GROUP_FAIL] {1}{2} {3}", scheduler.currentTime, "READER", this.Id, "new group failed, not enough nodes.");
            }
        }

        public void SendNewGroupRequest(int rootId, int k, int origId, int dstNode, double L, double l, double preAngle, int hops)
        {
            this.retryOnSendingFailture = true;
            Packet pkg = new Packet(this, Node.getNode(dstNode, NodeType.READER), PacketType.NEW_GROUP_REQUEST);
            pkg.Data = new NewGroupRequestField(rootId, k, origId, L, l, preAngle, hops);
            SendData(pkg);
            this.retryOnSendingFailture = false;
        }

        public void RecvNewGroupRequest(Packet pkg)
        {
            if (pkg.Next != this.Id && pkg.Next != BroadcastNode.Node.Id)
                return;
            if (this.Id != pkg.Dst)
            {
                this.retryOnSendingFailture = true;
                RoutePacket(pkg);
                this.retryOnSendingFailture = false;
                return;
            }

            NewGroupRequestField request = (NewGroupRequestField)pkg.Data;
            ProcessNewGroupRequest(request.rootId, request.k, request.origId, request.L, request.l, request.preAngle, request.hops, (Reader)Node.getNode(pkg.Src, NodeType.READER));
        }


        //建组成功后，其他节点收到响应
        public void RecvNewGroupResponse(Packet pkg)
        {
            if (pkg.Next != this.Id && pkg.Next != BroadcastNode.Node.Id)
                return;

            if (this.Id != pkg.Dst)
            {
                this.retryOnSendingFailture = true;
                RoutePacket(pkg);
                this.retryOnSendingFailture = false;
                return;
            }
            //收到上层节点的候选节点
            NewGroupRequestField request = (NewGroupRequestField)pkg.Data;
            int rootId = request.rootId;            
            int k = request.k;
            int origId = request.origId;

            //该请求是直接发到目的地的，不可能换origId
            if (this.Id != origId)
                throw new Exception("Wrong origId");
            ProcessNewGroup(rootId, k, k, origId, this.Id);
        }


        //检查NEW_GROUP请求
        public void ProcessNewGroup(int rootId, int k, int assigningCount, int origId, int prevNode)
        {
            Console.WriteLine("READER{0} processing...", Id);
            string key = rootId + "" ;

            if (this.anonGroups.groups.ContainsKey(k) && this.anonGroups.groups[k].Count > 0)
                return;

            if (!this.CachedTreeEntries.ContainsKey(key))
                return;

            if (!this.anonGroups.ks.Contains(k))
                this.anonGroups.ks.Add(k);

            AnonyTreeEntry subTreeInfo = this.CachedTreeEntries[key];
            string newgroupident = rootId + "-" + origId + "-" + k;

            Dictionary<int, SubTreeEntry> subtree = this.CachedTreeEntries[key].subtree;


            if (subTreeInfo.parent != null && prevNode == subTreeInfo.parent.Id)
                SendNewGroupPing(rootId, k, origId, subTreeInfo.parent.Id);

            if (subTreeInfo.anonyRequests.ContainsKey(newgroupident))
            {
                if (subTreeInfo.anonyRequests[newgroupident].recvParentTime > 0 && subTreeInfo.anonyRequests[newgroupident].responseTime < 0)//还没有响应，先放弃
                    return;
                if (subTreeInfo.lastSubTreeUpdateTime < subTreeInfo.anonyRequests[newgroupident].responseTime
                    && subTreeInfo.anonyRequests[newgroupident].responseTime > 0)
                {
                    //已有结果，汇报
                    Dictionary<int, HashSet<int>> d = subTreeInfo.pendingNewGroupNodes[newgroupident];
                    HashSet<int> newresult = new HashSet<int>();
                    foreach (HashSet<int> h in d.Values)
                    {
                        if (h == null) //如果还有子节点没有返回响应
                            return;
                    }

                    foreach (HashSet<int> h in d.Values)//全部有结果，则向上汇报
                    {
                        foreach (int x in h)
                        {
                            newresult.Add(x);
                        }
                    }
                    if (this.anonGroups.groups.ContainsKey(k))
                        newresult.Add(this.Id);
                    SendNewGroupResponse(rootId, k, origId, newresult, subTreeInfo.parent);
                }
            }
            else
            {
                subTreeInfo.anonyRequests.Add(newgroupident, new AnonyRequest(prevNode, assigningCount));
                Console.WriteLine("reader{0} appending new anonyRequests", this.Id);
            }



            subTreeInfo.anonyRequests[newgroupident].requiredCount = assigningCount;
            if (subTreeInfo.parent != null && prevNode == subTreeInfo.parent.Id)
            {
                subTreeInfo.anonyRequests[newgroupident].prevNode = subTreeInfo.parent.Id; //父节点
                subTreeInfo.anonyRequests[newgroupident].recvParentTime = scheduler.currentTime;

            }
            else
            {
                subTreeInfo.anonyRequests[newgroupident].prevNode = this.Id; //非父节点，赋值为本节点
            }

            //到了最后一站，返回
            if (assigningCount == 1 && subTreeInfo.subtree.Count == 0)
            {
                SendNewGroupResponse(rootId, k, origId, new HashSet<int>() { this.Id }, subTreeInfo.parent);
                return;
            }
        }

        /*
        public void SendNewGroupRequest(int rootId, int k, int origId, int assigningCount, HashSet<int> candidates, Node to)
        {
            Packet pkg = new Packet(this, to, PacketType.NEW_GROUP_REQUEST);
            pkg.TTL = global.longTTL;
            pkg.Data = new NewGroupRequestField(rootId, k, origId, assigningCount, candidates);
            this.retryOnSendingFailture = true;
            SendData(pkg);
            this.retryOnSendingFailture = false;
        }
        
        public void RecvNewGroupRequest(Packet pkg)
        {
            if (this.Id != pkg.Dst)
            {
                this.retryOnSendingFailture = true;
                RoutePacket(pkg);
                this.retryOnSendingFailture = false;
                return;
            }
            NewGroupRequestField newGroupRequest = (NewGroupRequestField)pkg.Data;
            int rootId = newGroupRequest.rootId;            
            int origId = newGroupRequest.origId;
            int k = newGroupRequest.k;
            int assignedCount = newGroupRequest.assigningCount;
            HashSet<int> candidates = newGroupRequest.candidates;


            ProcessNewGroup(rootId, k, assignedCount, origId, pkg.Src);
        }*/

        public void SendNewGroupPing(int rootId, int k, int origId, int dst)
        {
            this.retryOnSendingFailture = true;
            Packet pkg = new Packet(this, Node.getNode(dst, NodeType.READER), PacketType.NEW_GROUP_PING);
            pkg.TTL = global.longTTL;
            //pkg.Data = new NewGroupRequestField(rootId, k, origId, -1, null);
            pkg.Data = new NewGroupRequestField(rootId, k, origId, 0, 0, 0, 0);
            SendData(pkg);
            this.retryOnSendingFailture = false;
        }

        public void RecvNewGroupPing(Packet pkg)
        {
            if (pkg.Next != this.Id && pkg.Next!= BroadcastNode.Node.Id)
                return;
            if (this.Id != pkg.Dst)
            {
                this.retryOnSendingFailture = true;
                RoutePacket(pkg);
                this.retryOnSendingFailture = false;
                return;
            }

            NewGroupRequestField newGroupRequest = (NewGroupRequestField)pkg.Data;
            int rootId = newGroupRequest.rootId;
            int k = newGroupRequest.k;
            int origId = newGroupRequest.origId;

            string key = rootId + "";
            string pingkey = rootId + "-" + origId + "-" + k + ":" + pkg.Src;

            AnonyTreeEntry subTreeInfo = this.CachedTreeEntries[key];
            subTreeInfo.pingRecords[pingkey] = scheduler.currentTime;
        }

        public void SendNewGroupResponse(int rootId, int k, int origId, HashSet<int> result, Node to)
        {
            //Console.WriteLine("debug: list:{0}, ++reader{1}", Utility.DumpHashIntSet(result), this.id);
            this.retryOnSendingFailture = true;
            Packet pkg = new Packet(this, to, PacketType.NEW_GROUP_RESPONSE);
            pkg.TTL = global.longTTL;
            pkg.NewGroupResponse = new NewGroupResponseField(rootId, k, origId, result);
            SendData(pkg);
            this.retryOnSendingFailture = false;
        }


        public bool AllSubNodeRetrieveNewGroup(int rootId, int origId, int k)
        {
            string newgroupident = rootId + "-" + origId + "-" + k;
            string key = rootId + "";

            AnonyTreeEntry subTreeInfo = this.CachedTreeEntries[key];
            foreach (AnonyRequest r in subTreeInfo.anonyRequests.Values)
            {
                if (r.responseStatus != SubNodeStatus.NORMAL)
                    return false;
            }
            return true;

        }




        //建立匿名树
        public void SendTreeGroupRequest(int rootId, int m, double L, double l, double preAngle, int hops)
        {
            SendTreeGroupRequest(rootId, m, BroadcastNode.Node.Id,  L, l, preAngle, hops);
        }
        public void SendTreeGroupRequest(int rootId, int m, int dst, double L, double l, double preAngle, int hops)
        {
            if (this.Neighbors.Count == 0)
                return;
            string key = rootId + "";
            if (this.CachedTreeEntries.ContainsKey(key))
            {
                foreach (int c in this.CachedTreeEntries[key].subtree.Keys)
                {
                    SubTreeEntry e = this.CachedTreeEntries[key].subtree[c];
                    if (e.status == SubNodeStatus.RETRIEVING)
                        Console.WriteLine("Warning, {0} is still retrieving... ", c);
                    if (e.status == SubNodeStatus.NORMAL1)
                        e.status = SubNodeStatus.NORMAL;
                }
            }
            Packet pkg = new Packet(this, Node.getNode(dst, NodeType.READER), PacketType.INIT_TREE_REQUEST);
            pkg.Data = new InitTreeRequestField(rootId, m, L, l, preAngle, hops);
            SendPacketDirectly(scheduler.currentTime, pkg);
        }

        public void RecvTreeGroupRequest(Packet pkg)
        {

            if (pkg.Next != this.Id && pkg.Next!= BroadcastNode.Node.Id)
                return;
            if (pkg.Prev == Id && pkg.PrevType == type)
                return;

            InitTreeRequestField treeGroupRequest = (InitTreeRequestField)pkg.Data;
            int rootId = treeGroupRequest.rootId;
            int m = treeGroupRequest.m;
            double L = treeGroupRequest.L;
            double r1 = treeGroupRequest.l;
            double preAngle = treeGroupRequest.preAngle;            
            int hops = treeGroupRequest.hops;


            string key = rootId + "" ;
            AnonyTreeEntry subTreeInfo = null;



            //只接收一次，第二次忽略，m大于0除外。m大于0且非父亲节点也忽略
            if ((this.CachedTreeEntries.ContainsKey(key) && m < 0) 
                || this.Id == rootId 
                ||(this.CachedTreeEntries.ContainsKey(key) && m > 0 && pkg.Prev != this.CachedTreeEntries[key].parent.Id && this.CachedTreeEntries[key].status != SubNodeStatus.OUTSIDE))
                return;



            Reader snode = global.readers[pkg.Src];
            PrivacyNeighbor snb = (PrivacyNeighbor)this.Neighbors[snode.Id];
            //更新位置不应该在这里完成，但是减少计算开销，就放在这里吧
            UpdateNeighborLocation(snb);

            double x = 0;
            double angle = 0;
            if (L > 0)
            { //需要检查距离
                double r2 = snb.dist;
                //x为本节点到A0的距离
                if (r1 == 0)
                {
                    x = snb.dist;
                    angle = snb.angle - Math.PI; //角度相差pi
                }
                else
                {
                    x = Math.Sqrt(r1 * r1 + r2 * r2 - 2 * r1 * r2 * Math.Cos(preAngle - (3.14 + snb.angle)));
                    angle = preAngle - Math.Acos((x * x + r1 * r1 - r2 * r2) / (2 * x * r1));
                }

                if (!this.CachedDistEntries.ContainsKey(rootId))
                    this.CachedDistEntries.Add(rootId, new DistanceEntry(rootId, x, scheduler.currentTime));
                else
                {
                    this.CachedDistEntries[rootId].dist = x;
                    this.CachedDistEntries[rootId].time = scheduler.currentTime;
                }
            }
            else
            {
                x = r1;
                angle = preAngle;
            }

            //第一次收到请求
            if(!this.CachedRegionEntries.ContainsKey(key))
                this.CachedRegionEntries.Add(key, new AnonyRegionEntry(rootId, -1));

            if (L > 0)
            {
                if (x > L) //匿名区域外的边缘节点
                {
                    //SendInitRegionResponse(new List<int>(), origId);
                    //添加一项，但hops为-1，表示在区域外
                    this.CachedRegionEntries[key].hops = -1;
                    //return; 下面还有事情要做
                }
                else
                {
                    hops++;
                    this.CachedRegionEntries[key].hops = hops;
                }
            }


            //this.CachedTreeEntries.Add(key, new AnonyTreeEntry(rootId, (Reader)Node.getNode(pkg.Prev, NodeType.READER), m));


            //Console.WriteLine("m:{0}", m);

            //非匿名区域节点
            if (this.CachedRegionEntries.ContainsKey(key) && this.CachedRegionEntries[key].hops < 0 && m < 0)
            {
                //记录，已经收到过了，防止cn重复
                this.CachedTreeEntries.Add(key, new AnonyTreeEntry(rootId, snode, m));
                subTreeInfo = this.CachedTreeEntries[key];
                subTreeInfo.status = SubNodeStatus.OUTSIDE;
                SendSubTreeInfo(rootId, null, SubNodeStatus.OUTSIDE, 0, snode);
                return;
            }

            //第一次收到正式的请求
            int newm = m;
            if (!this.CachedTreeEntries.ContainsKey(key))
            {
                this.CachedTreeEntries.Add(key, new AnonyTreeEntry(rootId, snode, m));
                subTreeInfo = this.CachedTreeEntries[key];
                newm = m - 1;
            }
            else
            {
                subTreeInfo = this.CachedTreeEntries[key];
                subTreeInfo.parent = snode;
            }

            if (this.Id != rootId)
            {
                //List<int> list = null;
                List<int> list = getSubTreeNode(rootId);
                int cn = subTreeInfo.cn;

                if (newm != 0) //需要继续分配m的
                {
                    subTreeInfo.status = SubNodeStatus.RETRIEVING;
                    SendSubTreeInfo(rootId, list, SubNodeStatus.RETRIEVING, cn, snode);
                    SendTreeGroupRequest(rootId, newm, L, x, angle, hops);

                    subTreeInfo.checkSubTreeTime = scheduler.currentTime + global.waitChildDelay;
                    Event.AddEvent(new Event(scheduler.currentTime + global.waitChildDelay, EventType.CHK_SUBTREE, this, new List<int>() { rootId }));                    
                }
                else //否则直接返回
                {
                    subTreeInfo.status = SubNodeStatus.NORMAL1;
                    SendSubTreeInfo(rootId, list, SubNodeStatus.NORMAL1, cn, snode);
                }
            }
        }

        public void DumpTreeNodes(string key, int k)
        {
            Dictionary<int, SubTreeEntry> subtree = this.CachedTreeEntries[key].subtree;
            foreach (int c in subtree.Keys)
            {
                Console.WriteLine("{0}->{1}", this.Id, c);
                PrivacyReader r = (PrivacyReader)Node.getNode(c, NodeType.READER);
                r.DumpTreeNodes(key, k);
            }
        }

        public void PrintGroupNodes(int k)
        {
            Console.WriteLine("{0:F4} [NEW_GROUP] {1}{2} group:({3})", scheduler.currentTime, this.type, this.Id, Utility.DumpHashIntSet(this.anonGroups.groups[k]));
        }

        public void PrintGroupNodes(HashSet<int> group)
        {
            Console.WriteLine("{0:F4} [NEW_GROUP] {1}{2} group:({3})", scheduler.currentTime, this.type, this.Id, Utility.DumpHashIntSet(group));
        }

        public List<int> getSubTreeNode(int rootId)
        {
            string key = rootId + "";
            AnonyTreeEntry subTreeInfo = this.CachedTreeEntries[key];

            List<int> list = new List<int>();
            foreach (KeyValuePair<int, SubTreeEntry> pair in subTreeInfo.subtree)
            {
                HashSet<int> subnodes = pair.Value.subnodes;
                list.Add(pair.Key);
                list.AddRange(subnodes);
            }
            return list;
        }

        public void CheckSubTree(object obj)
        {
            List<int> l = (List<int>)obj;
            int rootId = l[0];
            string key = rootId + "";
            AnonyTreeEntry subTreeInfo = this.CachedTreeEntries[key];

            Console.WriteLine("Reader{0} check subtree", this.Id);

            int cn = subTreeInfo.cn;
            Reader parent = subTreeInfo.parent;

            int[] cs = subTreeInfo.subtree.Keys.ToArray<int>();
            foreach (int c in cs)
            {
                int scn = subTreeInfo.subtree[c].subcn;
                SubNodeStatus status = subTreeInfo.subtree[c].status;
                //如果有一个节点是在匿名区域内，则放弃，有其他的处理机制

                ////////////////
                if((status == SubNodeStatus.NORMAL || status == SubNodeStatus.NORMAL1)
                    && scheduler.currentTime - subTreeInfo.subtree[c].subnodeLastPing > 1.5 * global.beaconInterval)
                //if (Utility.Distance(this, (Reader)Node.getNode(c, NodeType.READER)) > global.nodeMaxDist && status != SubNodeStatus.OUTSIDE) //以前在匿名区域内，但现在移出
                {
                    Console.WriteLine("debug: READER{0} removes subtree READER{1} for not receiving lastping", this.Id, c);
                    subTreeInfo.cn -= subTreeInfo.subtree[c].subcn;
                    //subTreeInfo.subtree[c].status = SubNodeStatus.OUTSIDE;
                    subTreeInfo.subtree.Remove(c);

                }
                if (status == SubNodeStatus.RETRIEVING)
                {
                    //Console.WriteLine("WARNING: so long RETRIEVING???");//正常吧？
                    return;
                }
                if (status == SubNodeStatus.OUTSIDE)
                    return;
                SendPingRequest(c, global.longTTL);
            }
            //否则没有需要等待的节点，可发送subtree响应
            ////为在匿名区域内的边缘节点，树的叶节点            
            List<int> list = getSubTreeNode(rootId);
            subTreeInfo.status = SubNodeStatus.NORMAL;
            SendSubTreeInfo(rootId, list, SubNodeStatus.NORMAL, cn, parent);
        }

        public void SendSubTreeInfo(int rootId, List<int> list, SubNodeStatus status, int cn, Node parent)
        {
            Packet pkg = new Packet(this, parent, PacketType.SUBTREE_INFO);
            pkg.SubTreeInfo = new SubTreeInfoField(list, (int)status, cn, rootId);
            SendData(pkg);
            //debug
            Console.WriteLine("debug: status:{0} cn:{1} list:{2}", status, cn, Utility.DumpListIntSet(list));
        }

        //收到子树的响应
        public void RecvSubTreeInfo(Packet pkg)
        {
            if (pkg.Next != this.Id && pkg.Next!= BroadcastNode.Node.Id)
                return;
            if (pkg.Prev == Id && pkg.PrevType == type)
                return;

            if (this.Id != pkg.Dst)
            {
                this.retryOnSendingFailture = true;
                RoutePacket(pkg);
                this.retryOnSendingFailture = false;
                return;
            }

            int rootId = pkg.SubTreeInfo.rootId;
            List<int> newsubtree = pkg.SubTreeInfo.subtree;
            SubNodeStatus newstatus = (SubNodeStatus)pkg.SubTreeInfo.status;
            int newcn = pkg.SubTreeInfo.cn;
            int child = pkg.Src;


            string key = rootId + "";
            if (!this.CachedTreeEntries.ContainsKey(key))
                return;

            AnonyTreeEntry subTreeInfo = this.CachedTreeEntries[key];


            //update是子树节点是否有更新
            bool update = false;

            if (newstatus == SubNodeStatus.OUTSIDE)//对方为非匿名区域节点
            {
                subTreeInfo.cn++;
            }
            else if (!subTreeInfo.subtree.ContainsKey(child)) //新的子节点
            {
                if (newsubtree != null)
                {
                    subTreeInfo.subtree.Add(child, new SubTreeEntry());
                    subTreeInfo.subtree[child].subnodeLastPing = scheduler.currentTime;
                    foreach (int x in newsubtree)
                    {
                        subTreeInfo.subtree[child].subnodes.Add(x);
                    }
                }
                if (newstatus != SubNodeStatus.RETRIEVING)
                    subTreeInfo.cn += newcn;
                if (!subTreeInfo.subtree.ContainsKey(child))
                    subTreeInfo.subtree.Add(child, new SubTreeEntry());
                subTreeInfo.subtree[child].subcn = newcn;
                subTreeInfo.subtree[child].status = newstatus;
                subTreeInfo.subtree[child].hops = 1;
                subTreeInfo.subtree[child].subnodeLastPing = scheduler.currentTime;
            }
            else
            {
                //更新子树
                HashSet<int> subnodes = subTreeInfo.subtree[child].subnodes;
                int oldcn = subTreeInfo.subtree[child].subcn;

                List<int> temp = new List<int>();
                foreach (int c in subnodes)
                {
                    if (!newsubtree.Contains(c))
                    {
                        temp.Add(c);
                        update = true;
                    }
                }
                foreach (int c in temp)
                {
                    subnodes.Remove(c);
                }
                foreach (int x in newsubtree)
                {
                    if (!subnodes.Contains(x))
                    {
                        subnodes.Add(x);
                        update = true;
                    }
                }
                if (subTreeInfo.subtree[child].status != newstatus)
                    update = true;
                subTreeInfo.cn = subTreeInfo.cn + (newcn - oldcn);
                subTreeInfo.subtree[child].subcn = newcn;
                subTreeInfo.subtree[child].status = newstatus;
                subTreeInfo.subtree[child].subnodeLastPing = scheduler.currentTime;
            }
            subTreeInfo.lastSubTreeUpdateTime = scheduler.currentTime;


            /*
            Console.WriteLine("{0}---{1}", child, newstatus);
            foreach (KeyValuePair<int, SubTreeEntry> pair in subTreeInfo.subtree)
            {
                int c = pair.Key;
                SubNodeStatus status = pair.Value.status;
                Console.WriteLine("{0}->{1}", c, status);
            }
            */

            //update1是本区域是否还有等待的节点
            bool update1 = true;
            if (scheduler.currentTime < subTreeInfo.checkSubTreeTime)
                update1 = false;
            else
            {
                foreach (KeyValuePair<int, SubTreeEntry> pair in subTreeInfo.subtree)
                {
                    int c = pair.Key;
                    SubNodeStatus status = pair.Value.status;
                    if (status == SubNodeStatus.RETRIEVING && Utility.Distance(this, (Reader)Node.getNode(c, NodeType.READER)) < global.nodeMaxDist)//无需更新
                    {
                        update1 = false;
                        Console.WriteLine("reader{0} is still retrieving, waiting...", c);
                        break;
                    }
                }
            }

            List<int> sublist = getSubTreeNode(rootId);
            //Console.WriteLine("debug: cached subtree count {0}:{1}, update:{2}, update1:{3}", sublist.Count, Utility.DumpListIntSet(sublist), update, update1);

            if (this.Id != rootId)//中间节点
            {
                //需要更新
                if (update == true && update1 == true)
                {
                    //Event.AddEvent(new Event(scheduler.CurrentTime + global.waitChildDelay, EventType.CHK_SUBTREE, this, new List<int>() { rootId }));
                    List<int> list = getSubTreeNode(rootId);
                    subTreeInfo.status = SubNodeStatus.NORMAL;
                    SendSubTreeInfo(rootId, list, SubNodeStatus.NORMAL, subTreeInfo.cn, subTreeInfo.parent);
                }
            }
            else //根节点
            {
      
                //检查
                List<int> temp = new List<int>();
                //找到所有达到要求的匿名组
                foreach (int k in this.anonGroups.ks)
                {
                    int totalUnavailAnonyNodeCout = GetTotalUnavailAnonyNodeCout(subTreeInfo, k);
                    int totalNodeCount = getSubTreeCount(subTreeInfo.subtree) + 1;

                    if (totalNodeCount - totalUnavailAnonyNodeCout >= k)
                    {
                        //DumpTreeNodes(k);
                        temp.Add(k);
                    }
                    else//不满足要求
                    {
                        //看看是否有节点未返回
                        foreach (int c in subTreeInfo.subtree.Keys)
                        {
                            SubTreeEntry subtree = subTreeInfo.subtree[c];
                            //还有一个子节点未返回结果
                            if (subtree.status == SubNodeStatus.RETRIEVING)
                            {
                                Console.WriteLine("debug: child READER{0} is still retrieving, waiting...", c);
                                return;
                            }
                        }


                        //全部有结果，则重来
                        Console.WriteLine("cached subtree: {0}", Utility.DumpListIntSet(getSubTreeNode(rootId)));
                        //还没达到k要求，重新发送
                        Console.WriteLine("New round-----------------------------------------------------------------------");
                        int m = k - getSubTreeCount(subTreeInfo.subtree) - 1;
                        foreach (KeyValuePair<int, SubTreeEntry> pair in subTreeInfo.subtree)
                        {
                            int c = pair.Key;
                            SubTreeEntry e = pair.Value;
                            if (m <= 0)
                                break;
                            if (e.subcn == 0)
                                continue;
                            //进位，不四舍五入了,合理的数为m*(e.subcn/subTreeInfo.cn)+1，但是为了加速，就乘以2,
                            int newm = (int)(1 + 2 * m * ((float)e.subcn / subTreeInfo.cn));
                            SendTreeGroupRequest(this.Id, newm, c, -1, 0, 0, 0);
                            Console.WriteLine("send to {0}, m:{1}", c, newm);
                        }
                        Console.WriteLine("total: {0}", getSubTreeCount(subTreeInfo.subtree) + 1);
                        DumpTreeNodes(key, k);
                        return;
                    }
                }

                //发送setgroup请求
                foreach (int k in temp)
                {
                    string newgroupident = rootId + "-" + rootId + "-" + k;

                    this.anonGroups.ks.Remove(k);
                    if (!this.anonGroups.groups.ContainsKey(k))
                        this.anonGroups.groups.Add(k, new HashSet<int>());

                    HashSet<int> group = this.anonGroups.groups[k];
                    Dictionary<int, HashSet<int>> tempgroup = new Dictionary<int, HashSet<int>>();

                    if (!this.anonGroups.groups[k].Contains(this.Id))
                        this.anonGroups.groups[k].Add(this.Id);

                    foreach (KeyValuePair<int, SubTreeEntry> pair in subTreeInfo.subtree)
                    {
                        int c = pair.Key;
                        HashSet<int> l = pair.Value.subnodes;

                        this.anonGroups.groups[k].Add(c);

                        tempgroup.Add(c, new HashSet<int>());
                        tempgroup[c].Add(c);

                        int end = 0;
                        if (group.Count + l.Count + 1 > k)
                            end = k - group.Count;
                        else
                            end = l.Count;

                        int i = 0;
                        foreach (int x in l)
                        {
                            if (i == end)
                                break;
                            this.anonGroups.groups[k].Add(x);
                            tempgroup[c].Add(x);
                            i++;
                        }

                        if (group.Count >= k)
                            break;
                    }


                    foreach (KeyValuePair<int, HashSet<int>> pair in tempgroup)
                    {
                        int c = pair.Key;
                        string ckey = c + "-" + k;
                        HashSet<int> subnodes = pair.Value;
                        if (!this.anonGroups.subUnavailAnonyNodes.ContainsKey(ckey))
                            this.anonGroups.subUnavailAnonyNodes.Add(ckey, new HashSet<int>());

                        Utility.AddHashSet(this.anonGroups.subUnavailAnonyNodes[ckey], subnodes);
                        SendSetGroup(rootId, rootId, k, c, this.anonGroups.groups[k], subnodes);//这里rootId和origId应该是一样的

                        //TODO subUnavailAnonyNodeCounts在节点消失的时候应该减少的
                    }

                    PrintGroupNodes(k);
                    DumpTreeNodes(key, k);
                    PrivacyReader.rootReader = this;

                    //为建立新组作准备，将所有节点放到一个框内
                    if (PrivacyReader.AnonFrames.GetFrameCount() == 0)
                        PrivacyReader.AnonFrames.Init(this.anonGroups.groups[k], this.Id, false);
                    PrivacyReader.AnonFrames.DumpFrames(this.Id);
                }
            }
            Console.WriteLine("newstatus:{0}, current cn:{1}", newstatus, subTreeInfo.cn);
        }

        void SendSetGroup(int rootId, int origId, int k, int child, HashSet<int> group, HashSet<int> subnodes)
        {
            //Console.WriteLine("subnodes:{0}", Utility.DumpHashIntSet(subnodes));
            this.retryOnSendingFailture = true;
            Packet pkg = new Packet(this, Node.getNode(child, NodeType.READER), PacketType.SET_GROUP);
            pkg.SetGroup = new SetGroupField(rootId, origId, k, group, subnodes);
            SendData(pkg);
            this.retryOnSendingFailture = false;
        }

        void RecvSetGroup(Packet pkg)
        {
            if (pkg.Next != this.Id && pkg.Next!= BroadcastNode.Node.Id)
                return;
            if (this.Id != pkg.Dst)
            {
                this.retryOnSendingFailture = true;
                RoutePacket(pkg);
                this.retryOnSendingFailture = false;
                return;
            }

            int rootId = pkg.SetGroup.rootId;
            int origId = pkg.SetGroup.origId;
            int k = pkg.SetGroup.k;
            HashSet<int> group = pkg.SetGroup.group;
            HashSet<int> subnode = pkg.SetGroup.subnodes;

            if (global.method == 1 || global.method == 2)//native
            {
                this.anonGroups.groups.Add(k, group);
                Console.WriteLine("Reader{0} is set to {1}-group", this.Id , k);
                return;
            }

            string key = rootId + "";

            if (!this.CachedTreeEntries.ContainsKey(key))
                return;

            AnonyTreeEntry subTreeInfo = this.CachedTreeEntries[key];

            if (subnode == null)
            {
                if (subTreeInfo.subtree.ContainsKey(pkg.Src)) //子节点向上传递
                {
                    HashSet<int> tmp = new HashSet<int>();
                    string newgroupident = rootId + "-" + origId + "-" + k;
                    Utility.CopyHashSet(tmp, group);
                    string key1 = pkg.Src + "-" + k;
                    if (!this.anonGroups.subUnavailAnonyNodes.ContainsKey(key1))
                        this.anonGroups.subUnavailAnonyNodes.Add(key1, new HashSet<int>());
                    Utility.AddHashSet(this.anonGroups.subUnavailAnonyNodes[key1], group);
                    if (subTreeInfo.parent != null)
                        SendSetGroup(rootId, origId, k, subTreeInfo.parent.Id, group, null);
                }
                else
                {
                    Console.WriteLine("debug: reader{0} is not my subnode.", pkg.Src);
                }
                return;
            }
            else
            {

                if (this.anonGroups.ks.Contains(k))
                    this.anonGroups.ks.Remove(k);

                if (subnode.Contains(this.Id) && !this.anonGroups.groups.ContainsKey(k))
                {
                    this.anonGroups.groups.Add(k, new HashSet<int>());
                    Utility.CopyHashSet(this.anonGroups.groups[k], group);
                    Console.WriteLine("Reader{0} is set to {1}-group", Id, k);
                }

                foreach (KeyValuePair<int, SubTreeEntry> pair in subTreeInfo.subtree)
                {
                    int c = pair.Key;
                    HashSet<int> list = pair.Value.subnodes;

                    HashSet<int> newsubnode = new HashSet<int>();

                    foreach (int x in subnode)
                    {
                        if (c == x || list.Contains(x))
                            newsubnode.Add(x);
                    }
                    if (newsubnode.Count > 0)
                        SendSetGroup(rootId, origId, k, c, group, newsubnode);


                    string ckey = c + "-" + k;
                    if (this.anonGroups.subUnavailAnonyNodes.ContainsKey(ckey))
                        Utility.AddHashSet(this.anonGroups.subUnavailAnonyNodes[ckey], subnode);
                }
            }
        }

        public int getSubTreeCount(Dictionary<int, SubTreeEntry> tree)
        {
            int count = 0;
            foreach (KeyValuePair<int, SubTreeEntry> pair in tree)
            {
                HashSet<int> subnodes = pair.Value.subnodes;
                count += (1 + subnodes.Count);
            }
            return count;
        }


        public PrivacyNeighbor getFartherestNeighbor(List<PrivacyNeighbor> nbs)
        {
            PrivacyNeighbor maxnb = null;
            foreach (PrivacyNeighbor nb in nbs)
            {
                if (maxnb == null || maxnb.dist > nb.dist)
                    maxnb = nb;
            }
            return maxnb;
        }

        //计算多个匿名组的交集
        public HashSet<int> GetGroupInsection()
        {
            HashSet<int> insection = null;
            //这里我们直接计算某个树节点所在组的交集，其实应该是存放于每个父节点上的
            PrivacyGlobal global = (PrivacyGlobal)Global.getInstance();

            
            foreach (KeyValuePair<int, HashSet<int>> pair in this.anonGroups.groups)
            {
                HashSet<int> group = pair.Value;
                if (insection == null)
                {
                    insection = new HashSet<int>();
                    foreach (int x in group)
                        insection.Add(x);
                }
                else
                {
                    HashSet<int> temp = new HashSet<int>();
                    foreach (int x in insection)
                    {
                        if (!group.Contains(x))
                            temp.Add(x);
                    }
                    foreach (int x in temp)
                    {
                        insection.Remove(x);
                    }
                }
            }
            return insection;
        }


        //计算最小匿名组，就是组成员最少
        public HashSet<int> GetMinGroup()
        {
            HashSet<int> ming = null;         
            PrivacyGlobal global = (PrivacyGlobal)Global.getInstance();

            foreach (KeyValuePair<int, HashSet<int>> pair in this.anonGroups.groups)
            {
                HashSet<int> group = pair.Value;
                if (ming == null)
                {
                    ming = group;
                }
                else
                {
                    if (group.Count < ming.Count)
                        ming = group;
                }
            }
            return ming;
        }

        public static void CalculateGroups()
        {
            PrivacyGlobal global = (PrivacyGlobal)Global.getInstance();
            Scheduler scheduler = Scheduler.getInstance();
            for (int i = 0; i < global.readerNum; i++)
            {
                PrivacyReader reader = (PrivacyReader)global.readers[i];
                if (reader.anonGroups.groups.Count == 0)
                    continue;
                HashSet<int> insection = reader.GetGroupInsection();
                Console.WriteLine("{0:F4} [READER_GROUP_STAT] {1}{2}", scheduler.currentTime, "READER", reader.Id);
                foreach (KeyValuePair<int, HashSet<int>> pair1 in reader.anonGroups.groups)
                {
                    int k = pair1.Key;
                    HashSet<int> group = pair1.Value;
                    Console.WriteLine("{0:F4} [READER_GROUP] {1}{2} k={3} group:({4})", scheduler.currentTime, "READER", reader.Id, k, Utility.DumpHashIntSet(group));
                }
                HashSet<int> ming = reader.GetMinGroup();
                Console.WriteLine("{0:F4} [READER_INS_GROUP] {1}{2}  group:({3})", scheduler.currentTime, "READER", reader.Id, Utility.DumpHashIntSet(insection));
                Console.WriteLine("{0:F4} [READER_MIN_GROUP] {1}{2}  group:({3})", scheduler.currentTime, "READER", reader.Id, Utility.DumpHashIntSet(ming));
                
            }
            return;
        }




        override public void SendBeacon(float time)
        {
            Packet pkg = new Packet();
            pkg.Type = PacketType.BEACON;
            pkg.Src = pkg.Prev = Id;
            pkg.Dst = pkg.Next = -1;//Broadcast
            pkg.TTL = 1;

            pkg.Beacon = new BeaconField();
            if (this.gatewayEntities.Count > 0)
            {
                pkg.Beacon.gatewayEntities = new GatewayEntity[this.gatewayEntities.Count];
                int i = 0;
                foreach (int g in this.gatewayEntities.Keys)
                {
                    pkg.Beacon.gatewayEntities[i++] = new GatewayEntity(g, this.Id, this.gatewayEntities[g].hops);
                }
            }
            SendPacketDirectly(time, pkg);

            /* TODO
            foreach (AnonyTreeEntry subTreeInfo in this.CachedTreeEntries.Values)
            {
                foreach (int c in subTreeInfo.subtree.Keys)
                    SendPingRequest(c, global.longTTL);
            }
             */

            float nextBeacon = 0;
            if (scheduler.currentTime < global.beaconWarming)
                nextBeacon = (float)(Utility.P_Rand(10 * (global.beaconWarmingInterval + 0.4)) / 10);//0.5是为了设定最小值
            else
                nextBeacon = (float)(Utility.P_Rand(10 * global.beaconInterval) / 10);
            Event.AddEvent(new Event(scheduler.currentTime + nextBeacon, EventType.SND_BCN, this, null));
        }



        public new Neighbor AddNeighbor(Reader nb)
        {
            if (!this.Neighbors.ContainsKey(nb.Id))
                this.Neighbors.Add(nb.Id, new PrivacyNeighbor(nb));
            if (!this.routeTable.ContainsKey(nb.Id))
                this.routeTable.Add(nb.Id, new RouteEntity(nb.Id, nb.Id, 1, scheduler.currentTime, scheduler.currentTime));
            return this.Neighbors[nb.Id];
        }


        public void UpdateNeighborLocation(PrivacyNeighbor nb)
        {
            //检测对方的位置
            nb.dist = Utility.Distance(this, nb.node);
            nb.angle = Utility.IncludedAngle(this, nb.node);
        }
        override public void RecvBeacon(Packet pkg)
        {
            Scheduler scheduler = Scheduler.getInstance();
            Reader node = global.readers[pkg.Src];

            if (pkg.Prev == Id && pkg.PrevType == type)
                return;

            PrivacyNeighbor nb = null;
            if (Neighbors.ContainsKey(node.Id))
            {
                nb = (PrivacyNeighbor)Neighbors[node.Id];
            }
            if (nb != null)
            {
                nb.lastBeacon = scheduler.currentTime;
            }
            else
            {
                //Add as a neighbor
                nb = (PrivacyNeighbor)AddNeighbor(node);
                nb.lastBeacon = scheduler.currentTime;
            }

            if (!this.routeTable.ContainsKey(pkg.Prev))
                this.routeTable.Add(pkg.Prev, new RouteEntity(pkg.Prev, pkg.Prev, 1, scheduler.currentTime, scheduler.currentTime));
            else
            {
                this.routeTable[pkg.Prev].hops = 1;
                this.routeTable[pkg.Prev].next = pkg.Prev;
                this.routeTable[pkg.Prev].remoteLastUpdatedTime = scheduler.currentTime;
                this.routeTable[pkg.Prev].localLastUpdatedTime = scheduler.currentTime;   
            }

            foreach (KeyValuePair<string, AnonyTreeEntry> pair in this.CachedTreeEntries)
            {
                AnonyTreeEntry subTreeInfo = pair.Value;
                Node parent = subTreeInfo.parent;

                /*
                if (subTreeInfo.subtree.ContainsKey(pkg.Prev))
                {
                    subTreeInfo.subtree[pkg.Prev].lastconfirm = scheduler.CurrentTime;
                    subTreeInfo.subtree[pkg.Prev].hops = 1;
                }
                if (parent!=null && parent.Id == pkg.Prev)
                {
                    subTreeInfo.parentconfirmed = scheduler.CurrentTime;
                    subTreeInfo.parenthops = 1;
                }*/
            }

            if (pkg.Beacon != null)
            {
                if (pkg.Beacon.gatewayEntities != null)
                {
                    for (int i = 0; i < pkg.Beacon.gatewayEntities.Length; i++)
                    {
                        GatewayEntity g = pkg.Beacon.gatewayEntities[i];
                        if (!this.gatewayEntities.ContainsKey(g.gateway))
                        {
                            this.gatewayEntities.Add(g.gateway, new GatewayEntity(g.gateway, g.next, g.hops + 1));
                            Console.WriteLine("{0:F4} [{1}] {2}{3} add a gateway of {4} hops {5}.", scheduler.currentTime, pkg.Type, this.type, this.Id, g.gateway, g.hops);
                        }
                        else if (this.gatewayEntities[g.gateway].hops > g.hops + 1)
                        {
                            this.gatewayEntities[g.gateway].hops = g.hops + 1;
                            this.gatewayEntities[g.gateway].next = g.next;
                            Console.WriteLine("{0:F4} [{1}] {2}{3} update a gateway of {4} hops {5}.", scheduler.currentTime, pkg.Type, this.type, this.Id, g.gateway, g.hops);
                        }
                        else if (this.gatewayEntities[g.gateway].next == g.next)//update in case of the next hop moves
                            this.gatewayEntities[g.gateway].hops = g.hops + 1;
                    }
                }
            }
        }

        public List<PrivacyNeighbor> getNeighborsFromRage(double startAngle, double includedAngle)
        {
            List<PrivacyNeighbor> list = new List<PrivacyNeighbor>();
            foreach (PrivacyNeighbor nb in this.Neighbors.Values)
            {
                if (nb.angle >= startAngle && nb.angle < startAngle + includedAngle)
                    list.Add(nb);
            }
            return list;
        }

        //检测附近的标签，本项目省略交互过程，直接添加了
        public override void NotifyObjects()
        {
            List<ObjectNode> list = GetAllNearObjects(this, global.objectMaxDist);

            this.NearbyObjectCache.Clear();
            foreach (ObjectNode node in list)
            {
                this.NearbyObjectCache.Add(node.Id, new ObjectEntity(node.Id, node.OrgId, scheduler.currentTime));
            }
        }


        public override void CheckNeighbors()
        {
            List<int> temp = new List<int>();
            foreach (Neighbor nb in Neighbors.Values)
            {
                if (scheduler.currentTime - nb.lastBeacon > global.checkNeighborInterval)
                {
                    temp.Add(nb.node.Id);
                }
            }
            foreach (int t in temp)
            {
                Neighbors.Remove(t);
                routeTable.Remove(t);
                //Console.WriteLine("Node " + id + " remove neighbor " + t);
            }

            Event.AddEvent(new Event(scheduler.currentTime + global.checkNeighborInterval, EventType.CHK_NB, this, null));
        }
        public static void AppendHashSet(HashSet<int> to, HashSet<int> from)
        {
            foreach (int x in from)
                to.Add(x);
        }


        public override void SendPingRequest(int nodeId, int ttl)
        {
            Reader node = global.readers[nodeId];
            Packet pkg = new Packet(this, node, PacketType.PING_REQUEST);
            pkg.TTL = ttl;
            SendAODVData(pkg);
        }


        public override void RecvPingRequest(Packet pkg)
        {
            Reader node = global.readers[pkg.Prev];
            if (pkg.Dst == this.Id && pkg.DstType == this.type)
            {
                Packet pkg1 = new Packet(this, node, PacketType.PING_RESPONSE);
                pkg1.TTL = global.TTL; ;
                SendAODVData(pkg1);
            }
            else
            {
                RoutePacket(pkg);
            }
        }

        public override void RecvPingResponse(Packet pkg)
        {
            Reader node = global.readers[pkg.Prev];
            if (pkg.Dst == this.Id && pkg.DstType == this.type)
            {
                foreach (KeyValuePair<string, AnonyTreeEntry> pair in this.CachedTreeEntries)
                {
                    AnonyTreeEntry subTreeInfo = pair.Value;
                    Node parent = subTreeInfo.parent;

                    /*
                    if (this.id != subTreeInfo.rootId && parent.Id == pkg.Src)
                    {
                        subTreeInfo.parentconfirmed = scheduler.CurrentTime;
                        subTreeInfo.parenthops = global.pingTTL-pkg.TTL;
                    }
                    else if(subTreeInfo.subtree.ContainsKey(pkg.Src))
                    {
                        subTreeInfo.subtree[pkg.Src].lastconfirm = scheduler.CurrentTime;
                        subTreeInfo.subtree[pkg.Src].hops = global.pingTTL - pkg.TTL+1;
                    }*/
                }
            }
            else
            {
                RoutePacket(pkg);
            }
        }



        public override void ProcessPacket(Packet pkg)
        {
            //I send the packet myself, ignore
            if (pkg.Prev == Id && pkg.PrevType == type)
            {
                return;
            }

            if (pkg.Next == this.Id ||pkg.Next == BroadcastNode.Node.Id)
            {
                if (pkg.Type == PacketType.BEACON
                    || pkg.Type == PacketType.INIT_TREE_REQUEST
                    || pkg.Type == PacketType.SUBTREE_INFO
                    || pkg.Type == PacketType.SET_GROUP
                    || pkg.Type == PacketType.NEW_GROUP_PING
                    || pkg.Type == PacketType.NEW_GROUP_REQUEST
                    || pkg.Type == PacketType.NEW_GROUP_RESPONSE
                    || pkg.Type == PacketType.NATIVE_GROUP_REQUEST
                    || pkg.Type == PacketType.NATIVE_GROUP_RESPONSE
                    || pkg.Type == PacketType.NATIVE_LONG_GROUP_REQUEST
                    || pkg.Type == PacketType.NATIVE_LONG_GROUP_RESPONSE)
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}({6}->{7}->{8})", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev, pkg.Src, pkg.Prev, pkg.Dst);
            }

            //如果不存在邻居中，则添加.
            //如果存在，则更新时间
            //if (pkg.Beacon == null && !this.Neighbors.ContainsKey(pkg.Prev) && pkg.PrevType == NodeType.READER)
            if (pkg.Beacon == null && pkg.PrevType == NodeType.READER)
                RecvBeacon(pkg);

            switch (pkg.Type)
            {
                //Readers
                case PacketType.BEACON:
                    RecvBeacon(pkg);
                    break;
                case PacketType.INIT_TREE_REQUEST:
                    RecvTreeGroupRequest(pkg);
                    break;
                case PacketType.SUBTREE_INFO:
                    RecvSubTreeInfo(pkg);
                    break;
                case PacketType.SET_GROUP:
                    RecvSetGroup(pkg);
                    break;
                case PacketType.NEW_GROUP_PING:
                    RecvNewGroupPing(pkg);
                    break;
                case PacketType.NEW_GROUP_REQUEST:
                    RecvNewGroupRequest(pkg);
                    break;
                case PacketType.NEW_GROUP_RESPONSE:
                    RecvNewGroupResponse(pkg);
                    break;
                case PacketType.NATIVE_GROUP_REQUEST:
                    RecvNativeGroupRequest(pkg);
                    break;
                case PacketType.NATIVE_GROUP_RESPONSE:
                    RecvNativeGroupResponse(pkg);
                    break;
                case PacketType.NATIVE_LONG_GROUP_REQUEST:
                    RecvSetLongNativeGroupRequest(pkg);
                    break;
                case PacketType.NATIVE_LONG_GROUP_RESPONSE:
                    RecvSetLongNativeGroupResponse(pkg);
                    break;

                /*
            case PacketType.NEW_GROUP_REQUEST:
                RecvNewGroupRequest(pkg);
                break;
                 * */
                //Some codes are hided in the base class.
                default:
                    base.ProcessPacket(pkg);
                    return;
            }
        }

    }
}

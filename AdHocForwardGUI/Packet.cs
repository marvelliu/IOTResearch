using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace AdHocBaseApp
{
    public enum PacketType
    {
        BEACON,
        PING_REQUEST,
        PING_RESPONSE,
        FORWARD_STRATAGY_REQUEST,
        FORWARD_STRATAGY_REPLY,
        DATA,
        COMMAND,
        LANDMARK,
        DATA_AVAIL,
        AODV_REQUEST,
        AODV_REPLY,
        LOCATION_UPDATE,
        LOCATION_QUERY,
        LOGICAL_PATH_UPDATE,
        LOGICAL_PATH_REQUEST,
        LOGICAL_PATH_REPLY,
        CERTIFICATE,
        CERTIFICATE_FAIL,
        RSU_JOIN,
        RSU_NEW_BACKBONE_REQUEST,
        RSU_NEW_BACKBONE_RESPONSE,
        RSU_CA_FORWARD,
        TAG_HEADER,
        NODE_REPORT,
        EVENT_REPORT,
        NODE_TYPE_REPORT,
        AUTHORIZATION,
        SET_MONITOR,
        GET_MONITOR_REQUEST,
        GET_MONITOR_RESPONSE,
        INIT_TREE_REQUEST,
        INIT_REGION_REQUEST,
        SUBTREE_INFO,
        SET_GROUP,
        NEW_GROUP_CANDIDATE_REQUEST,
        NEW_GROUP_CANDIDATE_RESPONSE,
        NEW_GROUP_REQUEST,
        NEW_GROUP_PING,
        NEW_GROUP_RESPONSE,
        NATIVE_GROUP_REQUEST,
        NATIVE_GROUP_RESPONSE,
        NATIVE_LONG_GROUP_REQUEST,
        NATIVE_LONG_GROUP_RESPONSE,
        SW_DATA,
        UNKNOWN
    }

    [Serializable]
    public class Location
    {
        public double X;
        public double Y;
        public Location()
        {
            this.X = 0;
            this.Y = 0;
        }
        public Location(double x, double y)
        {
            this.X = x;
            this.Y = y;
        }
    }

    [Serializable]
    public class GatewayEntity
    {
        public int gateway;
        public int hops;
        public int next;
        public GatewayEntity(int gateway, int next, int hops)
        {
            this.gateway = gateway;
            this.next = next;
            this.hops = hops;
        }
    }

    [Serializable]
    public class BeaconField
    {
        public GatewayEntity[] gatewayEntities;
    }

    [Serializable]
    public class AODVRequestField
    {
        public int src;
        public int dst;
        public int hops;
        public AODVRequestField(int src, int dst, int hops)
        {
            this.src = src;
            this.dst = dst;
            this.hops = hops;
        }
    }

    [Serializable]
    public class SWRequestField
    {
        public int origDst;
        public int origSrc;
        public PacketType origType;
        public int origSenderSeq;
        public int swTTL;

        public SWRequestField(int origSrc, int origDst, int origSenderSeq, PacketType origType, int ttl)
        {
            this.origSrc = origSrc;
            this.origDst = origDst;
            this.origSenderSeq = origSenderSeq;
            this.origType = origType;
            this.swTTL = ttl;
        }
    }



    [Serializable]
    public class BackboneEntity
    {
        public int backbone;
        public int hops;
        public int next;
        public BackboneEntity(int backbone, int next, int hops)
        {
            this.backbone = backbone;
            this.next = next;
            this.hops = hops;
        }
    }

    [Serializable]
    public class VANETBeaconField
    {
        public GatewayEntity backboneEntity;
    }

    [Serializable]
    public class VANETRSUJoinField
    {
        public int id;
        public int hops;
        public Certificate cert;
        public bool isWired;

        public VANETRSUJoinField(int id, int hops, Certificate cert, bool isWired)
        {
            this.id = id;
            this.hops = hops;
            this.cert = cert;
            this.isWired = isWired;
        }
    }

    [Serializable]
    public class VANETNewBackboneRequestField
    {
        public Certificate backboneCert;
        public VANETNewBackboneRequestField(Certificate cert)
        {
            this.backboneCert = cert;
        }
    }


    [Serializable]
    public class VANETNewBackboneResponseField
    {
        public Certificate rsuCert;
        public VANETNewBackboneResponseField(Certificate cert)
        {
            this.rsuCert = cert;
        }
    }

    [Serializable]
    public class VANETCAForwardField
    {
        public Certificate rsuCA;
        public Certificate objCA;
        public int hops;

        public VANETCAForwardField(Certificate rsuCA, Certificate objCA, int hops)
        {
            this.rsuCA = rsuCA;
            this.objCA = objCA;
            this.hops = hops;
        }
    }

    [Serializable]
    public class LandmarkNotificationField
    {
        public Location location;
        public LandmarkNotificationField(double x, double y)
        {
            this.location = new Location(x, y);
        }
    }

    [Serializable]
    public class ObjectLogicalPathUpdateField
    {
        public int obj;
        public int r;
        public int g;
        public int s;
        public float t;
        public ObjectLogicalPathUpdateField(int obj)
        {
            this.obj = obj;
        }
    }

    [Serializable]
    public class ObjectLocationQueryRequestField
    {
        public int obj;
        public ObjectLocationQueryRequestField(int obj)
        {
            this.obj = obj;
        }
    }

    [Serializable]
    public class ObjectLocationQueryReplyField
    {
        public ObjectLocation location;
        public int obj;
        public ObjectLocationQueryReplyField(int obj, ObjectLocation l)
        {
            this.obj = obj;
            if(l!=null)
                this.location = new ObjectLocation(l.Id,l.X, l.Y, l.T);
        }
        public override string ToString()
        {
            return this.obj + ": " + (this.location == null ? "Unknown" : location.ToString());
        }
    }

    [Serializable]
    public class ObjectUpdateLocationField
    {
        //unnesseary since only one server.
        int server;
        public double updateTime;
        public Location location;
        public int obj;
        public ObjectUpdateLocationField(Location l, float time, int server, int obj)
        {
            this.location = new Location(l.X, l.Y);
            this.updateTime = time;
            this.server = server;
            this.obj = obj;
        }
    }

    [Serializable]
    public class ObjectLogicalPathQueryServerReplyField
    {
        public int obj;
        public int gateway;
        public int reader;
        public float time;
        public ObjectLogicalPathQueryServerReplyField(int obj, int gateway, int reader,float time)
        {
            this.obj = obj;
            this.gateway = gateway;
            this.reader = reader;
            this.time = time;
        }
        public override string ToString()
        {
            return string.Format("{0}:({1}, {2}, {3})",this.obj,this.gateway,this.reader, this.time);
        }
    }

    [Serializable]
    public class ObjectLogicalPathQueryRequestField
    {
        public int obj;
        public int gateway;
        public int reader;
    }

    [Serializable]
    public class ObjectLogicalPathQueryReplyField
    {
        public int obj;
        public int gateway;
        public Shape shape;
        public int querier;

        public override string ToString()
        {
            return obj+":"+shape;
        }
    }

    [Serializable]
    public class ObjectUpdateLinkPathField
    {
        public int id;
        public int r;
        public int g;
    }

    [Serializable]
    ///////////////////New Trust Approach/////////////////
    public class DataInfoField
    {
        public int lastReader;
        public int packetHash;
        public int seq;
        public DataInfoField(int lastReader, int packetHash, int seq)
        {
            this.lastReader = lastReader;
            this.packetHash = packetHash;
            this.seq = seq;
        }
    }



    //////////////////Trust Approach/////////////////
    [Serializable]
    public class ObjectTagHeaderField
    {
        public int orgId;
        public int tagId;
        public int networkId;
        public ObjectTagHeaderField(int tagId, int orgId)
        {
            this.orgId = orgId;
            this.tagId = tagId;
        }
    }

    [Serializable] 
    public class TrustReportField
    {
        public byte[] result;
        public long size;
        public int org;
        public TrustReportField(int org, byte[] result, long size)
        {
            this.org = org;
            //this.result = new byte[result.Length];
            //result.CopyTo(this.result, 0);
            this.result = result;
            this.size = size;
        }
    }

    [Serializable]
    public class AuthorizationField
    {
        public int[] tags;
        public int[] keys;
        public AuthorizationField(int[] tags, int[] keys)
        {
            this.tags = tags;
            this.keys = keys;
        }
    }

    [Serializable]
    public class GetMonitorRequestField
    {
        public int[] orgs;
        public int network;
        public int requestOrg;
        public GetMonitorRequestField(int[] orgs, int network, int requestOrg)
        {
            this.orgs = orgs;
            this.network = network;
            this.requestOrg = requestOrg;
        }
    }

    [Serializable]
    public class GetMonitorResponseField
    {
        public int network;
        //推荐的节点和机构
        public int monitorNode;
        public int monitorOrg;
        public GetMonitorResponseField(int monitorNode, int monitorOrg, int network)
        {
            this.monitorNode = monitorNode;
            this.monitorOrg = monitorOrg;
            this.network = network;
        }
    }

    [Serializable]
    public class InitTreeRequestField
    {
        public int rootId;
        public int m;  //至少要添加的节点
        public double L;
        public double l;
        public double preAngle;
        public int hops;

        public InitTreeRequestField(int rootId,int m, double L, double l, double preAngle, int hops)
        {
            this.rootId = rootId;
            this.m = m;
            this.L = L;
            this.l = l;
            this.preAngle = preAngle;
            this.hops = hops;
        }
    }

    [Serializable]
    public class InitTreeResponseField
    {
        public int origId;
        public int seq;
        public InitTreeResponseField(int origId, int seq)
        {
            this.origId = origId;
            this.seq = seq;
        }
    }

    [Serializable]
    public class InitRegionRequestField
    {
        public int origId;
        public int seq;
        public double L;
        public double l;
        public double preAngle;
        public int hops;

        public InitRegionRequestField(double L, double l, double preAngle, int hops, int orgId, int seq)
        {
            this.L = L;
            this.l = l;
            this.preAngle = preAngle;
            this.hops = hops;
            this.origId = orgId;
            this.seq = seq;
        }
    }

    [Serializable]
    public class SubTreeInfoField
    {
        public int rootId;
        public int seq;
        public List<int> subtree;
        public int cn;
        public int status;

        public SubTreeInfoField(List<int> children, int status, int cn, int orgId)
        {
            this.subtree = children;
            this.status = status;
            this.cn = cn;
            this.rootId = orgId;
        }
    }

    [Serializable]
    public class SetGroupField
    {
        public int rootId;
        public int k;
        public int origId;
        public HashSet<int> group;
        public HashSet<int> subnodes;

        public SetGroupField(int rootId, int origId, int k, HashSet<int> group, HashSet<int> subnodes)
        {
            this.rootId = rootId;
            this.origId = origId;
            this.k = k;
            this.group = group;
            this.subnodes = subnodes;
        }
    }
    
    
    [Serializable]
    public class AddNewGroupCandidateField
    {
        public int rootId;
        public int k;
        public int origId;
        public HashSet<int> set;

        public double L;
        public double l;
        public double preAngle;
        public int hops;

        public AddNewGroupCandidateField(int rootId, int k, int origId, HashSet<int> set, double L, double l, double preAngle, int hops)
        {
            this.rootId = rootId;
            this.k = k;
            this.origId = origId;
            this.set = set;
            this.L = L;
            this.l = l;
            this.preAngle = preAngle;
            this.hops = hops;
        }
    }

    [Serializable]
    public class NewGroupRequestField
    {
        public int rootId;
        public int k;
        public int origId;
        public int assigningCount;
        public HashSet<int> candidates;

        public NewGroupRequestField(int rootId, int k, int origId, int assignedCount, HashSet<int> candidates)
        {
            this.rootId = rootId;
            this.k = k;
            this.origId = origId;
            this.assigningCount = assignedCount;
            this.candidates = candidates;
        }
    }

    [Serializable]
    public class NewGroupResponseField
    {
        public int rootId;
        public int k;
        public int origId;
        public HashSet<int> result;

        public NewGroupResponseField(int rootId, int k, int origId, HashSet<int> result)
        {
            this.rootId = rootId;
            this.k = k;
            this.origId = origId;
            this.result = result;
        }
    }

    [Serializable]
    public class NativeGroupRequestField
    {
        public int origId;
        public int k;
        public int h;
        public int h0;
        public NativeGroupRequestField(int origId, int k, int h, int h0)
        {
            this.origId = origId;
            this.k = k;
            this.h = h;
            this.h0 = h0;
        }
    }
    
    [Serializable]
    public class SetLongNativeGroupResponseField
    {
        public int k;
        public bool avail;
        public SetLongNativeGroupResponseField(int k, bool avail)
        {
            this.k = k;
            this.avail = avail;
        }
    }
    

    [Serializable]
    public class CommandField
    {
        public int tag;
        public int operation;
        public CommandField(int tag, int operation)
        {
            this.tag = tag;
            this.operation = operation;
        }
    }


    [Serializable]
    public class SetMonitorResponseField
    {
        public int monitorNetwork;
        //推荐的节点和机构
        public int monitorNode;
        public int monitorOrg;
        public SetMonitorResponseField(int monitorNode, int monitorOrg, int network)
        {
            this.monitorNode = monitorNode;
            this.monitorOrg = monitorOrg;
            this.monitorNetwork = network;
        }
    }

    [Serializable]
    public class Packet :ICloneable
    {
        public int Src;
        public NodeType SrcType;
        public int Dst;
        public NodeType DstType;
        public int Prev;
        public int DelPacketNode;//指示减少源节点发送包的目的节点

        //Default type
        public NodeType PrevType = NodeType.READER;

        public int Next;
        public NodeType NextType = NodeType.READER;
        public PacketType Type;
        public object Data;
        public int origTTL;
        public int TTL;
        public uint Tags;

        public int SrcSenderSeq; //数据包本身的编号，用于区分其他数据包的标识，取决于pkg.Src的发送数据包数
        public int PrevSenderSeq; //pkg.Prev节点发送的数据包数
        public bool seqInited = false;

        public bool inited = false;

        public BeaconField Beacon;
        public AODVRequestField AODVRequest;
        public SWRequestField SWRequest;
        public LandmarkNotificationField LandmarkNotification;
        public ObjectUpdateLocationField ObjectUpdateLocation;        
        public ObjectLocationQueryRequestField ObjectLocationRequest;
        public ObjectLocationQueryReplyField ObjectLocationReply;

        /////////////////Logical Path Approach//////////////

        //s->q
        public ObjectLogicalPathQueryServerReplyField ObjectLogicalPathQueryServerReply;
        //q->g
        public ObjectLogicalPathQueryRequestField ObjectLogicalPathQueryReqeust;
        //g->q
        public ObjectLogicalPathQueryReplyField ObjectLogicalPathQueryReply;

        public ObjectLogicalPathUpdateField ObjectLogicalPathUpdate;


        //////////////////VANET Approach/////////////////
        public VANETBeaconField VANETBeacon;
        public Certificate VANETCertificate;
        public VANETRSUJoinField VANETRSUJoin;
        public VANETNewBackboneRequestField VANETNewBbReq;
        public VANETNewBackboneResponseField VANETNewBbRsp;
        public VANETCAForwardField VANETCaForward;
        public float beginSentTime;

        ///////////////////New Trust Approach////////////
        public DataInfoField DataInfo;

        //////////////////Trust Approach/////////////////
        public ObjectTagHeaderField ObjectTagHeader;
        public TrustReportField TrustReport;
        public AuthorizationField Authorization;
        public GetMonitorRequestField GetMonitorRequest;
        public GetMonitorResponseField GetMonitorResponse;
        public SetMonitorResponseField SetMonitorResponse;
        public CommandField Command;
        public int AppId;


        
        ///////////////////////Location Pricacy//////////////////////////////
        public InitRegionRequestField InitRegionRequest;
        public SubTreeInfoField SubTreeInfo;        
        public InitTreeResponseField InitTreeResponse;
        public SetGroupField SetGroup;
        
        public NewGroupResponseField NewGroupResponse;
        


        public Packet()
        {
            this.Type = PacketType.UNKNOWN;
            this.SrcSenderSeq = -1; //SrcSenderSeq和PrevSenderSeq初始值为负，表明该数据包的id未定
            this.PrevSenderSeq = -1;
        }

        public Packet(Node src, Node dst, PacketType type)
        {
            this.Src = this.Prev = src.Id;
            this.Dst = this.Next = dst.Id;
            this.SrcType = this.PrevType = src.type;
            this.DstType = this.NextType = dst.type;
            this.Type = type;
            this.TTL = Global.getInstance().TTL;
            this.DelPacketNode = this.Dst;
            this.beginSentTime = Scheduler.getInstance().currentTime;
            this.SrcSenderSeq = -1; //SrcSenderSeq和PrevSenderSeq初始值为负，表明该数据包的id未定
            this.PrevSenderSeq = -1;
        }

        public Packet(Node src, Node dst, PacketType type, float time)
            : this(src, dst, type)
        {
            this.beginSentTime = time;
        }

        public static bool PacketEqual(Packet a, Packet b)
        {
            if (a.Type == b.Type
                && a.Src == b.Src && a.SrcType == b.SrcType
                && a.Dst == b.Dst && a.DstType == b.DstType
                && a.SrcSenderSeq == b.SrcSenderSeq)
                return true;
            else
                return false;
        }

        public string getId()
        {
            return this.SrcType.ToString() + this.Src + "-" + this.DstType.ToString() + this.Dst + ":" + this.SrcSenderSeq;
        }
                
        public static bool PacketHeadEqual(Packet a, Packet b)
        {
            if (a.Type == b.Type
                && a.Src == b.Src && a.SrcType == b.SrcType
                && a.Dst == b.Dst && a.DstType == b.DstType)
                return true;
            else
                return false;
        }
                     
        public static bool PacketDataEqual(Packet a, Packet b)
        {
            return a.SrcSenderSeq == b.SrcSenderSeq;
        }

        public object Clone()
        {
            MemoryStream stream = new MemoryStream();
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(stream, this);
            stream.Position = 0;

            return formatter.Deserialize(stream);
        }

        public static bool IsSamePacket(Packet a, Packet b)
        {
            if (a == null && b != null)
                return false;
            else if (a != null && b == null)
                return false;
            else
                return (a.SrcSenderSeq == b.SrcSenderSeq && a.Src == b.Src && a.Dst == b.Dst);
        }
    }
}

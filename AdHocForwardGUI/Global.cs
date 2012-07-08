using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Reflection;

namespace AdHocBaseApp
{
    public enum RouteMethod
    {
        TAGGED_AODV,
        AODV,
        Adaptive,
        SmallWorld,
        CBRP //Cluster Based Routing Protocol
    }

    public delegate Global GlobalConstructor();
    public class GlobalProducer
    {
        static public GlobalConstructor globalConstructor;
    }

    public class Global
    {

        public static Global ProduceGlobal()
        {
            return new Global();
        }

        protected Global() { }

        protected static Global instance = null;
        public static Global getInstance()
        {
            if (instance == null)
                instance = GlobalProducer.globalConstructor();
            return instance;
        }
        public int PacketSeq = 0;

        //For logical path
        public float ReversePathCacheTimeout = 10;

        public RouteMethod routeMethod = RouteMethod.AODV;

        public Reader[] readers = null;
        public int readerNum = -1;
        public bool listenNeighborAODVReply = false;

        public Organization[] orgs = new Organization[0];

        public ObjectNode[] objects = new ObjectNode[0];
        public int objectNum = -1;

        public Querier[] queriers = new Querier[0];
        public int querierNum = -1;

        public Server server = null;

        public bool debug = false;

        public float nodeDist = -1;
        public float nodeMaxDist = -1;
        public float objectMaxDist = -1;        
        public int orgNum;
        public OrgGenType orgGenType;
        public float PoissonLamda = -1;
        public double layoutX = -1;
        public double layoutY = -1;

        public float startTime = -1;
        public float endTime = -1;
        public float step = -1;
        public float beaconWarming = -1;
        public float beaconInterval = 10;
        public float beaconWarmingInterval = 1;
        public float checkNeighborInterval = 15;
        public float checkNearObjInterval = 15;
        public float checkPendingPacketInterval = 10;

        public float serverProcessDelay = 0.2f;
        public float internetDelay = 0.2f;
        public float processDelay = 0.01f;
        public double lightSpeed = 299792458;

        public float maxLocationUpdateInterval = 5;
        public float minLocationUpdateInterval = 0.2f;

        public double delta = 0.001f;

        public string configFileName = "config.txt";
        public string eventsFileName = "events.txt";    
        public int TTL = 5;

        //最多发送回数
        public int SendEventNum = 10;
        public int SendEventDuration = 1;//每回发送时间
        public int SendEventInterval = 1;//每回中每次发送的间隔

        public double refSpeed = 1;

        //draw
        public bool drawLine = false;

        //public SortedList<double, Event> events = new SortedList<double,Event>();
        public List<Event> events = new List<Event>();

        public double[] orgRatio = null;

        //如果AODV reply是给邻居的，那本节点就不做处理
        public bool IngoreNeigbhorAODVReply = true;



        public ObjectNodeConstructor objectNodeConstructor;
        public ReaderConstructor readerConstructor;
        public QuerierConstructor querierConstructor;
        public ServerConstructor serverConstructor;
        public OrganizationConstructor organizationConstructor;

        //Automatically quit when done
        public bool automatic = false;
        public bool nodraw = false;

        public BaseForm mainForm;

        //Logical Path
        public bool CheckReverseRouteCache = false;
        public bool CheckGatewayCache = false;

        ///VANET
        public int vanetNetworkSize = -1;
        public float wiredProportion = 0;
        public bool vanetCaForward = false;
        public float checkCertDelay = 100000;

        public bool LoadConfigFile()
        {
            return LoadConfigFile(configFileName);
        }

        public virtual bool LoadConfigFile(string filename)
        {
            string line = null;
            StreamReader sr = null;
            string[] seperators = { "\t", " ", ":" };
            if (serverConstructor != null)
                server = serverConstructor();
            Console.WriteLine("Configs start");
            sr = new StreamReader(filename);
            for (line = sr.ReadLine(); line != null; line = sr.ReadLine())
            {
                if (line=="" || line[0] == '#')
                    continue;
                string[] v = line.Split(seperators, StringSplitOptions.RemoveEmptyEntries);
                ParseArgs(v);
            }
            Console.WriteLine("Configs end");
            if (sr != null)
                sr.Close();
            return true;
        }

        public virtual void ParseArgs(string []v)
        {
            if (v[0] == "node_num")
            {
                readerNum = int.Parse(v[1]);
                readers = new Reader[readerNum];
            }
            else if (v[0] == "object_num" && objectNodeConstructor != null)
            {
                objectNum = int.Parse(v[1]);
                objects = new ObjectNode[objectNum];
                for (int i = 0; i < objectNum; i++)
                    objects[i] = objectNodeConstructor(i);
            }
            else if (v[0] == "querier_num" && querierConstructor !=null)
            {
                querierNum = int.Parse(v[1]);
                queriers = new Querier[querierNum];
                for (int i = 0; i < querierNum; i++)
                    queriers[i] = querierConstructor(i);
            }
            else if (v[0] == "event_file")
            {
                eventsFileName = v[1];
                int i1 = eventsFileName.IndexOf("-s")+2;
                int i2 = eventsFileName.IndexOf("-", i1+1);
                string s = eventsFileName.Substring(i1, i2 - i1);
                double s1 = double.Parse(s);
                refSpeed = 1 / Math.Sqrt((s1+1) / 5);
                if (refSpeed > 1)
                    refSpeed *= 2;
            }
            else if (v[0] == "node_dist")
                nodeDist = float.Parse(v[1]);
            else if (v[0] == "node_maxdist")
                nodeMaxDist = float.Parse(v[1]);
            else if (v[0] == "object_maxdist")
                objectMaxDist = float.Parse(v[1]);
            else if (v[0] == "org_num")
                orgNum = int.Parse(v[1]);
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
            else if (v[0] == "Poisson_Lamda")
                PoissonLamda = float.Parse(v[1]);
            else if (v[0] == "layout_x")
                layoutX = double.Parse(v[1]);
            else if (v[0] == "layout_y")
                layoutY = double.Parse(v[1]);
            else if (v[0] == "start")
                startTime = float.Parse(v[1]);
            else if (v[0] == "end")
                endTime = float.Parse(v[1]);
            else if (v[0] == "step")
                step = float.Parse(v[1]);
            else if (v[0] == "beacon_interval")
                beaconInterval = float.Parse(v[1]);
            else if (v[0] == "beacon_warming")
                beaconWarming = float.Parse(v[1]);
            else if (v[0] == "beacon_warming_interval")
                beaconWarmingInterval = float.Parse(v[1]);
            else if (v[0] == "check_reverse_route_cache")
                CheckReverseRouteCache = bool.Parse(v[1]);
            else if (v[0] == "check_gateway_cache")
                CheckGatewayCache = bool.Parse(v[1]);
            else if (v[0] == "random_seed")
            {
                int seed = int.Parse(v[1]);
                Utility.ran = new Random(seed);
            }
            else if (v[0] == "ttl")
                TTL = int.Parse(v[1]);
            else if (v[0] == "nodraw")
                nodraw = bool.Parse(v[1]);
            else if (v[0] == "debug")
                debug = bool.Parse(v[1]);
            else if (v[0] == "routeMethod")
                routeMethod = (RouteMethod)Enum.Parse(typeof(RouteMethod), v[1], true);
            Console.WriteLine(v[0] + ":" + v[1]);
        }

        public void AssertConfig()
        {
            Debug.Assert(readerNum > 0, "NodeNum<=0 !");
            Debug.Assert(nodeDist > 0, "NodeDist<=0 !");
            Debug.Assert(orgNum > 0, "OrgNum<=0 !");
            Debug.Assert(layoutX > 0, "layoutX<=0 !");
            Debug.Assert(layoutY > 0, "layoutY<=0 !");
            Debug.Assert(startTime >= 0, "startTime<0 !");
            Debug.Assert(endTime > 0, "endTime<=0 !");
        }

    }
}

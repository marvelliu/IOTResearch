using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.IO;
using System.Diagnostics;

namespace AdHocBaseApp
{
    public enum OrgGenType
    {
        LOG,
        AVG,
        Poisson,
        CUS1
    }
    public enum OrgName
    {
        Org1=0, 
        Org2,
        Org3,
        Org4,
        Org5,
        Org6,
        Org7,
        Org8,
        Org9,
        Org10 
    }

    [Serializable]
    public class Certificate
    {
        public int Id;
        public int[] PubKey;
        public int CAId;
        public int[] CAPubKey;
        public int authedRSUId;
        public Certificate(int id)
        {
            this.Id = id;
        }

        public Certificate(int id, int[] cert, int caId, int[] caPubKey)
        {
            this.Id = id;
            this.PubKey = new int[cert.Length];
            cert.CopyTo(this.PubKey, 0);
            this.CAId = caId;
            this.CAPubKey = caPubKey;
        }
        static int RootCAId = -1;
        static int[] RootCACert = new int[] { -1, (int)NodeType.CA, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }; //128 bits        
        static public Certificate RootCA = new Certificate(RootCAId, RootCACert, RootCAId, RootCACert);

        public bool IsValid()
        {
            if (CAId != RootCA.CAId)
                return false;
            for (int i = 0; i < CAPubKey.Length; i++)
            {
                if(CAPubKey[i] != RootCA.CAPubKey[i])
                    return false;
            }
            if (Id == RootCA.CAId && PubKey[0] == (int)NodeType.CA && PubKey[1] == RootCA.CAId) //CA
                return true;
            else if (this.PubKey[1] == this.Id && (this.PubKey[0] == (int)NodeType.OBJECT || this.PubKey[0] == (int)NodeType.OBJECT))
                return true;
            return false;
        }

        public string getStrPubKey()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < this.PubKey.Length; i++)
            {
                sb.Append(this.PubKey[i]);
            }
            return sb.ToString();
        }
    }
    public delegate Organization OrganizationConstructor(int id, string name);





    public class Organization:Node
    {
        private Global global;
        string name;

        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        public List<Reader> nodes;

        public static Organization ProduceOrganization(int id, string name)
        {
            return new Organization(id, name);
        }

        public Organization(int id, string name):base(id)
        {
            this.global = Global.getInstance();
            this.Id = id;
            this.name = name;
            this.type = NodeType.ORG;
            nodes = new List<Reader>();
        }

        protected int sentPacketCount = 0;
        
        public static Color[] colors = {Color.Green, Color.Red, Color.Plum, Color.Purple, Color.RoyalBlue, Color.SandyBrown, Color.SteelBlue, Color.Tomato, Color.Yellow, Color.Black};
                
        public static void GenerateOrganizations()
        {
            Global global = Global.getInstance();
            double totalv = 0;
            double[] v = new double[global.orgNum];
            int totalnode = 0;

            if (global.orgGenType == OrgGenType.Poisson)
            {
                for (int i = 0; i < global.orgNum; i++)
                {
                    v[i] = Utility.P_Rand(global.PoissonLamda);
                    //if(i!=0)
                    //    v[i] += v[i - 1];
                    totalv += v[i];
                }
                totalnode = 0;
                for (int i = 0; i < global.orgNum; i++)
                {
                    v[i] = v[i] * global.readerNum / totalv;
                    totalnode += (int)v[i];
                }
                v[global.orgNum-1] += (global.readerNum - totalnode);
            }
            else if (global.orgGenType == OrgGenType.CUS1)//指定每个机构的节点的比例
            {
                for (int i = 0; i < global.orgNum; i++)
                {
                    v[i] = global.orgRatio[i]*global.readerNum;
                    totalv += v[i];
                }
                v[global.orgNum - 1] += (global.readerNum - totalv);
            }

            totalnode = 0;
            global.orgs = new Organization[global.orgNum];
            for (int i = 0; i < global.orgNum; i++)
            {
                global.orgs[i] = global.organizationConstructor(i, ((OrgName)i).ToString());

                for (int j = 0; j < (int)v[i]; j++)
                {
                    Reader node = global.readers[totalnode + j];
                    node.OrgId = i;
                    global.orgs[i].nodes.Add(node);
                    Console.WriteLine("Organization{0} adds reader{1}", i, node.Id);
                }
                totalnode += (int)v[i];
            }   
        }


        public static void GenerateNodes()
        {
            Global global = Global.getInstance();
            for (int i = 0; i < global.readerNum; i++)
            {
                Reader reader = global.readerConstructor(i, (int)Utility.U_Rand(0, global.orgNum));
                global.readers[i] = reader;
            }
        }


        public static void GenerateNodePositions()
        {
            Global global = Global.getInstance();
            for (int i = 0; i < global.orgNum; i++)
            {
                double x = Utility.U_Rand(global.layoutX);
                double y = Utility.U_Rand(global.layoutY);
                if (global.orgs[i].nodes.Count > 0)
                {
                    global.orgs[i].nodes[0].X = x;
                    global.orgs[i].nodes[0].Y = y;
                    //TODO
                    //global.orgs[i].nodes[0].SetAsGateway();
                }

                for (int j = 1; j < global.orgs[i].nodes.Count; j++)
                {
                    double dist = Utility.P_Rand(global.nodeDist);
                    if (dist > global.nodeMaxDist)
                        dist = global.nodeMaxDist;
                    double angle = Utility.U_Rand(2*Math.PI);
                    x = dist * Math.Sin(angle);
                    y = dist * Math.Cos(angle);
                    global.orgs[i].nodes[j].X = global.orgs[i].nodes[j - 1].X + x;
                    global.orgs[i].nodes[j].Y = global.orgs[i].nodes[j - 1].Y + y;
                    
                    if ((global.orgs[i].nodes[j].X > global.layoutX || global.orgs[i].nodes[j].X < 0)
                        && (global.orgs[i].nodes[j].Y > global.layoutY || global.orgs[i].nodes[j].Y < 0))
                    {
                        global.orgs[i].nodes[j].X = Utility.U_Rand(global.layoutX);
                        global.orgs[i].nodes[j].Y = Utility.U_Rand(global.layoutY); ;
                        //TODO
                        //global.orgs[i].nodes[j].SetAsGateway();
                    }

                    if (global.orgs[i].nodes[j].X < 0)
                        global.orgs[i].nodes[j].X = 1;
                    if (global.orgs[i].nodes[j].X > global.layoutX)
                        global.orgs[i].nodes[j].X = global.layoutX;
                    if (global.orgs[i].nodes[j].Y < 0)
                        global.orgs[i].nodes[j].Y = 1;
                    if (global.orgs[i].nodes[j].Y > global.layoutY)
                        global.orgs[i].nodes[j].Y = global.layoutY;
                }
            }
        }


        public static void GenerateNodePositionsAllRandom()
        {
            Global global = Global.getInstance();

            //先放置于无穷远
            for (int i = 0; i < global.readerNum; i++)
            {
                global.readers[i].X = 10000;
                global.readers[i].Y = 10000;
            }

            for (int i = 0; i < global.readerNum; i++)
            {
                double x = 0, y = 0;
                //double maxdist = 0;
                x = Utility.U_Rand(global.layoutX);
                y = Utility.U_Rand(global.layoutY);
                global.readers[i].X = x;
                global.readers[i].Y = y;
            }
            /*
            for (int i = 0; i < global.orgs[0].nodes.Count; i++)
            {
                double x = 0, y = 0;
                //double maxdist = 0;
                x = Utility.U_Rand(global.layoutX);
                y = Utility.U_Rand(global.layoutY);
                global.orgs[0].nodes[i].X = x;
                global.orgs[0].nodes[i].Y = y;
            }
             */
        }
        


        public static void GenerateObjectPositionsAllRandom()
        {
            Global global = Global.getInstance();

            //先放置于无穷远
            for (int i = 0; i < global.objectNum; i++)
            {
                global.objects[i].X = 10000;
                global.objects[i].Y = 10000;
            }
            for (int i = 0; i < global.objectNum; i++)
            {
                //do{
                double x = Utility.U_Rand(global.layoutX);
                double y = Utility.U_Rand(global.layoutY);
                global.objects[i].X = x;
                global.objects[i].Y = y;
                //} while (global.readers[i].GetAllNearReaders(global.nodeDist, true).Count == 0 && i > global.readerNum / 5);
            }
        }



        public override bool SendPacketDirectly(float time, Packet pkg)
        {
            pkg.Prev = Id;
            Console.WriteLine("{0:F4} [{1}] {2}{3} sends to {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.NextType, (pkg.Next == -1 ? "all" : pkg.Next.ToString()));

            float recv_time = global.serverProcessDelay + global.internetDelay;
            if (pkg.Next == -1) //Broadcast
                return true;//No such a case.
            else
            {
                Node node = null;
                switch (pkg.NextType)
                {
                    case NodeType.READER:
                        node = global.readers[pkg.Next];
                        break;
                    case NodeType.QUERIER:
                        node = global.queriers[pkg.Next];
                        break;
                    case NodeType.OBJECT:
                        node = global.objects[pkg.Next];
                        break;
                    default:
                        Debug.Assert(false, "Error Next Type!");
                        break;
                }
                pkg.PrevType = type;
                pkg.Prev = Id;
                pkg.SrcSenderSeq = this.sentPacketCount++;
                Event.AddEvent(
                    new Event(time + recv_time, EventType.RECV,
                        node, pkg));
            }
            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace AdHocBaseApp
{
    public class Node
    {
        public NodeType type;
        public int Id;
        private Global global;
        protected Scheduler scheduler = Scheduler.getInstance();
        protected int packetSeq;

        public Node(int id)
        {
            this.Id = id;
            this.packetSeq = 0;
            this.global = Global.getInstance();
        }
        public Node(int id, NodeType type)
        {
            this.Id = id;
            this.type = type;
            this.packetSeq = 0;
            this.global = Global.getInstance();
        }

        public static Node BroadcastNode = new Node(-1, NodeType.READER);

        public static Node getNode(int n, NodeType type)
        {
            Global global = Global.getInstance();
            Node node = null;
            if (type == NodeType.READER)
            {
                if (n >= global.readerNum)
                {
                    Console.WriteLine("Warning: parse READER{0} bigger than readerNum({1})", n, global.readerNum);
                    node = null;
                }
                else if (n < Reader.BroadcastNode.Id)
                    node = null;
                else if (n == Reader.BroadcastNode.Id)
                    node = Reader.BroadcastNode;
                else
                    node = global.readers[n];
            }
            else if (type == NodeType.OBJECT)
            {
                if (n >= global.objectNum)
                {
                    Console.WriteLine("Warning: parse OBJ{0} bigger than objectNum({1})", n, global.objectNum);
                    node = null;
                }
                else
                    node = global.objects[n];
            }
            else if (type == NodeType.QUERIER)
                node = global.queriers[n];
            else if (type == NodeType.ORG)
            {
                if (n >= global.orgNum)
                {
                    Console.WriteLine("Warning: parse ORG{0} bigger than orgNum({1})", n, global.orgNum);
                    node = null;
                }
                else
                    node = global.orgs[n];
            }
            else if (type == NodeType.SERVER)
                node = global.server;
            else
            {
                Console.WriteLine("Warning:Unknown node type!");
                node = null;
            }
            return node;
        }

        public static Node getNode(string s)
        {
            char t = s[0];
            string n = s.Substring(1);
            NodeType type = NodeType.CA;
            if (t == 't' || t == 'T')//Tag
                type = NodeType.OBJECT;
            else if (t == 'o' || t == 'O')//Org
                type = NodeType.ORG;
            else if (t == 'c' || t == 'C')
                type = NodeType.CA;
            else if (t == 'r' || t == 'R')
                type = NodeType.READER;
            else if (t == 'q' || t == 'Q')
                type = NodeType.QUERIER;
            else if (t == 's' || t == 'S')
                type = NodeType.SERVER;
            else
                return null;

            int num = int.Parse(n);
            return getNode(num, type);
        }

        public override string ToString()
        {
            if (type == NodeType.ORG)
                return "O" + Id;
            else if (type == NodeType.CA)
                return "O" + Id;
            else if (type == NodeType.OBJECT)
                return "T" + Id;
            else if (type == NodeType.QUERIER)
                return "Q" + Id;
            else if (type == NodeType.READER)
                return "R" + Id;
            else if (type == NodeType.SERVER)
                return "S" + Id;
            else
                return "wrong type";
        }

        public virtual void SendPacketDirectly(float time, Packet pkg)
        {
        }

        public virtual void Recv(Packet pkg)
        { }

        public virtual void ProcessPacket(Packet pkg)
        { }

        public virtual bool SendData(Packet pkg)
        {
            return true;
        }

    }
}

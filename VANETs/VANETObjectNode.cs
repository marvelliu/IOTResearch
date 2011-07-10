using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace VANETs
{
    class VANETObjectNode:ObjectNode
    {
        new public static VANETObjectNode ProduceObjectNode(int id)
        {
            return new VANETObjectNode(id);
        }


        protected VANETObjectNode(int id)
            : base(id)
        {
            int[] key = new int[32];
            key[0] = (int)NodeType.OBJECT;
            key[1] = id;
            key[2] = 0;
            Cert = new Certificate(-1, key, Certificate.RootCA.CAId, Certificate.RootCA.CAPubKey); //128 bytes
            
        }

        public Certificate Cert;


        public override void Recv(Packet pkg)
        {
            if (pkg.PrevType != NodeType.READER)
            {
                Console.WriteLine("{0:F4} [{1}] {2}{3} drop from {4}{5} due to wrong node type", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                return;
            }

            Reader node = global.readers[pkg.Prev];
            if (Utility.Distance(this, (MobileNode)node) > Global.getInstance().objectMaxDist)
            {
                Console.WriteLine("{0:F4} [{1}] {2}{3} Drop {4}{5} due to out of space", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                return;
            }

            switch (pkg.Type)
            {
                case PacketType.DATA_AVAIL: 
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    this.lastNearReader = this.global.readers[pkg.Src];
                    this.lastNearReaderTime = scheduler.currentTime;
                    foreach (Packet p in this.cachePackets)
                        SendData(p);
                    break;
                case PacketType.CERTIFICATE:
                    Console.WriteLine("{0:F4} [{1}] {2}{3} recv from {4}{5}", scheduler.currentTime, pkg.Type, this.type, this.Id, pkg.PrevType, pkg.Prev);
                    RecvCertificate(pkg);
                    break;
                default:
                    base.Recv(pkg);
                    return;
            }
            return;
        }

        public void RecvCertificate(Packet pkg)
        {
            Packet pkg1 = new Packet(this, global.readers[pkg.Src], PacketType.CERTIFICATE);
            pkg1.VANETCertificate = new Certificate(this.Id, this.Cert.PubKey, this.Cert.CAId, this.Cert.CAPubKey);
            SendPacketDirectly(scheduler.currentTime, pkg1);
        }
    }
}

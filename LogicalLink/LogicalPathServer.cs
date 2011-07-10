using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace LogicalPath
{
    class ObjectLogicalPath
    {
        public int O;
        public int R;
        public int G;
        public float T;
    }
    class LogicalPathServer: Server
    {
        Dictionary<int, ObjectLogicalPath> objectLogicalPaths;

        public LogicalPathServer()
            : base()
        {
            this.objectLogicalPaths = new Dictionary<int, ObjectLogicalPath>();
        }

        new public static Server getInstance()
        {
            if (instance == null)
                instance = new LogicalPathServer();
            return instance;
        }

        public override void Recv(Packet pkg)
        {
            switch (pkg.Type)
            {
                //Some codes are hided in the base class.
                case PacketType.LOGICAL_PATH_UPDATE:
                    UpdateLogicalPath(pkg);
                    break;
                case PacketType.LOCATION_QUERY:
                    SendObjectLogicalPath(pkg);
                    break;
                default:
                    base.Recv(pkg);
                    return;
            }
        }
        public void UpdateLogicalPath(Packet pkg)
        {
            int id = pkg.ObjectLogicalPathUpdate.obj;
            ObjectLogicalPath path = null;
            if (this.objectLogicalPaths.ContainsKey(id))
                path = this.objectLogicalPaths[id];
            else
            {
                path = new ObjectLogicalPath();
                this.objectLogicalPaths.Add(id, path);
            }

            path.O = pkg.ObjectLogicalPathUpdate.obj;
            path.R = pkg.ObjectLogicalPathUpdate.r;
            path.G = pkg.ObjectLogicalPathUpdate.g;
            path.T = pkg.ObjectLogicalPathUpdate.t;
        }

        public void SendObjectLogicalPath(Packet pkg)
        {
            Node querier = global.queriers[pkg.Src];
            Packet pkg1 = new Packet(this, querier, PacketType.LOCATION_QUERY);
            int obj = pkg.ObjectLocationRequest.obj;
            if (this.objectLogicalPaths.ContainsKey(obj))
                pkg1.ObjectLogicalPathQueryServerReply = new ObjectLogicalPathQueryServerReplyField(obj, this.objectLogicalPaths[obj].G, this.objectLogicalPaths[obj].R, this.objectLogicalPaths[obj].T);
            else
                pkg1.ObjectLogicalPathQueryServerReply = null;                

            SendPacketDirectly(scheduler.currentTime, pkg1);
        }
    }
}

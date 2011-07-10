using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AdHocBaseApp
{
    public class MobileNode:Node
    {
        public MobileNode(int id)
            : base(id)
        { }

        protected double x;
        protected double y;

        public double X
        {
            get { return x; }
            set { x = value; }
        }
        public double Y
        {
            get { return y; }
            set { y = value; }
        }

        //Move
        private List<double> dstX;
        public List<double> DstX
        {
            get { return dstX; }
            set { dstX = value; }
        }
        private List<double> dstY;

        public List<double> DstY
        {
            get { return dstY; }
            set { dstY = value; }
        }

        private List<double> speed;
        public List<double> Speed
        {
            get { return speed; }
            set { speed = value; }
        }


        public override void SendPacketDirectly(float time, Packet pkg)
        {
        }

        public override void ProcessPacket(Packet pkg)
        {
        }

    }
}

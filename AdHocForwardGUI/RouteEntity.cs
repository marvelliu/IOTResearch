using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AdHocBaseApp
{
    public class RouteEntity
    {
        public int dst;
        public long tags;
        public int next;
        public int hops;
        public double remoteLastUpdatedTime;//这个是对方声称的更新时间
        public double localLastUpdatedTime;//这个是本地缓存更新的时间

        public RouteEntity(int dst, int next, int hops, double time, double localLastUpdatedTime)
        {
            this.dst = dst;
            this.next = next;
            this.hops = hops;
            this.remoteLastUpdatedTime = time;
            this.localLastUpdatedTime = localLastUpdatedTime;
        }

        public RouteEntity(int dst, int next, int hops, double time, double localLastUpdatedTime, int tags)
        {
            this.dst = dst;
            this.next = next;
            this.hops = hops;
            this.tags = tags;
            this.remoteLastUpdatedTime = time;
            this.localLastUpdatedTime = localLastUpdatedTime;
        }
    }
}

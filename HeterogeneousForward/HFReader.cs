using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace HeterogeneousForward
{
    public abstract class HFReader:Reader
    {
        public List<ForwardStrategy> forwardStrategies;
        protected TagEntity availTagEntity = null;
        protected HFGlobal global;

        public HFReader(int id, int org)
            : base(id, org)
        {
            this.global = (HFGlobal)Global.getInstance();
            this.forwardStrategies = new List<ForwardStrategy>();
        }


        new public static HFReader ProduceReader(int id, int org)
        {
            Global global = Global.getInstance();
            if (global.routeMethod == RouteMethod.CBRP)
                return new CBReader(id, org);
            return new SWReader(id, org);
        }


        public bool IsAllowedTags(uint selfTags, uint tags)
        {
            return (selfTags | tags) == selfTags;
        }


        public bool IsAllowedTags(uint tags)
        {
            if (this.forwardStrategies != null)
            {
                if (this.availTagEntity == null)
                    this.availTagEntity = CalculateTagEntity(this.forwardStrategies);
                return (this.availTagEntity.allowTags | tags) == this.availTagEntity.allowTags;
            }
            else
                return false;
        }

        public TagEntity CalculateTagEntity(ForwardStrategy[] fs)
        {
            return CalculateTagEntity(new List<ForwardStrategy>(fs));
        }

        public TagEntity CalculateTagEntity(List<ForwardStrategy> fs)
        {
            uint allowTags = 0;

            unchecked
            {
                if (global.defaultForwardStrategyAction == ForwardStrategyAction.ACCEPT)
                    allowTags = (uint)-1;
                else
                    allowTags = 0;
            }

            List<ForwardStrategy> accecpted = new List<ForwardStrategy>();
            List<ForwardStrategy> refused = new List<ForwardStrategy>();
            foreach (ForwardStrategy f in fs)
            {
                if (f.Action == ForwardStrategyAction.ACCEPT)
                    accecpted.Add(f);
                else
                    refused.Add(f);
            }
            foreach (ForwardStrategy f in accecpted)
            {
                allowTags = allowTags | f.Tags;
            }
            foreach (ForwardStrategy f in refused)
            {
                allowTags = allowTags & ~f.Tags;
            }

            uint mask = (uint)(1 << (global.tagNameNum - 1) + 1) - 1;
            allowTags = allowTags & mask; //0(32-tagnum)1(tagnum)

            /*
            for(int i=0;i< global.tagNameNum;i++)
            {
                allowTags ^= (uint)1 << i;
                foreach (ForwardStrategy f in fs)
                {
                    if (f.Action == ForwardStrategyAction.ACCEPT)
                        continue;
                    if ((f.Tags & (ulong)1 << i) == 0)
                    {
                        allowTags &= (~((uint)1 << i));
                        break;
                    }
                }
            }*/
            int n = CaculateTagNum(allowTags);
            return new TagEntity(n, allowTags);
        }

        public int CaculateTagNum(uint tags)
        {
            int n = 0;
            for (int i = 0; i < global.tagNameNum; i++)
            {
                if ((tags & (ulong)1 << i) != 0)
                    n++;
            }
            return n;
        }

        public abstract bool IsHub();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace HeterogeneousForward
{
    class HFEventManager:EventManager
    {
        public override void ParseEventArgs(string[] array)
        {
            HFGlobal global = (HFGlobal)Global.getInstance();
            if (array[0] == "SND_DATA")
            {
                Node src = Node.getNode(array[1]);
                Node dst = Node.getNode(array[2]);
                float start = float.Parse(array[3]);
                float endtime = float.Parse(array[4]);
                float interval = float.Parse(array[5]);;
                uint tags = 0;

                if (array.Length>6 && array[6] == "tag")
                {
                    string t0 = array[7];
                    string[] t1 = t0.Split("_".ToCharArray());
                    for (int i = 0; i < t1.Length; i++)
                    {
                        uint t = (uint)(1 << (int.Parse(t1[i])));
                        tags |= t;
                    }
                }

                PacketType type = PacketType.DATA;
                if (endtime < 0)
                {
                    Packet pkg = new Packet(src, dst, type, start);
                    pkg.TTL = global.TTL;
                    pkg.Tags = tags;
                    AddEvent(new Event(start, EventType.SND_DATA, src, pkg));
                }
                else
                {
                    for (float t = start; t < endtime; t += interval)
                    {
                        Packet pkg = new Packet(src, dst, type, start);
                        pkg.TTL = global.TTL;
                        pkg.Tags = tags;
                        pkg.Tags = (uint)(t * 100);
                        AddEvent(new Event(t, EventType.SND_DATA, src, pkg));
                    }
                }
            }
            else if (array[0] == "SET_STG")
            {
                Node node = Node.getNode(array[1]);
                
                ForwardStrategyAction action = (ForwardStrategyAction)Enum.Parse(typeof(ForwardStrategyAction), array[2]);
                string[] t0 = array[4].Split("_".ToCharArray());
                ulong tags = 0;
                for (int i = 0; i < t0.Length; i++)
                {
                    ulong tag = (ulong)(1 << int.Parse(t0[i]));
                    tags |= tag;
                }

                if (node.type == NodeType.READER)
                {
                    HFReader reader = (HFReader)node;
                    reader.forwardStrategies.Add(new ForwardStrategy(tags, action));
                }
                else//org
                {
                    HFOrganization org = (HFOrganization)node;
                    for (int i = 0; i < org.Nodes.Count; i++)
                    {
                        HFReader reader = (HFReader)org.Nodes[i];
                        reader.forwardStrategies.Add(new ForwardStrategy(tags, action));
                    }
                }
            }
            else
            {
                base.ParseEventArgs(array);
            }

        }
    }
}

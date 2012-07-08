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

                //这里需要找到与src同在一个机构的节点，tag一致
                if (dst == BroadcastNode.Node)
                {
                    int orgId = ((Reader)src).OrgId;
                    Organization org = (Organization)Node.getNode(orgId, NodeType.ORG);
                    int n = (int)Utility.U_Rand(org.nodes.Count);
                    dst = org.nodes[n];
                }


                if (array.Length>6 && array[6] == "tag")
                {
                    string t0 = array[7];
                    string[] t1 = t0.Split("_".ToCharArray());
                    if (t1.Length ==0 ||
                        (t1.Length == 1 && t1[0] == "-1"))
                    {
                        SWReader r = (SWReader)src;
                        TagEntity t = r.CalculateTagEntity(r.forwardStrategies);
                        tags = t.allowTags;
                        for (int i = 0; i < global.tagNameNum; i++)
                        {
                            if(Utility.U_Rand(1)>0.5)
                                tags = tags & (uint)~(1 << i);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < t1.Length; i++)
                        {
                            uint t = (uint)(1 << (int.Parse(t1[i])));
                            tags |= t;
                        }
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
                        //pkg.Tags = (uint)(t * 100);//这是干什么的？忘了                        
                        AddEvent(new Event(t, EventType.SND_DATA, src, pkg));
                    }
                }
            }
            else if (array[0] == "SET_STG")
            {
                Node node = Node.getNode(array[1]);
                
                ForwardStrategyAction action = (ForwardStrategyAction)Enum.Parse(typeof(ForwardStrategyAction), array[2]);
                string[] t0 = array[4].Split("_".ToCharArray());
                uint tags = 0;
                for (int i = 0; i < t0.Length; i++)
                {
                    uint tag = (uint)(1 << int.Parse(t0[i]));
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
                    for (int i = 0; i < org.nodes.Count; i++)
                    {
                        HFReader reader = (HFReader)org.nodes[i];
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;
using System.IO;
using System.Diagnostics;

namespace HeterogeneousForward
{
    class HFOrganization:Organization
    {
        new public static HFOrganization ProduceOrganization(int id, string name)
        {
            return new HFOrganization(id, name);
        }

        public HFOrganization(int id, string name)
            : base(id, name)
        {
        }


        new public static void GenerateNodePositionsAllRandom()
        {
            Global global = Global.getInstance();

            //先放置于无穷远
            for (int i = 0; i < global.readerNum; i++)
            {
                global.readers[i].X = 10000;
                global.readers[i].Y = 10000;
            }

            //机构0, 1的候选节点
            for (int p = 0; p < 2; p++)
            {
                for (int i = 0; i < global.orgs[p].nodes.Count; i++)
                {
                    double x = 0, y = 0, mindist = 9999, retry = 0;
                    //double maxdist = 0;
                    do
                    {
                        mindist = 9999;
                        //maxdist = 0;
                        x = Utility.U_Rand(global.layoutX);
                        y = Utility.U_Rand(global.layoutY);
                        global.orgs[p].nodes[i].X = x;
                        global.orgs[p].nodes[i].Y = y;

                        for (int s = 0; s < p; s++)
                        {
                            int t = (s == p) ? i : global.orgs[s].nodes.Count;
                            for (int j = 0; j < t; j++)
                            {
                                double d = Utility.Distance(global.orgs[s].nodes[j], global.orgs[p].nodes[i]);
                                if (d < mindist)
                                    mindist = d;
                                //if (d > maxdist)
                                //    maxdist = d;
                            }
                        }
                        retry++;

                    } while (i > 0 && mindist < 200 && retry < 20);
                }
            }


            for (int i = 2; i < global.orgs.Length; i++)
            {
                for (int j = 0; j < global.orgs[i].nodes.Count; j++)
                {
                    double x = 0, y = 0;
                    x = Utility.U_Rand(global.layoutX);
                    y = Utility.U_Rand(global.layoutY);
                    global.orgs[i].nodes[j].X = x;
                    global.orgs[i].nodes[j].Y = y;
                }
            }

            /*
            //验证所有节点的连通性,每个节点的连通度至少为2
            for (int i = 0; i < global.readerNum; i++)
            {
                Reader r1 = global.readers[i];
                do
                {
                    int n = 0;
                    for (int j = 0; j < global.readerNum; j++)
                    {
                        Reader r2 = global.readers[j];
                        if (r1.Id == r2.Id)
                            continue;
                        double dist = Utility.Distance(r1, r2);
                        if (dist < global.nodeMaxDist)
                            n++;
                        if (dist < 20)//两个节点不能太近
                        {
                            n = 1;
                            break;
                        }
                    }
                    if (n >= 2)
                        break;
                    r1.X = Utility.U_Rand(global.layoutX);
                    r1.Y = Utility.U_Rand(global.layoutY);
                } while (true);
            }
             */ 
        }


    }
}

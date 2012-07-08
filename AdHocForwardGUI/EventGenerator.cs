using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace AdHocBaseApp
{
    public class EventGenerator
    {
        //send events. the cmd is SND_DATA or SND_CMD
        public virtual void GenerateSendEvents(bool append, bool reload, Node[] froms, Node[] tos, string cmd)
        {
            Global global = Global.getInstance();
            string line = null;
            StreamWriter sw = null;
            sw = new StreamWriter(global.eventsFileName, append);
            sw.WriteLine();

            double t = (global.endTime - global.startTime) / global.SendEventNum;
            double current = 0;
            for (int i = 0; i < global.SendEventNum; i++)
            {
                Node snode = froms[Utility.Rand(froms.Length)];
                Node dnode = snode;

                if (snode.type == NodeType.OBJECT && tos[0].type == NodeType.ORG)
                    dnode = global.orgs[((ObjectNode)snode).OrgId];
                else
                {
                    while (dnode == snode)
                        dnode = tos[Utility.Rand(tos.Length)];
                }

                double time = Utility.P_Rand(t);
                current += time;
                if (current >= global.endTime)//End
                    break;
                double end = Utility.U_Rand(current, global.endTime);
                double freq = 0.5;

                line = cmd + "\t" + snode + "\t" + dnode + "\t" + (int)current + "\t" + (int)end + "\t" + freq;
                sw.WriteLine(line);
            }


            if (sw != null)
                sw.Close();

            if (reload == true)
                Event.LoadEvents();
        }

        public void GenerateRandomMotionEvents(bool append, NodeType nodeType, bool reload, double speed, int count)
        {
            Global global = Global.getInstance();
            string line = null;
            string type;
            if (nodeType == NodeType.READER)
                type = "R";
            else
                type = "T";
            StreamWriter sw = null;
            try
            {
                sw = new StreamWriter(global.eventsFileName, append);
                sw.WriteLine();                

                for (int i = 0; i < count; i++)
                {
                    int node = 2;
                    int dx = 500;
                    int dy = 200;
                    double s = Utility.P_Rand(speed);
                    line = "MOV\t" + type + node + "\t" + dx + "\t" + dy + "\t" + s;
                    sw.WriteLine(line);
                }
            }

            catch (Exception ex)
            {
                Debug.Assert(false, ex.StackTrace);
                return;
            }
            finally
            {
                if (sw != null)
                    sw.Close();
            }

            if (reload == true)
                Event.LoadEvents();
        }


        //这个函数调用完了需要reload事件
        public void GenerateRandomObjectMotionEvents(bool append, double speed, int eventCount, int nodeCount, NodeType nodeType, string filename)
        {
            string type = "";
            if (nodeType == NodeType.READER)
            {
                type = "R";
            }
            else
            {
                type = "T";
            }

            HashSet<int> ignoreOrgs = new HashSet<int> { 0, 1 };
            Global global = Global.getInstance();
            string line = null;
            StreamWriter sw = null;
            try
            {
                sw = new StreamWriter(filename, append);
                sw.WriteLine();

                HashSet<int> nodes = new HashSet<int>();
                for (int t = 0; t < nodeCount; t++)
                {
                    int node = -1;
                    do{
                        node = (int)Utility.U_Rand(global.readerNum);
                    } while (nodes.Contains(node) || ignoreOrgs.Contains(global.readers[node].OrgId));
                    nodes.Add(node);
                    for (int i = 0; i < eventCount; i++)
                    {
                        //max speed 
                        double s = Utility.P_Rand(speed);
                        double x = Utility.U_Rand(global.layoutX);
                        double y = Utility.U_Rand(global.layoutY);

                        line = "MOV\t" + type + node + "\t" + x + "\t" + y + "\t" + s;
                        sw.WriteLine(line);
                    }
                }

            }

            catch (Exception ex)
            {
                Debug.Assert(false, ex.StackTrace);
                return;
            }
            finally
            {
                if (sw != null)
                    sw.Close();
            }
        }

        public void ClearEvents(string filename, string cmd)
        {
            Global global = Global.getInstance();
            string tempfile = filename + ".tmp";
            string line = null;

            StreamReader sr = null;
            sr = new StreamReader(filename);

            StreamWriter sw = null;
            sw = new StreamWriter(tempfile);

            while ((line = sr.ReadLine()) != null)
            {
                if (line.StartsWith(cmd))
                    continue;
                sw.WriteLine(line);
            }
            if (sr != null)
                sr.Close();
            if (sw != null)
                sw.Close();
            File.Delete(filename);
            File.Move(tempfile, filename);
        }
    }


}

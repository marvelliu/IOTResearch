using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace AdHocBaseApp
{
    public class EventManager
    {

        public void AddEvent(Event e)
        {
            Global global = Global.getInstance();
            global.events.Add(e);
            global.events.Sort();
        }

        public void ClearEvents()
        {
            Global global = Global.getInstance();
            global.events.Clear();
        }

        public void LoadEvents()
        {
            LoadEvents(false);
        }

        public void LoadEvents(bool clear)
        {
            Global global = Global.getInstance();

            if (clear)
                ClearEvents();


            //Load send beacon events.
            Scheduler scheduler = Scheduler.getInstance();
            foreach (Reader reader in global.readers)
            {
                if (reader.SendBeaconFlag)
                {
                    float nextBeacon = 0;
                    if (reader.IsGateway)
                        nextBeacon = 0;
                    else
                        nextBeacon = (float)(Utility.P_Rand(10 * (global.beaconWarmingInterval)) / 10);//0.5是为了设定最小值
                    //float nextBeacon = (float)(Utility.U_Rand(10 * global.beaconInterval) / 10);
                    AddEvent(new Event(scheduler.currentTime + nextBeacon, EventType.SND_BCN, reader, null));
                }
            }

            string line = null;
            StreamReader sr = null;
            string[] seperators = { "\t", " ", ":" };
            Console.WriteLine("Events start");
            sr = new StreamReader(global.eventsFileName);
            Console.WriteLine("Parsing " + global.eventsFileName);
            for (line = sr.ReadLine(); line != null; line = sr.ReadLine())
            {
                if (line == "" || line[0] == '#')
                    continue;
                string[] array = line.Split(new string[] { " ", "\t" }, StringSplitOptions.RemoveEmptyEntries);

                ParseEventArgs(array);
                Console.WriteLine(line);
            }
            Console.WriteLine("Events end");
            if (sr != null)
                sr.Close();
        }

        public virtual void ParseEventArgs(string[] array)
        {
            Global global = Global.getInstance();
            if (array[0] == "SND_DATA" || array[0] == "SND_CMD")
            {
                Node src = Node.getNode(array[1]);
                Node dst = Node.getNode(array[2]);
                float start = float.Parse(array[3]);
                int offset = 4;
                float endtime = -1;
                float interval = -1;

                endtime = float.Parse(array[offset++]);
                interval = float.Parse(array[offset++]);

                PacketType type = (array[0] == "SND_DATA") ? PacketType.DATA : PacketType.COMMAND;

                if (endtime < 0)
                {
                    Packet pkg = new Packet(src, dst, type, start);
                    pkg.TTL = global.TTL;
                    AddEvent(new Event(start, EventType.SND_DATA, src, pkg));
                }
                else
                {
                    for (float t = start; t < endtime; t += interval)
                    {
                        Packet pkg = new Packet(src, dst, type, start);
                        pkg.TTL = global.TTL;
                        AddEvent(new Event(t, EventType.SND_DATA, src, pkg));
                    }
                }
            }
            else if (array[0] == "MOV")
            {
                MobileNode node;
                node = (MobileNode)Node.getNode(array[1]);
                double dx = double.Parse(array[2]);
                double dy = double.Parse(array[3]);
                double speed = double.Parse(array[4]);

                if (node.DstX == null)
                {
                    node.DstX = new List<double>();
                    node.DstY = new List<double>();
                    node.Speed = new List<double>();
                }
                node.DstX.Add(dx);
                node.DstY.Add(dy);
                node.Speed.Add(speed);
            }
            else if (array[0] == "SET")
            {
                MobileNode node;
                node = (MobileNode)Node.getNode(array[1]);

                double x = 0, y = 0;
                if (array[2] == "RANDOM")
                {
                    x = Utility.U_Rand(global.layoutX);
                    y = Utility.U_Rand(global.layoutY);
                }
                else
                {
                    x = double.Parse(array[2]);
                    y = double.Parse(array[3]);
                }

                node.X = x;
                node.Y = y;
            }
            else if (array[0] == "SET_GW")
            {
                int num = 0;
                if (array[1] == "RANDOM")
                {
                    do
                    {
                        num = (int)Utility.U_Rand(global.readerNum);
                    } while (global.readers[num].IsGateway);
                }
                else
                {
                    num = int.Parse(array[1]);
                }
                global.readers[num].SetAsGateway();
            }
            else if (array[0] == "SET_LM")
            {
                int num = int.Parse(array[1]);
                global.readers[num].LandmarkReader = true;
            }
            else if (array[0] == "SET_ONLY_LM")
            {
                int num = int.Parse(array[1]);
                global.readers[num].LandmarkReader = true;
            }
            else if (array[0] == "QRY")
            {
                int q = int.Parse(array[1]);
                int o = int.Parse(array[2]);
                float t = float.Parse(array[3]);
                AddEvent(new Event(t, EventType.QRY_LOCATION, global.queriers[q], o));
            }
            else
                throw new Exception("Unknown event type: " + array[0]);
        }
    }
}

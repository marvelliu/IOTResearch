using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace AdHocBaseApp
{
    public delegate Scheduler SchedulerlConstructor();
    public class SchedulerProducer
    {
        static public SchedulerlConstructor schedulerlConstructor;
    }

    public class Scheduler
    {
        protected static Scheduler instance = null;
        protected Global global;

        public static Scheduler ProduceScheduler()
        {
            return new Scheduler();
        }

        protected Scheduler()
        {
            global = Global.getInstance();
        }

        public static Scheduler getInstance()
        {
            if (instance == null)
                instance = SchedulerProducer.schedulerlConstructor();
            return instance;
        }

        public float step;

        float startTime;
        float endTime;
        Thread thread;
        public float currentTime;
        
        Scheduler(float step, float start, float end)
        {
            this.step = step;
            this.startTime = start;
            this.endTime = end;
        }

        //在模拟器结束的时候处理的事件
        public virtual void EndProcess()
        {
            Console.WriteLine("Simulation ends");
        }

        public void Start()
        {
            Console.WriteLine("Simulation start");
            thread = new Thread(new ThreadStart(StartThread));
            thread.Start();
        }

        public void Stop()
        {
            if (thread != null && thread.IsAlive)
            {
                EndProcess();
                thread.Abort();
            }
        }

        public bool Started()
        {
            if (thread == null)
                return false;
            return thread.IsAlive;
        }

        private void StartThread(){
            startTime = global.startTime;
            endTime = global.endTime;
            step = global.step;

            currentTime = startTime;

            while (currentTime < endTime)
            {
                //Console.WriteLine("Current time: "+currentTime);
                NextStep();
                currentTime += step;
                global.mainForm.Invalidate();
            }

            Console.WriteLine("{0} [TOTAL_PKT] {1}", currentTime ,global.PacketSeq);

            quitDele q = global.mainForm.Quit;
            if (global.automatic)
            {
                Thread.Sleep(500);
                global.mainForm.Invoke(q);
            }
        }

        public delegate void quitDele();

        public virtual void ProcessEvent(Node node, Event e)
        {
            switch (e.Type)
            {
                case EventType.RECV:
                    node.Recv((Packet)e.Obj);
                    break;
                case EventType.SND_BCN:
                    ((Reader)node).SendBeacon(currentTime);
                    break;
                case EventType.SND_DATA:
                    Packet pkg = (Packet)e.Obj;
                    if (pkg.SrcSenderSeq < 0 && node.type == NodeType.READER)//未定该数据包的id
                        ((Reader)node).initPacketSeq(pkg);
                    node.SendData(pkg);
                    break;
                case EventType.CHK_NB:
                    ((Reader)node).CheckNeighbors();
                    break;
                case EventType.CHK_NEAR_OBJ:
                    ((Reader)node).CheckNearObjects();
                    break;
                case EventType.CHK_PEND_PKT:
                    ((Reader)node).CheckPendingPackets();
                    break;
                case EventType.QRY_LOCATION:
                    ((Querier)node).SendQueryRequest((int)e.Obj);
                    break;
                default:
                    throw new Exception("Unknown event type: " + e.Type);
            }
        }

        public void NextStep()
        {
            MoveMobileNodes();
            float time = currentTime;
            while (global.events.Count > 0 && global.events[0].Time < time + step)
            {
                Event e = global.events[0];
                Node node = e.Node;
                currentTime = e.Time;
                ProcessEvent(node, e);
                int x = global.events.Count;
                global.events.Remove(e);
                if (x == global.events.Count)
                    Console.WriteLine("---------------------error................");
            }
            currentTime = time;
        }

        public void MoveMobileNodes()
        {
            foreach (Reader reader in global.readers)
            {
                if (reader.DstX == null || reader.Speed[0]<global.delta)//Not moving
                    continue;

                double t1 = 0.0f, t2= 0.0f;
                while (true)
                {
                    if (reader.DstX.Count == 0)//Arrived
                    {
                        reader.DstX = null;
                        break;
                    }

                    double dX = reader.DstX[0];
                    double dY = reader.DstY[0];
                    double speed = reader.Speed[0];
                    t2 = Utility.Distance(reader.X, reader.Y, dX, dY) / speed;
                    if (t1+t2 < step)//Still have time
                    {
                        reader.X = dX;
                        reader.Y = dY;
                        reader.DstX.RemoveAt(0);
                        reader.DstY.RemoveAt(0);
                        reader.Speed.RemoveAt(0);
                        t1 += t2;
                    }
                    else
                    {
                        reader.X += (step - t1) * (dX - reader.X) / t2;
                        reader.Y += (step - t1) * (dY - reader.Y) / t2;
                        break;
                    }
                    
                }
                //省略
                //Console.WriteLine(CurrentTime + " [MOVE] " + reader.type + " " + reader.Id + " move to (" + reader.X + ", " + reader.Y + ")");
            }

            foreach (ObjectNode obj in global.objects)
            {
                if (obj.DstX == null || obj.Speed[0] < global.delta)//Not moving
                    continue;

                double t1 = 0.0f, t2 = 0.0f;
                while (true)
                {
                    if (obj.DstX.Count == 0)//Arrived
                    {
                        obj.DstX = null;
                        break;
                    }

                    double dX = obj.DstX[0];
                    double dY = obj.DstY[0];
                    double speed = obj.Speed[0];
                    t2 = Utility.Distance(obj.X, obj.Y, dX, dY) / speed;
                    if (t1 + t2 < step)//Still have time
                    {
                        obj.X = dX;
                        obj.Y = dY;
                        obj.DstX.RemoveAt(0);
                        obj.DstY.RemoveAt(0);
                        obj.Speed.RemoveAt(0);
                        t1 += t2;
                    }
                    else
                    {
                        obj.X += (step - t1) * (dX - obj.X) / t2;
                        obj.Y += (step - t1) * (dY - obj.Y) / t2;
                        break;
                    }

                }
                Console.WriteLine("{0:F4} [MOVE] {1}{2} move to ({3},{4})", currentTime, obj.type, obj.Id, obj.X, obj.Y);
            }

            foreach (Reader reader in global.readers)
            {                
                reader.NotifyObjects();
            }
        }


        public PointShape NextReaderPosition(Reader reader, float time)
        {
            double x = reader.X, y =reader.Y;
            if (reader.DstX == null || reader.Speed[0] < global.delta)//Not moving
                return new PointShape(reader.X, reader.Y);

            double t1 = 0.0f, t2 = 0.0f;

            for(int i=0;i<reader.DstX.Count;i++)
            {
                if (reader.DstX.Count == 0)//Arrived
                {
                    break;
                }

                double dX = reader.DstX[i];
                double dY = reader.DstY[i];
                double speed = reader.Speed[i];
                t2 = Utility.Distance(reader.X, reader.Y, dX, dY) / speed;
                if (t1 + t2 < time)//Still have time
                {
                    x = dX;
                    y = dY;
                    t1 += t2;
                }
                else
                {
                    x += (time - t1) * (dX - reader.X) / t2;
                    y += (time - t1) * (dY - reader.Y) / t2;
                    break;
                }

            }
            return new PointShape(x, y);
        }


    }
}

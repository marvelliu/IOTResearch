﻿
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace AdHocBaseApp
{
    public enum EventType
    {
        SND_BCN,
        RCV_BCN,
        REQ_FWD,
        RCV_FWD,
        RECV,
        SND_DATA,
        CHK_NB,
        CHK_NEAR_OBJ,
        CHK_PEND_PKT,
        CHK_REV_PATH_CACHE,
        QRY_LOCATION,
        QRY_LINKPATH_SRV,
        QRY_LINKPATH_ADV,
        CHK_CERT,
        CHK_RT_TIMEOUT,
        CHK_RECV_PKT,
        CHK_EVENT_TIMEOUT,
        K_ANONY,
        CHK_SUBTREE,
        CHK_NEWGROUP,
        CHK_NATGROUP,
        CHK_NATGROUP1,
        CHK_SW_NB,
        DEDUCE_EVENT,
        FWD_EVENT_REPORT,
    }

    public class Event : IComparable
    {
        public float Time;
        public EventType Type;
        public Node Node;
        public object Obj;

        public Event(float time, EventType type, Node node, object obj)
        {
            this.Time = time;
            this.Type = type;
            this.Node = node;
            this.Obj = obj;
        }

        public int CompareTo(object obj)
        {
            if (!(obj is Event))
                throw new ArgumentException("Argument not a evnet", "right");
            Event e = (Event)obj;
            if (this.Time > e.Time)
                return 1;
            else if (this.Time < e.Time)
                return -1;
            else
                return 0;
        }

        static public void AddEvent(Event e)
        {
            EventManager manager = new EventManager();
            manager.AddEvent(e);
        }

        static public void LoadEvents()
        {
            EventManager manager = new EventManager();
            manager.LoadEvents();
        }

        static public void LoadEvents(bool clear)
        {
            EventManager manager = new EventManager();
            manager.LoadEvents(clear);
        }


    }

    
}

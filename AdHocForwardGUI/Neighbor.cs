﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AdHocBaseApp
{
    public class Neighbor
    {
        public Reader node;
        public float lastBeacon;
        public float firstBeacon;

        public Neighbor(Reader node)
        {
            this.node = node;
            lastBeacon = -1;
            firstBeacon = -1;
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;
using System.IO;
using System.Diagnostics;

namespace MaliciousOrganizationDetection
{
    class MODOrganization:Organization
    {
        public ReaderType orgType = ReaderType.NORMAL;
        new public static MODOrganization ProduceOrganization(int id, string name)
        {
            return new MODOrganization(id, name);
        }

        public MODOrganization(int id, string name)
            : base(id, name)
        {
        }

        public bool IsMalicious = false;
    }
}

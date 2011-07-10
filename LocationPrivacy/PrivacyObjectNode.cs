using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;
using System.Diagnostics;

namespace LocationPrivacy
{
    public class PrivacyObjectNode:ObjectNode
    {
        new public static PrivacyObjectNode ProduceObjectNode(int id)
        {
            return new PrivacyObjectNode(id);
        }


        protected PrivacyObjectNode(int id)
            : base(id)
        {
        }

    }
}

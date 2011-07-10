using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace LocationPrivacy
{
    class PrivacyOrganization:Organization
    {
        new public static PrivacyOrganization ProduceOrganization(int id, string name)
        {
            return new PrivacyOrganization(id, name);
        }

        public PrivacyOrganization(int id, string name)
            : base(id, name)
        {
        }
    }
}

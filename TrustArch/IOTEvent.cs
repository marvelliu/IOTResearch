using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrustArch
{
    enum IOTEventType
    {
        //Intermediate node forward data
        DropPacketMaliciously,
        DropPacketDueToBandwith,
        DropPacketDueToMove,
        ModifyPacketMaliciously,
        RedirectPacketMaliciously,
        NormalForwardPacket,
        //Intermediate node topology information
        BadTopologyMaliciously,
        BadTopologyDueToMove,
        BadTopologyDueToNetwork,
        NormalTopology,
        //Terminal node forward command
        ModifyCommandMaliciously,
        DropCommandMaliciously,
        ForgeCommandMaliciously,
        NormalForwardCommand,
        //Terminal node forward data
        RedirectTagDataMaliciously,
        ModifyTagDataMaliciously,
        RedirectTagDataMaliciously,
        NormalForwardTagData,

        //Misc event
        BadDeclaredRegionInfo,
        CorrectDeclaredRegionInfo,
        COUNT
    }

    class IOTEventResult
    {
        IOTEventType type;
        double m;
        double b;
        double p;
    }

    class IOTEvent
    {
        static void IOTDeduceAll(int node, List<IOTPhenomemon> observedPhenomemons)
        {
            int n = (int)IOTEventType.COUNT;
            IOTEventResult[] results = new IOTEventResult[n];
            for(int i=0;i<n;i++)
            {
            }
        }

        static double ConditionHappened(IOTPhenomemonType type, int node, List<IOTPhenomemon> observedPhenomemons)
        {
            for(int i=0;i<observedPhenomemons.Count;i++)
            {
                if(observedPhenomemons[i].node == node && observedPhenomemons[i].type == type)
                    return observedPhenomemons[i].likehood;
            }
            return 0.001;
        }


        static double AND(IOTPhenomemonType t1, IOTPhenomemonType t2, int node, List<IOTPhenomemon> observedPhenomemons)
        {
            double a = ConditionHappened(t1, node, observedPhenomemons);
            double b = ConditionHappened(t2, node, observedPhenomemons);
            //return the smaller one
            return (a > b) ? b : a;
        }
        
        static double OR(IOTPhenomemonType t1, IOTPhenomemonType t2, int node, List<IOTPhenomemon> observedPhenomemons)
        {
            double a = ConditionHappened(t1, node, observedPhenomemons);
            double b = ConditionHappened(t2, node, observedPhenomemons);
            //return the smaller one
            return (a > b) ? a : b;
        }

    }
}

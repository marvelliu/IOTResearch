using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace HeterogeneousForward
{
    public enum ForwardStrategyAction
    {
        ACCEPT,
        REFUSE
    }

    [Serializable]
    public class ForwardStrategy
    {
        public ulong Tags;
        public ForwardStrategyAction Action;

        public ForwardStrategy()
        {
            unchecked
            {
                this.Tags = (ulong)-1; //default
                this.Action = ((HFGlobal)Global.getInstance()).defaultForwardStrategyAction ;
            }
        }

        public ForwardStrategy(ulong tags, ForwardStrategyAction action)
        {
            this.Tags = tags;
            this.Action = action;
        }
    }
}

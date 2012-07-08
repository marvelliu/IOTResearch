using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace VANETs
{
    public enum CertificateMethod
    {
        LOCAL,
        REMOTE_AUTH,
        REMOTE_RETR
    }
    public class CertificateArg
    {
        public Certificate cert;
        /* method
         * 0 local
         * 1 remote auth
         * 2 remote retrieve(no auth)
         * */
        public CertificateMethod method;
        public CertificateArg(Certificate cert, CertificateMethod method)
        {
            this.cert = cert;
            this.method = method;
        }

    }
    class VANETScheduler: Scheduler
    {

        new public static Scheduler ProduceScheduler()
        {
            return new VANETScheduler();
        }


        public override void ProcessEvent(Node node, Event e)
        {
            switch (e.Type)
            {
                case EventType.CHK_CERT:
                    ((VANETReader)node).CheckCertificate((CertificateArg)e.Obj);
                    break;
                default:
                    base.ProcessEvent(node, e);
                    break;
            }
        }

        public override void EndProcess()
        {
            VANETReader.ComputeNetworkDetail();
            base.EndProcess();
        }
    }
}

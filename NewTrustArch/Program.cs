using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using AdHocBaseApp;
using System.Diagnostics;
using TrustArch;

namespace NewTrustArch
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            GlobalProducer.globalConstructor = IOTGlobal.ProduceGlobal;
            SchedulerProducer.schedulerlConstructor = IOTScheduler.ProduceScheduler;
            IOTGlobal global = (IOTGlobal)Global.getInstance();

            global.objectNodeConstructor = IOTObjectNode.ProduceObjectNode;
            global.readerConstructor = IOTReader.ProduceReader;
            global.organizationConstructor = IOTOrganization.ProduceOrganization;
            global.trustManagerConstructor = IOTTrustManager.getInstance;


            ParseArgs(args);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }


        static void ParseArgs(string[] args)
        {
            IOTGlobal global = (IOTGlobal)Global.getInstance();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-c")
                {
                    global.configFileName = args[i + 1];
                    break;
                }
            }

            global.LoadConfigFile();

            for (int i = 0; i < args.Length; i++)
            {
                string[] v = null;

                if (args[i] == "--auto")
                {
                    global.automatic = true;
                }
                else if (args[i].StartsWith("--"))
                {
                    v = new string[2];
                    v[0] = args[i].Substring(2);
                    v[1] = args[i + 1];
                    i++;
                    global.ParseArgs(v);
                }
                else if (args[i].StartsWith("-"))
                {
                    v = new string[2];
                    v[0] = args[i].Substring(1);
                    v[1] = args[i + 1];
                    i++;
                    global.ParseArgs(v);
                }
                else
                {
                    Debug.Assert(false, "Format error!");
                }
            }
            global.AssertConfig();
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using AdHocBaseApp;
using System.Diagnostics;

namespace LogicalPath
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Global global = Global.getInstance();

            global.objectNodeConstructor = LogicalPathObjectNode.ProduceObjectNode;
            global.readerConstructor = LogicalPathReader.ProduceReader;
            global.querierConstructor = LogicalPathQuerier.ProduceQuerier;
            global.serverConstructor = LogicalPathServer.getInstance;

            ParseArgs(args);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        static void ParseArgs(string[] args)
        {
            Global global = Global.getInstance();

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

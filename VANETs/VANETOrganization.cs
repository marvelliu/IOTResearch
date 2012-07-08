using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;
using System.IO;
using System.Diagnostics;

namespace VANETs
{

    class VANETOrganization:Organization
    {
        public VANETOrganization(int id, string name)
            : base(id, name)
        {
        }

        new public static void GenerateNodes()
        {
            Global global = Global.getInstance();
            VANETServer server = VANETServer.getInstance();
            for (int i = 0; i < global.readerNum; i++)
            {
                Reader reader = global.readerConstructor(i, -1);
                global.readers[i] = reader;
                if (Utility.U_Rand(1) < global.wiredProportion)
                {
                    ((VANETReader)reader).IsWired = true;
                    Console.WriteLine("Reader {0} is wired.", reader.Id);

                    /*
                    reader.IsGateway = true;
                    reader.gatewayEntities.Add(-1, new GatewayEntity(i, i, 0));

                    server.BackboneNodeDB.Add((VANETReader)reader);
                    server.BackboneNodeMapping[reader.Id] = server.BackboneNodeDB.Count;
                    Console.WriteLine("Reader {0} is set as gateway.", reader.Id);
                     * */


                }
            }
        }

        public static bool GenerateNetworks()
        {
            Global global = Global.getInstance();
            string filename = "networks.txt";
            string line = null;
            StreamReader sr = null;
            string[] seperators = { "\t", " ", ":" };

            Console.WriteLine("Parse network start");
            sr = new StreamReader(filename);
            for (line = sr.ReadLine(); line != null; line = sr.ReadLine())
            {
                if (line[0] == '#')
                    continue;
                string[] v = line.Split(seperators, StringSplitOptions.RemoveEmptyEntries);
                ParseArgs(v);
            }
            Console.WriteLine("Parse network end");

            if (sr != null)
                sr.Close();
            return true;
        }

        public static void ParseArgs(string[] v)
        {
            Global global = Global.getInstance();
            VANETServer server = VANETServer.getInstance();
            if (v[0] == "WIRED_BACKBONE")
            {
                int r = int.Parse(v[1]);
                if (r >= global.readers.Length)
                    return;
                VANETReader reader = (VANETReader)global.readers[r];
                reader.IsGateway = true;
                reader.IsWired = true;
                reader.gatewayEntities[-1] = new GatewayEntity(reader.Id, reader.Id, 0);
                reader.IssuedCertificate = Certificate.RootCA;

                server.BackboneNodeDB.Add(reader);
                server.BackboneNodeMapping[reader.Id] = server.BackboneNodeDB.Count;
                Console.WriteLine("Reader {0} is set as gateway.", reader.Id);

                global.readers[reader.Id].gatewayEntities[-1] = new GatewayEntity(reader.Id, reader.Id, 0);
            }
            Console.WriteLine(v[0] + ":" + v[1]);
        }

    }
}

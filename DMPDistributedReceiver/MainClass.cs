using System;

namespace DMPDistributedReceiver
{
    public class MainClass
    {
        public const string SETTINGS_FILE = "ReceiverSettings.xml";

        public static void Main()
        {
            MainClass mainClass = new MainClass();
            mainClass.Start();
        }

        public void Start()
        {
            ReceiverSettings receiverSettings = new ReceiverSettings();
            receiverSettings.LoadFromFile(SETTINGS_FILE);
            DBBackendServer dbServer = new DBBackendServer(receiverSettings);
            dbServer.Start();
            RelayServer relayServer = new RelayServer(receiverSettings, dbServer);
            relayServer.Start();
            ReporterServer reporterServer = new ReporterServer(receiverSettings, relayServer);
            reporterServer.Start();
            DetectConsole(relayServer, dbServer);
        }

        private void DetectConsole(RelayServer relayServer, DBBackendServer dbServer)
        {
            bool consoleOK = Console.In != null;
            if (consoleOK)
            {
                InteractiveConsole(relayServer, dbServer);
            }
            else
            {
                NoConsole();
            }
        }

        private void InteractiveConsole(RelayServer relayServer, DBBackendServer dbServer)
        {
            bool running = true;
            while (running)
            {
                string line = Console.ReadLine().ToLower();
                bool handled = false;
                if (line == "q")
                {
                    handled = true;
                    running = false;
                }
                if (line == "p")
                {
                    handled = true;
                    relayServer.PrintTree();
                }
                if (line == "d")
                {
                    handled = true;
                    dbServer.PrintClients();
                }
                if (!handled)
                {
                    Console.WriteLine("Commands: q for quit, p for print server/relay tree, d for connected DB clients");
                }
            }
        }

        private void NoConsole()
        {
            while (true)
            {
                System.Threading.Thread.Sleep(1000);
            }
        }
    }
}


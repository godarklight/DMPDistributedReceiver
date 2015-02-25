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
            bool running = true;
            while (running)
            {
                ConsoleKeyInfo key = Console.ReadKey();
                if (key.Key == ConsoleKey.Escape || key.Key == ConsoleKey.Q)
                {
                    running = false;
                }
                if (key.Key == ConsoleKey.P)
                {
                    relayServer.PrintTree();
                }
            }
        }
    }
}


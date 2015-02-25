using System;
using System.Net;

namespace DMPDistributedReceiver
{
    public class ReporterClient
    {
        public int clientID;
        public string remoteIP;
        public int remotePort;
        public ReportingMessage lastMessage;
    }
}


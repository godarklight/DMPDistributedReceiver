using System;
using System.Collections.Generic;
using System.Net;

namespace DMPDistributedReceiver
{
    public class RelayClient
    {
        public string relayHash;
        public string remoteIP;
        public int remotePort;
        public Dictionary<int, ReporterClient> remoteClients = new Dictionary<int, ReporterClient>();
        public Dictionary<string, RelayClient> remoteRelays = new Dictionary<string, RelayClient>();
    }
}


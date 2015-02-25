using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using DarkNetwork;
using MessageStream2;

namespace DMPDistributedReceiver
{
    public class DBBackendServer
    {
        private Dictionary<int, ReporterClient> localClients;
        private Dictionary<string, RelayClient> remoteClients;
        private ReceiverSettings settings;
        private NetworkServer<DBClient> networkServer;
        private int freeID = 0;

        public DBBackendServer(ReceiverSettings settings)
        {
            this.settings = settings;
        }

        public void SetData(Dictionary<int, ReporterClient> localClients, Dictionary<string, RelayClient> remoteClients)
        {
            this.localClients = localClients;
            this.remoteClients = remoteClients;
        }

        public void Start()
        {
            if (networkServer == null)
            {
                NetworkHandler<DBClient> networkHandler = new NetworkHandler<DBClient>();
                networkHandler.SetConnectCallback(HandleConnectCallback);
                networkHandler.SetHeartbeatCallback(HandleSendHeartbeat, 5000, 20000);
                networkHandler.SetDisconnectCallback(HandleDisconnectCallback);
                networkHandler.SetMessageCallback((int)MessageType.HEARTBEAT, HandleHeartbeat);
                networkServer = new NetworkServer<DBClient>(networkHandler, false);
                networkServer.Start(new IPEndPoint(IPAddress.IPv6Any, settings.dbBackendPort));
            }
        }

        private void HandleConnectCallback(NetworkClient<DBClient> client, TcpClient tcpClient)
        {
            client.stateObject = new DBClient();
            client.stateObject.remoteAddress = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
            client.stateObject.clientID = Interlocked.Increment(ref freeID);
            SendCurrentState(client);
            Console.WriteLine("DATABASE: Connect from " +  client.stateObject.clientID + ", Endpoint: " + client.stateObject.remoteAddress + ", Total: " + (networkServer.ConnectCount + 1));
        }

        private void HandleDisconnectCallback(NetworkClient<DBClient> client, Exception exception)
        {
            Console.WriteLine("DATABASE: Disconnect from " + client.stateObject.clientID + ", Total: " + (networkServer.ConnectCount - 1));
        }


        private NetworkMessage HandleSendHeartbeat(NetworkClient<DBClient> client)
        {
            return new NetworkMessage((int)MessageType.HEARTBEAT, null);
        }

        private void HandleHeartbeat(NetworkClient<DBClient> client, byte[] data)
        {
            //Don't care
        }

        private void SendCurrentState(NetworkClient<DBClient> client)
        {
            lock (localClients)
            {
                foreach (ReporterClient localClient in localClients.Values)
                {
                    client.QueueNetworkMessage(new NetworkMessage((int)MessageType.CONNECT, GetConnectMessageBytes(settings.reporterHash, localClient)));
                    if (localClient.lastMessage != null)
                    {
                        client.QueueNetworkMessage(new NetworkMessage((int)MessageType.REPORT, GetReportMessageBytes(settings.reporterHash, localClient, localClient.lastMessage)));
                    }
                }
            }
            lock (remoteClients)
            {
                foreach (RelayClient relayClient in remoteClients.Values)
                {
                    SendCurrentChildren(client, relayClient);
                }
            }
        }

        private void SendCurrentChildren(NetworkClient<DBClient> client, RelayClient relayClient)
        {
            foreach (ReporterClient remoteClient in relayClient.remoteClients.Values)
            {
                client.QueueNetworkMessage(new NetworkMessage((int)MessageType.CONNECT, GetConnectMessageBytes(relayClient.relayHash, remoteClient)));
                if (remoteClient.lastMessage != null)
                {
                    client.QueueNetworkMessage(new NetworkMessage((int)MessageType.REPORT, GetReportMessageBytes(relayClient.relayHash, remoteClient, remoteClient.lastMessage)));
                }
            }
            foreach (RelayClient childClient in relayClient.remoteRelays.Values)
            {
                SendCurrentChildren(client, childClient);
            }
        }

        public void Connect(string serverID, ReporterClient client)
        {
            networkServer.QueueToAll(new NetworkMessage((int)MessageType.CONNECT, GetConnectMessageBytes(serverID, client)));
        }

        public void Report(string serverID, ReporterClient client, ReportingMessage reportMessage)
        {
            networkServer.QueueToAll(new NetworkMessage((int)MessageType.REPORT, GetReportMessageBytes(serverID, client, reportMessage)));
        }

        public void Disconnect(string serverID, ReporterClient client)
        {
            networkServer.QueueToAll(new NetworkMessage((int)MessageType.DISCONNECT, GetDisconnectMessageBytes(serverID, client)));
        }

        private byte[] GetConnectMessageBytes(string serverID, ReporterClient client)
        {
            byte[] retBytes;
            using (MessageWriter mw = new MessageWriter())
            {
                mw.Write<string>(serverID);
                mw.Write<int>(client.clientID);
                mw.Write<string>(client.remoteIP);
                mw.Write<int>(client.remotePort);
                retBytes = mw.GetMessageBytes();
            }
            return retBytes;
        }

        private byte[] GetReportMessageBytes(string serverID, ReporterClient client, ReportingMessage reportMessage)
        {
            byte[] retBytes;
            using (MessageWriter mw = new MessageWriter())
            {
                mw.Write<string>(serverID);
                mw.Write<int>(client.clientID);
                mw.Write<byte[]>(reportMessage.GetBytes());
                retBytes = mw.GetMessageBytes();
            }
            return retBytes;
        }

        private byte[] GetDisconnectMessageBytes(string serverID, ReporterClient client)
        {
            byte[] retBytes;
            using (MessageWriter mw = new MessageWriter())
            {
                mw.Write<string>(serverID);
                mw.Write<int>(client.clientID);
                retBytes = mw.GetMessageBytes();
            }
            return retBytes;
        }

        private enum MessageType
        {
            HEARTBEAT,
            CONNECT,
            REPORT,
            DISCONNECT,
        }
    }
}


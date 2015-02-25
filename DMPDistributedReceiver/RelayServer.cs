using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using DarkNetwork;
using MessageStream2;

namespace DMPDistributedReceiver
{
    public class RelayServer
    {
        private ReceiverSettings settings;
        private DBBackendServer dbServer;
        NetworkHandler<RelayClient> networkHandler;
        private NetworkClient<RelayClient> networkClient;
        private NetworkServer<RelayClient> networkServer;
        private Dictionary<int, ReporterClient> localClients = new Dictionary<int, ReporterClient>();
        private Dictionary<string, RelayClient> remoteRelays = new Dictionary<string, RelayClient>();
        private Thread networkClientThread;

        public RelayServer(ReceiverSettings settings, DBBackendServer dbServer)
        {
            this.settings = settings;
            dbServer.SetData(localClients, remoteRelays);
            this.dbServer = dbServer;
        }

        public void Start()
        {
            if (networkServer == null)
            {
                //NetworkClient<RelayClient>.messageThrowBehaviour = MessageThrowBehaviour.CRASH;
                networkHandler = new NetworkHandler<RelayClient>();
                networkHandler.SetConnectCallback(HandleConnectCallback);
                networkHandler.SetHeartbeatCallback(HandleSendHeartbeat, 5000, 20000);
                networkHandler.SetDisconnectCallback(HandleDisconnectCallback);
                networkHandler.SetMessageCallback((int)MessageType.HEARTBEAT, HandleHeartbeat);
                networkHandler.SetMessageCallback((int)MessageType.INFO, HandleInfo);
                networkHandler.SetMessageCallback((int)MessageType.ACCEPT, HandleAccept);
                networkHandler.SetMessageCallback((int)MessageType.REJECT, HandleReject);
                networkHandler.SetMessageCallback((int)MessageType.REMOTE_CONNECT, HandleRemoteConnect);
                networkHandler.SetMessageCallback((int)MessageType.REMOTE_REPORT, HandleRemoteReport);
                networkHandler.SetMessageCallback((int)MessageType.REMOTE_DISCONNECT, HandleRemoteDisconnect);
                networkHandler.SetMessageCallback((int)MessageType.SERVER_CONNECT, HandleServerConnect);
                networkHandler.SetMessageCallback((int)MessageType.SERVER_DISCONNECT, HandleServerDisconnect);
                networkServer = new NetworkServer<RelayClient>(networkHandler, false);
                networkServer.Start(new IPEndPoint(IPAddress.IPv6Any, settings.relayPort));
                networkClientThread = new Thread(new ThreadStart(SyncParentLoop));
                networkClientThread.IsBackground = true;
                networkClientThread.Start();
            }
        }

        private void SyncParentLoop()
        {
            if (settings.otherReporters.Count == 0)
            {
                return;
            }
            while (true)
            {
                TcpClient currentConnection = null;
                foreach (string connectString in settings.otherReporters)
                {
                    foreach (IPEndPoint endpoint in IPUtils.FindEndpoint(connectString))
                    {
                        Console.WriteLine("RELAY CHILD: Connecting to " + endpoint);
                        try
                        {
                            TcpClient newClient = new TcpClient(endpoint.AddressFamily);
                            IAsyncResult ar = newClient.BeginConnect(endpoint.Address, endpoint.Port, null, null);
                            if (ar.AsyncWaitHandle.WaitOne(5000))
                            {
                                if (newClient.Connected)
                                {
                                    newClient.EndConnect(ar);
                                    currentConnection = newClient;
                                    break;
                                }
                                else
                                {
                                    Console.WriteLine("RELAY CHILD: Connection failed to " + endpoint.Address + " port " + endpoint.Port);
                                    newClient.Close();
                                }
                            }
                            else
                            {
                                Console.WriteLine("RELAY CHILD: Connection failed to " + endpoint.Address + " port " + endpoint.Port);
                                newClient.Close();
                            }
                        }
                        catch
                        {
                            Console.WriteLine("RELAY CHILD: Connection failed to " + endpoint.Address + " port " + endpoint.Port);
                        }
                    }
                    if (currentConnection != null)
                    {
                        break;
                    }
                }
                if (currentConnection != null)
                {
                    Console.WriteLine("RELAY CHILD: Connected!");
                    networkClient = new NetworkClient<RelayClient>(networkHandler, currentConnection, false);
                    networkClient.TransferToServer(networkServer);
                    while (networkClient.Connected)
                    {
                        Thread.Sleep(1000);
                    }
                    Console.WriteLine("RELAY CHILD: Disconnected!");
                }
                else
                {
                    Console.WriteLine("RELAY CHILD: All addresses failed. Retrying in 60 sec...!");
                }
                Thread.Sleep(60000);
            }
        }

        private void HandleConnectCallback(NetworkClient<RelayClient> client, TcpClient tcpClient)
        {
            client.stateObject = new RelayClient();
            client.stateObject.remoteIP = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address.ToString();
            client.stateObject.remotePort = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Port;
            client.QueueNetworkMessage(new NetworkMessage((int)MessageType.INFO, GetServerInfoBytes()));
            Console.WriteLine("RELAY: Connect from endpoint " + client.stateObject.remoteIP + ":" + client.stateObject.remotePort + ", Total: " + (networkServer.ConnectCount + 1));
        }

        private void HandleDisconnectCallback(NetworkClient<RelayClient> client, Exception exception)
        {
            Console.WriteLine("RELAY: Disconnect from endpoint " + client.stateObject.remoteIP + ":" + client.stateObject.remotePort + ", Total: " + (networkServer.ConnectCount - 1));
            if (client.stateObject.relayHash != null)
            {
                lock (remoteRelays)
                {
                    RemoteServerDisconnect(null, client.stateObject);
                    networkServer.QueueToOthers(client, new NetworkMessage((int)MessageType.SERVER_DISCONNECT, GetRemoteServerDisconnectBytes(new string[] {settings.reporterHash}, client.stateObject)));
                }
            }
        }

        private bool ContainsNode(RelayClient searchNode, string relayHash)
        {
            bool found = false;
            lock (remoteRelays)
            {
                if (searchNode.relayHash == relayHash)
                {
                    found = true;
                }
                else
                {
                    foreach (RelayClient relayClient in searchNode.remoteRelays.Values)
                    {
                        if (ContainsNode(relayClient, relayHash))
                        {
                            found = true;
                            break;
                        }
                    }
                }
            }
            return found;
        }

        private NetworkMessage HandleSendHeartbeat(NetworkClient<RelayClient> client)
        {
            return new NetworkMessage((int)MessageType.HEARTBEAT, null);
        }

        private void HandleHeartbeat(NetworkClient<RelayClient> client, byte[] data)
        {
            //Don't care
        }

        private void HandleAccept(NetworkClient<RelayClient> client, byte[] data)
        {
            Console.WriteLine("RELAY: Accepted from endpoint " + client.stateObject.remoteIP + ":" + client.stateObject.remotePort);
        }

        private void HandleReject(NetworkClient<RelayClient> client, byte[] data)
        {
            Console.WriteLine("RELAY: Rejected from endpoint " + client.stateObject.remoteIP + ":" + client.stateObject.remotePort);
            client.Disconnect();
        }

        private void HandleInfo(NetworkClient<RelayClient> client, byte[] data)
        {
            using (MessageReader mr = new MessageReader(data))
            {
                string serverHash = mr.Read<string>();
                bool found = false;
                if (settings.reporterHash == serverHash)
                {
                    found = true;
                }
                else
                {
                    lock (remoteRelays)
                    {
                        foreach (KeyValuePair<string, RelayClient> kvp in remoteRelays)
                        {
                            if (kvp.Value.relayHash == serverHash)
                            {
                                found = true;
                                break;
                            }
                            if (ContainsNode(kvp.Value, serverHash))
                            {
                                found = true;
                                break;
                            }
                        }
                    }
                }
                if (!found)
                {
                    client.QueueNetworkMessage(new NetworkMessage((int)MessageType.ACCEPT, null));
                    lock (remoteRelays)
                    {
                        SendTreeToClient(client);
                        client.stateObject.relayHash = serverHash;
                        remoteRelays.Add(serverHash, client.stateObject);
                        networkServer.QueueToOthers(client, new NetworkMessage((int)MessageType.SERVER_CONNECT, GetRemoteServerConnectBytes(new string[]{ settings.reporterHash }, client.stateObject)));
                    }
                }
                else
                {
                    client.QueueNetworkMessage(new NetworkMessage((int)MessageType.REJECT, null));
                }
            }
        }

        private void HandleRemoteConnect(NetworkClient<RelayClient> client, byte[] data)
        {
            lock (remoteRelays)
            {
                using (MessageReader mr = new MessageReader(data))
                {
                    string[] parents = mr.Read<string[]>();
                    int serverID = mr.Read<int>();
                    string serverIP = mr.Read<string>();
                    int serverPort = mr.Read<int>();
                    RelayClient relayClient = GetRelayClient(parents);
                    ReporterClient reporterClient = new ReporterClient();
                    reporterClient.clientID = serverID;
                    reporterClient.remoteIP = serverIP;
                    reporterClient.remotePort = serverPort;
                    //Add to DB
                    Console.WriteLine("RELAY: Remote receiver connect from " + relayClient.relayHash + ", client: " + reporterClient.clientID);
                    relayClient.remoteClients.Add(reporterClient.clientID, reporterClient);
                    dbServer.Connect(relayClient.relayHash, reporterClient);
                    //Relay
                    string[] newParents = new string[parents.Length + 1];
                    newParents[0] = settings.reporterHash;
                    parents.CopyTo(newParents, 1);
                    networkServer.QueueToOthers(client, new NetworkMessage((int)MessageType.REMOTE_CONNECT, GetRemoteConnectBytes(newParents, reporterClient)));
                }
            }
        }

        private void HandleRemoteReport(NetworkClient<RelayClient> client, byte[] data)
        {
            lock (remoteRelays)
            {
                using (MessageReader mr = new MessageReader(data))
                {
                    string[] parents = mr.Read<string[]>();
                    int serverID = mr.Read<int>();
                    byte[] reportBytes = mr.Read<byte[]>();
                    RelayClient relayClient = GetRelayClient(parents);
                    ReporterClient reporterClient = relayClient.remoteClients[serverID];
                    ReportingMessage reportMessage = ReportingMessage.FromBytesBE(reportBytes);
                    reporterClient.lastMessage = reportMessage;
                    //Add to DB
                    Console.WriteLine("RELAY: Remote receiver report from " + relayClient.relayHash + ", client: " + reporterClient.clientID + ", players: " + reportMessage.players.Length);
                    dbServer.Report(relayClient.relayHash, reporterClient, reportMessage);
                    //Relay
                    string[] newParents = new string[parents.Length + 1];
                    newParents[0] = settings.reporterHash;
                    parents.CopyTo(newParents, 1);
                    networkServer.QueueToOthers(client, new NetworkMessage((int)MessageType.REMOTE_REPORT, GetRemoteReportBytes(newParents, reporterClient, reportMessage)));
                }
            }
        }

        private void HandleRemoteDisconnect(NetworkClient<RelayClient> client, byte[] data)
        {
            lock (remoteRelays)
            {
                using (MessageReader mr = new MessageReader(data))
                {
                    string[] parents = mr.Read<string[]>();
                    int serverID = mr.Read<int>();
                    RelayClient relayClient = GetRelayClient(parents);
                    ReporterClient reporterClient = relayClient.remoteClients[serverID];
                    RemoteDisconnect(relayClient, reporterClient);
                    //Relay
                    string[] newParents = new string[parents.Length + 1];
                    newParents[0] = settings.reporterHash;
                    parents.CopyTo(newParents, 1);
                    networkServer.QueueToOthers(client, new NetworkMessage((int)MessageType.REMOTE_DISCONNECT, GetRemoteDisconnectBytes(newParents, reporterClient)));
                }
            }
        }

        private void HandleServerConnect(NetworkClient<RelayClient> client, byte[] data)
        {
            lock (remoteRelays)
            {
                using (MessageReader mr = new MessageReader(data))
                {
                    string[] parents = mr.Read<string[]>();
                    string serverHash = mr.Read<string>();
                    string serverIP = mr.Read<string>();
                    int serverPort = mr.Read<int>();
                    RelayClient parentClient = GetRelayClient(parents);
                    RelayClient childClient = new RelayClient();
                    childClient.relayHash = serverHash;
                    childClient.remoteIP = serverIP;
                    childClient.remotePort = serverPort;
                    parentClient.remoteRelays.Add(childClient.relayHash, childClient);
                    Console.WriteLine("RELAY: Remote receiver connect from " + parentClient.relayHash + " to " + childClient.relayHash);
                    //Relay
                    string[] newParents = new string[parents.Length + 1];
                    newParents[0] = settings.reporterHash;
                    parents.CopyTo(newParents, 1);
                    networkServer.QueueToOthers(client, new NetworkMessage((int)MessageType.SERVER_CONNECT, GetRemoteServerConnectBytes(newParents, client.stateObject)));
                }
            }
        }

        private void HandleServerDisconnect(NetworkClient<RelayClient> client, byte[] data)
        {
            lock (remoteRelays)
            {
                using (MessageReader mr = new MessageReader(data))
                {
                    string[] parents = mr.Read<string[]>();
                    string serverHash = mr.Read<string>();
                    RelayClient parentClient = GetRelayClient(parents);
                    RemoteServerDisconnect(parentClient, parentClient.remoteRelays[serverHash]);
                    Console.WriteLine("RELAY: Remote receiver disconnect " + serverHash + " from " + parentClient.relayHash);
                    //Relay
                    string[] newParents = new string[parents.Length + 1];
                    newParents[0] = settings.reporterHash;
                    parents.CopyTo(newParents, 1);
                    networkServer.QueueToOthers(client, new NetworkMessage((int)MessageType.SERVER_DISCONNECT, GetRemoteServerDisconnectBytes(parents, client.stateObject)));
                }
            }
        }

        public void LocalConnect(ReporterClient client)
        {
            lock (localClients)
            {
                localClients.Add(client.clientID, client);
                networkServer.QueueToAll(new NetworkMessage((int)MessageType.REMOTE_CONNECT, GetRemoteConnectBytes(new string[] { settings.reporterHash }, client)));
                dbServer.Connect(settings.reporterHash, client);
            }
        }

        public void LocalReport(ReporterClient client, ReportingMessage reportMessage)
        {
            lock (localClients)
            {
                networkServer.QueueToAll(new NetworkMessage((int)MessageType.REMOTE_REPORT, GetRemoteReportBytes(new string[] { settings.reporterHash }, client, reportMessage)));
                dbServer.Report(settings.reporterHash, client, reportMessage);
            }
        }

        public void LocalDisconnect(ReporterClient client)
        {
            lock (localClients)
            {
                localClients.Remove(client.clientID);
                networkServer.QueueToAll(new NetworkMessage((int)MessageType.REMOTE_DISCONNECT, GetRemoteDisconnectBytes(new string[] { settings.reporterHash }, client)));
                dbServer.Disconnect(settings.reporterHash, client);
            }
        }

        private void RemoteDisconnect(RelayClient parentClient, ReporterClient client)
        {
            Console.WriteLine("RELAY: Remote receiver disconnect from " + parentClient.relayHash + ", client: " + client.clientID);
            parentClient.remoteClients.Remove(client.clientID);
            dbServer.Disconnect(parentClient.relayHash, client);
        }

        private void RemoteServerDisconnect(RelayClient parentClient, RelayClient disconnectClient)
        {
            lock (remoteRelays)
            {
                List<RelayClient> relaysToDisconnect = new List<RelayClient>(disconnectClient.remoteRelays.Values);
                foreach (RelayClient childServer in relaysToDisconnect)
                {
                    RemoteServerDisconnect(disconnectClient, childServer);
                }
                List<ReporterClient> serversToDisconnect = new List<ReporterClient>(disconnectClient.remoteClients.Values);
                foreach (ReporterClient childServer in serversToDisconnect)
                {
                    RemoteDisconnect(disconnectClient, childServer);
                }
                if (parentClient == null)
                {
                    Console.WriteLine("RELAY: Remote relay disconnect " + disconnectClient.relayHash + " from us");
                    remoteRelays.Remove(disconnectClient.relayHash);
                }
                else
                {
                    Console.WriteLine("RELAY: Remote relay disconnect " + disconnectClient.relayHash + " from " + parentClient.relayHash);
                    parentClient.remoteRelays.Remove(disconnectClient.relayHash);
                }
            }
        }

        private void SendTreeToClient(NetworkClient<RelayClient> client)
        {
            lock (localClients)
            {
                lock (remoteRelays)
                {
                    string[] rootParent = new string[] { settings.reporterHash };
                    foreach (ReporterClient reporterClient in localClients.Values)
                    {
                        client.QueueNetworkMessage(new NetworkMessage((int)MessageType.REMOTE_CONNECT, GetRemoteConnectBytes(rootParent, reporterClient)));
                        if (reporterClient.lastMessage != null)
                        {
                            client.QueueNetworkMessage(new NetworkMessage((int)MessageType.REMOTE_REPORT, GetRemoteReportBytes(rootParent, reporterClient, reporterClient.lastMessage)));
                        }
                    }
                    foreach (RelayClient childClient in remoteRelays.Values)
                    {
                        SendTreeRecursive(client, rootParent, childClient);
                    }
                }
            }
        }

        private void SendTreeRecursive(NetworkClient<RelayClient> client, string[] parents, RelayClient relayClient)
        {
            string[] ourPosition = new string[parents.Length + 1];
            parents.CopyTo(ourPosition, 0);
            ourPosition[parents.Length] = relayClient.relayHash;
            client.QueueNetworkMessage(new NetworkMessage((int)MessageType.SERVER_CONNECT, GetRemoteServerConnectBytes(parents, relayClient)));
            foreach (ReporterClient reporterClient in relayClient.remoteClients.Values)
            {
                client.QueueNetworkMessage(new NetworkMessage((int)MessageType.REMOTE_CONNECT, GetRemoteConnectBytes(ourPosition, reporterClient)));
                if (reporterClient.lastMessage != null)
                {
                    client.QueueNetworkMessage(new NetworkMessage((int)MessageType.REMOTE_REPORT, GetRemoteReportBytes(ourPosition, reporterClient, reporterClient.lastMessage)));
                }
            }
            foreach (RelayClient childClient in relayClient.remoteRelays.Values)
            {
                SendTreeRecursive(client, ourPosition, childClient);
            }
        }

        private RelayClient GetRelayClient(string[] treePath)
        {
            return GetRelayClientRecursive(remoteRelays[treePath[0]], treePath, 1);
        }

        private RelayClient GetRelayClientRecursive(RelayClient parentClient, string[] treePath, int index)
        {
            if (treePath.Length == index)
            {
                return parentClient;
            }
            return GetRelayClientRecursive(parentClient.remoteRelays[treePath[index]], treePath, index + 1);
        }

        private byte[] GetServerInfoBytes()
        {
            byte[] retVal;
            using (MessageWriter mw = new MessageWriter())
            {
                mw.Write<string>(settings.reporterHash);
                retVal = mw.GetMessageBytes();
            }
            return retVal;
        }

        private byte[] GetRemoteConnectBytes(string[] parents, ReporterClient reporterClient)
        {
            byte[] retVal;
            using (MessageWriter mw = new MessageWriter())
            {
                mw.Write<string[]>(parents);
                mw.Write<int>(reporterClient.clientID);
                mw.Write<string>(reporterClient.remoteIP);
                mw.Write<int>(reporterClient.remotePort);
                retVal = mw.GetMessageBytes();
            }
            return retVal;
        }

        private byte[] GetRemoteReportBytes(string[] parents, ReporterClient reporterClient, ReportingMessage reporterMessage)
        {
            byte[] retVal;
            using (MessageWriter mw = new MessageWriter())
            {
                mw.Write<string[]>(parents);
                mw.Write<int>(reporterClient.clientID);
                mw.Write<byte[]>(reporterMessage.GetBytes());
                retVal = mw.GetMessageBytes();
            }
            return retVal;
        }

        private byte[] GetRemoteDisconnectBytes(string[] parents, ReporterClient reporterClient)
        {
            byte[] retVal;
            using (MessageWriter mw = new MessageWriter())
            {
                mw.Write<string[]>(parents);
                mw.Write<int>(reporterClient.clientID);
                retVal = mw.GetMessageBytes();
            }
            return retVal;
        }

        private byte[] GetRemoteServerConnectBytes(string[] parents, RelayClient relayClient)
        {
            byte[] retVal;
            using (MessageWriter mw = new MessageWriter())
            {
                mw.Write<string[]>(parents);
                mw.Write<string>(relayClient.relayHash);
                mw.Write<string>(relayClient.remoteIP);
                mw.Write<int>(relayClient.remotePort);
                retVal = mw.GetMessageBytes();
            }
            return retVal;
        }

        private byte[] GetRemoteServerDisconnectBytes(string[] parents, RelayClient relayClient)
        {
            byte[] retVal;
            using (MessageWriter mw = new MessageWriter())
            {
                mw.Write<string[]>(parents);
                mw.Write<string>(relayClient.relayHash);
                retVal = mw.GetMessageBytes();
            }
            return retVal;
        }

        public void PrintTree()
        {
            Console.WriteLine();
            Console.WriteLine("@LOCAL " + settings.reporterHash);
            foreach (ReporterClient client in localClients.Values)
            {
                if (client.lastMessage == null)
                {
                    Console.WriteLine("  +" + client.clientID + " CONNECTING");
                }
                else
                {
                    Console.WriteLine("  +" + client.clientID + " " + client.lastMessage.gameAddress + ":" + client.lastMessage.gamePort);
                }
            }
            foreach (RelayClient client in remoteRelays.Values)
            {
                PrintRecursive(client, 1);
            }
        }

        private void PrintRecursive(RelayClient currentClient, int depth)
        {
            string prefix = "".PadLeft(depth * 2);
            Console.WriteLine(prefix + "@" + currentClient.relayHash);
            foreach (ReporterClient client in currentClient.remoteClients.Values)
            {
                if (client.lastMessage == null)
                {
                    Console.WriteLine(prefix + "  +" + client.clientID + " CONNECTING");
                }
                else
                {
                    Console.WriteLine(prefix + "  +" + client.clientID + " " + client.lastMessage.gameAddress + ":" + client.lastMessage.gamePort);
                }
            }
            foreach (RelayClient client in currentClient.remoteRelays.Values)
            {
                PrintRecursive(client, depth + 1);
            }
        }

        private enum MessageType
        {
            HEARTBEAT,
            INFO,
            ACCEPT,
            REJECT,
            REMOTE_CONNECT,
            REMOTE_REPORT,
            REMOTE_DISCONNECT,
            SERVER_CONNECT,
            SERVER_DISCONNECT,
        }
    }
}


using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using DarkNetwork;

namespace DMPDistributedReceiver
{
    public class ReporterServer
    {
        private NetworkHandler<ReporterClient> networkHandler;
        private NetworkServer<ReporterClient> networkServer;
        private ReceiverSettings settings;
        private RelayServer relayServer;
        private int freeID = 0;


        public ReporterServer(ReceiverSettings settings, RelayServer relayServer)
        {
            this.settings = settings;
            this.relayServer = relayServer;
        }

        public void Start()
        {
            if (networkServer == null)
            {
                networkHandler = new NetworkHandler<ReporterClient>();
                networkHandler.SetConnectCallback(HandleConnectCallback);
                networkHandler.SetHeartbeatCallback(HandleSendHeartbeat, 5000, 20000);
                networkHandler.SetDisconnectCallback(HandleDisconnectCallback);
                networkHandler.SetMessageCallback((int)MessageType.HEARTBEAT, HandleHeartbeat);
                networkHandler.SetMessageCallback((int)MessageType.REPORT_V1, HandleReportV1);
                networkHandler.SetMessageCallback((int)MessageType.REPORT_V2, HandleReportV2);
                //Little endian format :(
                networkServer = new NetworkServer<ReporterClient>(networkHandler, true);
                networkServer.Start(new IPEndPoint(IPAddress.IPv6Any, settings.reporterPort));
            }
        }

        private NetworkMessage HandleSendHeartbeat(NetworkClient<ReporterClient> client)
        {
            return new NetworkMessage((int)MessageType.HEARTBEAT, null);
        }

        private void HandleConnectCallback(NetworkClient<ReporterClient> client, TcpClient tcpClient)
        {
            client.stateObject = new ReporterClient();
            client.stateObject.remoteIP = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address.ToString();
            if (client.stateObject.remoteIP.StartsWith("::ffff:"))
            {
                client.stateObject.remoteIP = client.stateObject.remoteIP.Substring(7);
            }
            client.stateObject.remotePort = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Port;
            client.stateObject.clientID = Interlocked.Increment(ref freeID);
            Console.WriteLine("RECEIVER: Connect from " +  client.stateObject.clientID + ", Endpoint: " + client.stateObject.remoteIP + ":" + client.stateObject.remotePort + ", Total: " + (networkServer.ConnectCount + 1));
            relayServer.LocalConnect(client.stateObject);
        }

        private void HandleDisconnectCallback(NetworkClient<ReporterClient> client, Exception exception)
        {
            Console.WriteLine("RECEIVER: Disconnect from " + client.stateObject.clientID + ", Total: " + (networkServer.ConnectCount - 1));
            relayServer.LocalDisconnect(client.stateObject);
        }

        private void HandleHeartbeat(NetworkClient<ReporterClient> client, byte[] data)
        {
            //Don't care.
        }

        private void HandleReportV1(NetworkClient<ReporterClient> client, byte[] data)
        {
            ReportingMessage reportingMessage = new ReportingMessage();
            using (MessageStream.MessageReader mr = new MessageStream.MessageReader(data, false))
            {
                reportingMessage.serverHash = mr.Read<string>();
                reportingMessage.serverName = mr.Read<string>();
                reportingMessage.description = mr.Read<string>();
                reportingMessage.gamePort = mr.Read<int>();
                reportingMessage.gameAddress = IPUtils.GetSafeGameAddress(mr.Read<string>(), client.stateObject);
                reportingMessage.protocolVersion = mr.Read<int>();
                reportingMessage.programVersion = mr.Read<string>();
                reportingMessage.maxPlayers = mr.Read<int>();
                reportingMessage.modControl = mr.Read<int>();
                reportingMessage.modControlSha = mr.Read<string>();
                reportingMessage.gameMode = mr.Read<int>();
                reportingMessage.cheats = mr.Read<bool>();
                reportingMessage.warpMode = mr.Read<int>();
                reportingMessage.universeSize = mr.Read<long>();
                reportingMessage.banner = mr.Read<string>();
                reportingMessage.homepage = mr.Read<string>();
                reportingMessage.httpPort = mr.Read<int>();
                reportingMessage.admin = mr.Read<string>();
                reportingMessage.team = mr.Read<string>();
                reportingMessage.location = mr.Read<string>();
                reportingMessage.fixedIP = mr.Read<bool>();
                reportingMessage.players = mr.Read<string[]>();
            }
            HandleReport(client.stateObject, reportingMessage);
        }

        private void HandleReportV2(NetworkClient<ReporterClient> client, byte[] data)
        {
            ReportingMessage reportingMessage = new ReportingMessage();
            using (MessageStream2.MessageReader mr = new MessageStream2.MessageReader(data))
            {
                reportingMessage.serverHash = mr.Read<string>();
                reportingMessage.serverName = mr.Read<string>();
                reportingMessage.description = mr.Read<string>();
                reportingMessage.gamePort = mr.Read<int>();
                reportingMessage.gameAddress = IPUtils.GetSafeGameAddress(mr.Read<string>(), client.stateObject);
                reportingMessage.protocolVersion = mr.Read<int>();
                reportingMessage.programVersion = mr.Read<string>();
                reportingMessage.maxPlayers = mr.Read<int>();
                reportingMessage.modControl = mr.Read<int>();
                reportingMessage.modControlSha = mr.Read<string>();
                reportingMessage.gameMode = mr.Read<int>();
                reportingMessage.cheats = mr.Read<bool>();
                reportingMessage.warpMode = mr.Read<int>();
                reportingMessage.universeSize = mr.Read<long>();
                reportingMessage.banner = mr.Read<string>();
                reportingMessage.homepage = mr.Read<string>();
                reportingMessage.httpPort = mr.Read<int>();
                reportingMessage.admin = mr.Read<string>();
                reportingMessage.team = mr.Read<string>();
                reportingMessage.location = mr.Read<string>();
                reportingMessage.fixedIP = mr.Read<bool>();
                reportingMessage.players = mr.Read<string[]>();
            }
            HandleReport(client.stateObject, reportingMessage);
        }

        private void HandleReport(ReporterClient client, ReportingMessage message)
        {
            client.lastMessage = message;
            Console.WriteLine("RECEIVER: Report from " + client.clientID + ", players: " + message.players.Length);
            relayServer.LocalReport(client, message);
        }

        private enum MessageType
        {
            HEARTBEAT,
            REPORT_V1,
            REPORT_V2,
        }
    }
}


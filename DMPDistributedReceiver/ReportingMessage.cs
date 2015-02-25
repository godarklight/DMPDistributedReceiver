using System;

namespace DMPDistributedReceiver
{
    public class ReportingMessage
    {
        public string serverHash;
        public string serverName;
        public string description;
        public int gamePort;
        public string gameAddress;
        public int protocolVersion;
        public string programVersion;
        public int maxPlayers;
        public int modControl;
        public string modControlSha;
        public int gameMode;
        public bool cheats;
        public int warpMode;
        public long universeSize;
        public string banner;
        public string homepage;
        public int httpPort;
        public string admin;
        public string team;
        public string location;
        public bool fixedIP;
        public string[] players;

        public byte[] GetBytes()
        {
            byte[] retVal = null;
            using (MessageStream2.MessageWriter mw = new MessageStream2.MessageWriter())
            {
                mw.Write<string>(serverHash);
                mw.Write<string>(serverName);
                mw.Write<string>(description);
                mw.Write<int>(gamePort);
                mw.Write<string>(gameAddress);
                mw.Write<int>(protocolVersion);
                mw.Write<string>(programVersion);
                mw.Write<int>(maxPlayers);
                mw.Write<int>(modControl);
                mw.Write<string>(modControlSha);
                mw.Write<int>(gameMode);
                mw.Write<bool>(cheats);
                mw.Write<int>(warpMode);
                mw.Write<long>(universeSize);
                mw.Write<string>(banner);
                mw.Write<string>(homepage);
                mw.Write<int>(httpPort);
                mw.Write<string>(admin);
                mw.Write<string>(team);
                mw.Write<string>(location);
                mw.Write<bool>(fixedIP);
                mw.Write<string[]>(players);
                retVal = mw.GetMessageBytes();
            }
            return retVal;
        }

        public static ReportingMessage FromBytesBE(byte[] inputBytes)
        {
            ReportingMessage returnMessage = new ReportingMessage();
            using (MessageStream2.MessageReader mr = new MessageStream2.MessageReader(inputBytes))
            {
                returnMessage.serverHash = mr.Read<string>();
                returnMessage.serverName = mr.Read<string>();
                returnMessage.description = mr.Read<string>();
                returnMessage.gamePort = mr.Read<int>();
                returnMessage.gameAddress = mr.Read<string>();
                returnMessage.protocolVersion = mr.Read<int>();
                returnMessage.programVersion = mr.Read<string>();
                returnMessage.maxPlayers = mr.Read<int>();
                returnMessage.modControl = mr.Read<int>();
                returnMessage.modControlSha = mr.Read<string>();
                returnMessage.gameMode = mr.Read<int>();
                returnMessage.cheats = mr.Read<bool>();
                returnMessage.warpMode = mr.Read<int>();
                returnMessage.universeSize = mr.Read<long>();
                returnMessage.banner = mr.Read<string>();
                returnMessage.homepage = mr.Read<string>();
                returnMessage.httpPort = mr.Read<int>();
                returnMessage.admin = mr.Read<string>();
                returnMessage.team = mr.Read<string>();
                returnMessage.location = mr.Read<string>();
                returnMessage.fixedIP = mr.Read<bool>();
                returnMessage.players = mr.Read<string[]>();
            }
            return returnMessage;
        }
    }
}


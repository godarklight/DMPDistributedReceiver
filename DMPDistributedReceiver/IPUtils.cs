using System;
using System.Net;

namespace DMPDistributedReceiver
{
    public static class IPUtils
    {
        public static IPEndPoint[] FindEndpoint(string connectionString)
        {
            if (connectionString.StartsWith("::ffff:"))
            {
                connectionString = connectionString.Substring(7);
            }
            if (!connectionString.Contains(":") || connectionString.EndsWith("]"))
            {
                connectionString += ":9003";
            }
            string ipPart = connectionString.Substring(0, connectionString.LastIndexOf(":"));
            string portPart = connectionString.Substring(connectionString.LastIndexOf(":") + 1);
            int portInt = 9003;
            if (!Int32.TryParse(portPart, out portInt))
            {
                return new IPEndPoint[0];
            }
            //IPv6 literal
            if (ipPart.StartsWith("[") && ipPart.EndsWith("]"))
            {
                ipPart = ipPart.Substring(1, ipPart.Length - 2);
            }
            IPAddress ipAddr= null;
            if (IPAddress.TryParse(ipPart, out ipAddr))
            {
                IPEndPoint[] retVal = new IPEndPoint[1];
                retVal[0] = new IPEndPoint(ipAddr, portInt);
                return retVal;
            }
            try
            {
                IAsyncResult ar = Dns.BeginGetHostEntry(ipPart, null, null);
                if (ar.AsyncWaitHandle.WaitOne(5000))
                {
                    IPHostEntry hostEntry = Dns.EndGetHostEntry(ar);
                    IPEndPoint[] retVal = new IPEndPoint[hostEntry.AddressList.Length];
                    for (int i = 0; i < hostEntry.AddressList.Length; i++)
                    {
                        retVal[i] = new IPEndPoint(hostEntry.AddressList[i], portInt);
                    }
                    return retVal;
                }
            }
            catch
            {
                //Don't care
            }
            return new IPEndPoint[0];
        }

        public static string GetSafeGameAddress(string inputAddress, ReporterClient client)
        {
            //If there is only 1 ':' mark, they have probably incorrectly put the port after the game address. Let's cut it off.
            if (inputAddress.Contains(":") && inputAddress.IndexOf(":") == inputAddress.LastIndexOf(":"))
            {
                inputAddress = inputAddress.Substring(0, inputAddress.IndexOf(":"));
            }
            string outputAddress = inputAddress;
            IPAddress parseAddress;
            bool overrideAddress = false;
            if (inputAddress == "")
            {
                overrideAddress = true;
            }
            //Check that it's a valid IP address or DNS address.
            if (!IPAddress.TryParse(inputAddress, out parseAddress))
            {
                try
                {
                    IAsyncResult ar = Dns.BeginGetHostAddresses(inputAddress, null, null);
                    if (ar.AsyncWaitHandle.WaitOne(30000))
                    {
                        IPAddress[] addresses = Dns.EndGetHostAddresses(ar);
                        if (addresses.Length == 0)
                        {
                            overrideAddress = true;
                        }
                    }
                    else
                    {
                        overrideAddress = true;
                    }
                }
                catch
                {
                    overrideAddress = true;
                }
            }
            if (overrideAddress)
            {
                outputAddress = client.remoteIP;
            }
            return outputAddress;
        }
    }
}


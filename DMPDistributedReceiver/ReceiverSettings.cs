using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Xml;

namespace DMPDistributedReceiver
{
    public class ReceiverSettings
    {
        public string reporterHash = Guid.NewGuid().ToString();
        public int reporterPort = 9001;
        public int relayPort = 9002;
        public int dbBackendPort = 9003;
        public List<string> otherReporters = new List<string>();

        public void LoadFromFile(string fileName)
        {
            if (!File.Exists(fileName))
            {
                otherReporters.Add("d-mp.org:9002");
                otherReporters.Add("godarklight.info.tm:9002");
                SaveToFile(fileName);
            }
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(fileName);
            reporterHash = xmlDoc.DocumentElement.GetElementsByTagName("reporterHash")[0].InnerText;
            reporterPort = Int32.Parse(xmlDoc.DocumentElement.GetElementsByTagName("reporterPort")[0].InnerText);
            relayPort = Int32.Parse(xmlDoc.DocumentElement.GetElementsByTagName("relayPort")[0].InnerText);
            dbBackendPort = Int32.Parse(xmlDoc.DocumentElement.GetElementsByTagName("dbBackendPort")[0].InnerText);
            otherReporters.Clear();
            foreach (XmlNode endpointNode in xmlDoc.DocumentElement.GetElementsByTagName("remoteReporter"))
            {
                otherReporters.Add(endpointNode.InnerText);
            }
        }

        public void SaveToFile(string fileName)
        {
            string newFile = fileName + ".new";
            if (File.Exists(newFile))
            {
                File.Delete(newFile);
            }
            XmlDocument xmlDoc = new XmlDocument();
            XmlElement settingsElement = xmlDoc.CreateElement("settings");
            xmlDoc.AppendChild(settingsElement);
            //Settings
            XmlComment reporterHashComment = xmlDoc.CreateComment("Unique ID to identify this reporter. This is randomly generated and does not need to be changed");
            settingsElement.AppendChild(reporterHashComment);
            XmlElement reporterHashElement = xmlDoc.CreateElement("reporterHash");
            reporterHashElement.InnerText = reporterHash.ToString();
            settingsElement.AppendChild(reporterHashElement);

            XmlComment reporterPortComment = xmlDoc.CreateComment("The port that DMPServers will connect to");
            settingsElement.AppendChild(reporterPortComment);
            XmlElement reporterPortElement = xmlDoc.CreateElement("reporterPort");
            reporterPortElement.InnerText = reporterPort.ToString();
            settingsElement.AppendChild(reporterPortElement);

            XmlComment relayPortComment = xmlDoc.CreateComment("The port that other receivers will connect to");
            settingsElement.AppendChild(relayPortComment);
            XmlElement relayPortElement = xmlDoc.CreateElement("relayPort");
            relayPortElement.InnerText = relayPort.ToString();
            settingsElement.AppendChild(relayPortElement);

            XmlComment dbbackendPortComment = xmlDoc.CreateComment("The port that DB Backends will connect to");
            settingsElement.AppendChild(dbbackendPortComment);
            XmlElement dbBackendPortElement = xmlDoc.CreateElement("dbBackendPort");
            dbBackendPortElement.InnerText = dbBackendPort.ToString();
            settingsElement.AppendChild(dbBackendPortElement);

            XmlComment xmlComment = xmlDoc.CreateComment("Specify other reporters to connect to. You may use dns:port, ipv4:port, or [ipv6]:port format");
            settingsElement.AppendChild(xmlComment);
            foreach (string remoteAddress in otherReporters)
            {
                XmlElement remoteReporterElement = xmlDoc.CreateElement("remoteReporter");
                remoteReporterElement.InnerText = remoteAddress;
                settingsElement.AppendChild(remoteReporterElement);
            }

            //Save
            xmlDoc.Save(newFile);
            File.Move(newFile, fileName);
        }
    }
}


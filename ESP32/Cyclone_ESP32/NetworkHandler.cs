using nanoFramework.Networking;
using System;
using System.Device.Wifi;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;

namespace Cyclone_ESP32
{
    internal class NetworkHandler
    {
        private string ssid;
        private string password;
        private bool connected = false;
        private WifiAdapter wifiAdapter;

        public NetworkHandler(string ssid, string password)
        {
            this.ssid = ssid;
            this.password = password;
            wifiAdapter = WifiAdapter.FindAllAdapters()[0];
            if (!tryConnectWifi()) { 
                Console.WriteLine("Failed to connect to WiFi network.");
                while (!connected)
                {
                    Console.WriteLine("Retrying connection to WiFi network: " + ssid);
                    Thread.Sleep(5000);
                    connected = tryConnectWifi();
                }
            }
            else
            {
                Console.WriteLine("Connected to WiFi network: " + ssid);
                Thread.Sleep(Timeout.Infinite);
            }
        }

        private bool tryConnectWifi()
        {
            wifiAdapter.ScanAsync();
            Thread.Sleep(10000); // Wait for scan to complete
            foreach (WifiAvailableNetwork network in wifiAdapter.NetworkReport.AvailableNetworks)
            {
                if (network.Ssid == ssid)
                {
                    wifiAdapter.Connect(ssid, WifiReconnectionKind.Automatic, password);
                    connected = true;
                    return true;
                }
                return false;
            }
            return false;
        }

        public bool IsConnected()
        {
            return connected && NetworkInterface.GetIsNetworkAvailable();
        }

        public bool sendData()
        {

            return false;
        }
    }
}

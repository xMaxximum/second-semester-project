using nanoFramework.Hardware.Esp32;
using nanoFramework.M2Mqtt;
using nanoFramework.M2Mqtt.Messages;
using System;
using System.Device.Wifi;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;

namespace Cyclone_ESP32
{
    public class MQTT
    {
        private static MqttClient client; // the client object
        private bool publishEnabled;
        private string brokerHostname = "mqtt-dhbw-hdh-ai2024.duckdns.org";

        public bool Publish
        {
            set
            {
                publishEnabled = value;
            }
        }

        // constructor does initialization of state and event handlers
        public MQTT()
        {
            publishEnabled = false;
        }


        private void Connect()
        {
            // setup network
            // TODO: set multiple credentials on the microsd and use them for the available networks.
            // currently credentials (only one user) are saved to memory by the visual studio extension
            SetupAndConnectNetwork();

            // Connect to MQTT broker
            ConnectToBroker(brokerHostname);

            // after successful connection register connection closed event
            client.ConnectionClosed += OnMqttConnectionClosed;
        }

        public void DoWork()
        {
            bool endOfFile = false; // Is true when filestream is finished
            if (publishEnabled) // only do something if pusblishing is enabled
            {
                while (!endOfFile) // endOfFile should come from Filesystem class that knows which line is currently processed                
                {
                    if (!PublishMessage("Filesystem data as one line of file in csv format")) // on error while publishing try to reconnec
                        Connect(); // reconnect the damn thing, creates new client object and frees resources from the previous one   
                }
            }
        }


        private void OnMqttConnectionClosed(object sender, EventArgs e)
        {
            //Debug.WriteLine("MQTT connection closed. Attempting to reconnect...");
            if (publishEnabled)
            {

                Thread.Sleep(5000); // wait for 2 seconds before reconnecting
                Connect();
                // Finish publishing
                DoWork();
            }
        }

        public bool PublishMessage(string payload)
        {
            if (client == null)
                return false;

            if (client.IsConnected)
            {
                try
                {
                    client.Publish(
                 "esp32-mqtt-client/test",
                 Encoding.UTF8.GetBytes(payload),
                 null, null,
                 MqttQoSLevel.ExactlyOnce, // Message with acknowledgement of retrieval
                 true); // retain
                    Thread.Sleep(500);
                    Debug.WriteLine("Message sent!");
                    uint totalSize, totalFreeSize, largestFreeBlock;
                    NativeMemory.GetMemoryInfo(NativeMemory.MemoryType.All, out totalSize, out totalFreeSize, out largestFreeBlock);
                    long used = totalSize - totalFreeSize;
                    Debug.WriteLine($"Total memory: {totalSize} bytes, Used: {used} bytes, Largest free block: {largestFreeBlock} bytes");
                    return true;
                }
                catch (NullReferenceException)
                {
                    Debug.WriteLine("The M2Mqtt library has thrown a null reference exception");
                    return false;
                }
            }
            else
            {
                Debug.WriteLine("MQTT client is not connected. Cannot publish message.");
                return false;
            }
        }

        private void ConnectToBroker(string hostname)
        {
            client = null; // free resources from possible previous lost connection, object is invalid anyway
            bool connectSuccessful = false;
            while (!connectSuccessful)
            {
                try
                {
                    client = new MqttClient(hostname);
                    var clientId = Guid.NewGuid().ToString();
                    client.Connect(clientId, "username", "password");
                    connectSuccessful = true;
                    break;
                }
                catch (System.Net.Sockets.SocketException ex)
                {
                    // the first connection attempt always fails with this exception (bad implementation of the m2mqtt library)
                    // have to investigate this issue in the future
                    Debug.WriteLine("SocketException while initialising the connection to the broker " + ex.Message);
                    Thread.Sleep(5000); // Timeout because broker is overloaded
                }
                catch (nanoFramework.M2Mqtt.Exceptions.MqttConnectionException ex)
                {
                    Debug.WriteLine("SocketException while connecting to the broker: " + ex.Message);
                    client.Dispose(); // Dispose the client to free resources, Exception on client.Connect
                    Thread.Sleep(5000); // Timeout because broker is overloaded
                }
                catch (nanoFramework.M2Mqtt.Exceptions.MqttCommunicationException ex)
                {
                    Debug.WriteLine("SocketException while connecting to the broker: " + ex.Message);
                    client.Dispose(); // Dispose the client to free resources, Exception on client.Connect
                    Thread.Sleep(5000); // Timeout because broker is overloaded
                }
            }
        }


        /// <summary>
        /// This is a helper function to pick up first available network interface and use it for communication.
        /// </summary>
        private void SetupAndConnectNetwork()
        {
            // Get the first WiFI Adapter
            var wifiAdapter = WifiAdapter.FindAllAdapters()[0];

            while (wifiAdapter == null)
            {
                Debug.WriteLine("No WiFi adapter found. Waiting for 5 seconds before retrying...");
                Thread.Sleep(5000);
                wifiAdapter = WifiAdapter.FindAllAdapters()[0];
            }

            // Begin network scan.
            wifiAdapter.ScanAsync();

            // While networks are being scan, continue on configuration. If networks were set previously, 
            // board may already be auto-connected, so reconnection is not even needed.
            var wiFiConfiguration = Wireless80211Configuration.GetAllWireless80211Configurations()[0];
            var ipAddress = NetworkInterface.GetAllNetworkInterfaces()[0].IPv4Address;
            var needToConnect = string.IsNullOrEmpty(ipAddress) || (ipAddress == "0.0.0.0");
            while (needToConnect)
            {
                foreach (var network in wifiAdapter.NetworkReport.AvailableNetworks)
                {
                    // Show all networks found
                    Debug.WriteLine($"Net SSID :{network.Ssid},  BSSID : {network.Bsid},  rssi : {network.NetworkRssiInDecibelMilliwatts},  signal : {network.SignalBars}");

                    // If its our Network then try to connect
                    if (network.Ssid == wiFiConfiguration.Ssid)
                    {

                        var result = wifiAdapter.Connect(network, WifiReconnectionKind.Automatic, wiFiConfiguration.Password);

                        if (result.ConnectionStatus == WifiConnectionStatus.Success)
                        {
                            Debug.WriteLine($"Connected to Wifi network {network.Ssid}.");
                            needToConnect = false;
                        }
                        else
                        {
                            Debug.WriteLine($"Error {result.ConnectionStatus} connecting to Wifi network {network.Ssid}.");
                        }
                    }
                }

            }
            ipAddress = NetworkInterface.GetAllNetworkInterfaces()[0].IPv4Address;
            Debug.WriteLine($"Connected to Wifi network with IP address {ipAddress}");
        }
    }
}

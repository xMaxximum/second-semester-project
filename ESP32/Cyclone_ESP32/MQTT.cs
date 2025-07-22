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
        private static int lastMessage; // last message that was sent (used to continue sending messages after connection is lost)

        // constructor does initialization and starts the MQTT client + publishes messages directly (prototype)
        // this needs some work OOP wise obviously
        MQTT()
        {
            string timeSinceStart = "PT1H30M";
            double currentTemperature = 22.5;
            double currentSpeed = 15.0;
            double latitude = 51.123456;
            double longitude = 8.123456;
            double averagedAccelerationX = 0.5;
            double averagedAccelerationY = 0.5;
            double averagedAccelerationZ = 0.5;
            double peakAccelerationX = 2.0;
            double peakAccelerationY = 2.0;
            double peakAccelerationZ = 2.0;

            // Checksumme berechnen
            double checksum = currentTemperature + currentSpeed + latitude + longitude +
                              averagedAccelerationX + averagedAccelerationY + averagedAccelerationZ +
                              peakAccelerationX + peakAccelerationY + peakAccelerationZ;

            // JSON manuell zusammenbauen
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"time_since_start\":\"").Append(timeSinceStart).Append("\",");
            sb.Append("\"current_temperature\":").Append(currentTemperature).Append(",");
            sb.Append("\"current_speed\":").Append(currentSpeed).Append(",");
            sb.Append("\"current_coordinates\":{");
            sb.Append("\"latitude\":").Append(latitude).Append(",");
            sb.Append("\"longitude\":").Append(longitude).Append("},");
            sb.Append("\"averaged_acceleration_x\":").Append(averagedAccelerationX).Append(",");
            sb.Append("\"averaged_acceleration_y\":").Append(averagedAccelerationY).Append(",");
            sb.Append("\"averaged_acceleration_z\":").Append(averagedAccelerationZ).Append(",");
            sb.Append("\"peak_acceleration_x\":").Append(peakAccelerationX).Append(",");
            sb.Append("\"peak_acceleration_y\":").Append(peakAccelerationY).Append(",");
            sb.Append("\"peak_acceleration_z\":").Append(peakAccelerationZ).Append(",");
            sb.Append("\"checksum\":").Append(checksum);
            sb.Append("}");

            string jsonPayload = sb.ToString();


            // STEP 1: setup network
            // You need to set Wifi connection credentials in the configuration first!
            // Go to Device Explorer -> Edit network configuration -> Wifi proiles and set SSID and password there.
            SetupAndConnectNetwork();

            // STEP 2: connect to MQTT broker
            // Warning: test.mosquitto.org is very slow and congested, and is only suitable for very basic validation testing.
            // Change it to your local broker as soon as possible.
            ConnectToBroker("test.mosquitto.org");
            // after successful connection register connection closed event
            client.ConnectionClosed += OnMqttConnectionClosed;


            // Publish
            for (int i = 0; i < 100; i++)
            {
                PublishMessage(client, jsonPayload);
                lastMessage = i;
                Debug.WriteLine($"lastMessage: {lastMessage}");
            }
        }



        private static void OnMqttConnectionClosed(object sender, EventArgs e)
        {
            Debug.WriteLine("MQTT connection was closed!");
            ConnectToBroker("test.mosquitto.org");
            Debug.WriteLine("MQTT connection is established again!");
            // Finish publishing
            for (int i = lastMessage; i < 100; i++)
            {
                Debug.WriteLine($"lastMessage: {lastMessage}");
                PublishMessage(client, "Filesystem data");
            }
            Debug.WriteLine("Publishing finished.");
        }

        public static void PublishMessage(MqttClient client, string jsonPayload)
        {
            if (client.IsConnected)
            {
                client.Publish(
                     "nf-mqtt/basic-demo-580842750752075423750435789452",
                     Encoding.UTF8.GetBytes(jsonPayload),
                     null, null,
                     MqttQoSLevel.AtLeastOnce,
                     false);
                Thread.Sleep(500);
                Debug.WriteLine("Message sent!");
            }
            else
            {
                Debug.WriteLine("Client is disconnected");
                // The client is disconnected, try to reconnect
                ConnectToBroker("test.mosquitto.org");
                PublishMessage(client, jsonPayload);
            }

        }

        private static void ConnectToBroker(string hostname)
        {
            client = null;
            bool connectSuccessful = false;
            while (!connectSuccessful)
            {
                try
                {
                    client = new MqttClient("test.mosquitto.org");
                    var clientId = Guid.NewGuid().ToString();
                    client.Connect(clientId);
                    connectSuccessful = true;

                    break;
                }
                catch (System.Net.Sockets.SocketException ex)
                {
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
        private static void SetupAndConnectNetwork()
        {
            // Get the first WiFI Adapter
            var wifiAdapter = WifiAdapter.FindAllAdapters()[0];

            // Begin network scan.
            wifiAdapter.ScanAsync();

            // While networks are being scan, continue on configuration. If networks were set previously, 
            // board may already be auto-connected, so reconnection is not even needed.
            var wiFiConfiguration = Wireless80211Configuration.GetAllWireless80211Configurations()[0];
            var ipAddress = NetworkInterface.GetAllNetworkInterfaces()[0].IPv4Address;
            var needToConnect = string.IsNullOrEmpty(ipAddress) || (ipAddress == "0.0.0.0");
            //while (needToConnect)
            //{
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

            //}

            ipAddress = NetworkInterface.GetAllNetworkInterfaces()[0].IPv4Address;
            Debug.WriteLine($"Connected to Wifi network with IP address {ipAddress}");
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;


namespace Cyclone_ESP32
{
    public class Program
    {
        public static void Main()
        {
            MQTT mqttClient = new MQTT();

            while (true)
            {
                // tells MQTT client to publish messages
                mqttClient.Publish = true;

                // perform MQTT client work, which includes publishing messages
                mqttClient.DoWork();
            }
        }
    }
}

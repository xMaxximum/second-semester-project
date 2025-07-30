using Iot.Device.Common.GnssDevice;
using nanoFramework.Hardware.Esp32;
using System;
using System.Diagnostics;
using System.Threading;

namespace Cyclone_ESP32 { 
    public class gps
    {
        private static GenericSerialGnssDevice gpsModule;
        public static void Setup()
            {
            // Some modules like ESP32 requires to setup serial pins
            // Configure GPIOs 16 and 17 to be used in UART2 (that's refered as COM3)
            Configuration.SetPinFunction(16, DeviceFunction.COM2_RX);
            Configuration.SetPinFunction(17, DeviceFunction.COM2_TX);

            // By default baud rate is 9600
            Nmea0183Parser.AddParser(new TxtData());
            gpsModule = new GenericSerialGnssDevice("COM2");
            gpsModule.FixChanged += FixChanged;
            gpsModule.LocationChanged += LocationChanged;
            gpsModule.OperationModeChanged += OperationModeChanged;
            gpsModule.ParsingError += ParsingError;
            gpsModule.ParsedMessage += ParsedMessage;
            gpsModule.UnparsedMessage += UnparsedMessage;

            gpsModule.Start();

            Thread.Sleep(Timeout.Infinite);
        }

        private static void UnparsedMessage(string message)
        {
            Console.WriteLine($"Received unparsed message: {message}");
        }

        private static void ParsedMessage(NmeaData data)
        {
            Console.WriteLine($"Received parsed message: {data.GetType()}");
            if (data is TxtData txtData)
            {
                Console.WriteLine($"Received TXT message: {txtData.Text}, severity: {txtData.Severity}");
            }
        }

        private static void ParsingError(Exception exception)
        {
            Console.WriteLine($"Received parsed error: {exception.Message}");
        }

        private static void OperationModeChanged(GnssOperation mode)
        {
            Console.WriteLine($"Received Operation Mode changed: {mode}");
        }

        private static void LocationChanged(Location position)
        {
            Console.WriteLine($"Received position changed: {position.Latitude},{position.Longitude}");
        }

        private static void FixChanged(Fix fix)
        {
            Console.WriteLine($"Received Fix changed: {fix}");
        }
        public static void TryGetCurrentPosition()
        {
            Console.WriteLine("lattitude: " + gpsModule.Location.Latitude.ToString());
            Console.WriteLine("longitude: " + gpsModule.Location.Longitude.ToString()); 
        }
    }

}

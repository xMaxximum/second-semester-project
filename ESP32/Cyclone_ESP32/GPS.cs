using Iot.Device.Common.GnssDevice;
using nanoFramework.Hardware.Esp32;
using System;
using System.Device.Gpio;
using System.Diagnostics;
using System.Threading;

namespace Cyclone_ESP32 { 
    public class gps
    {
        private static GenericSerialGnssDevice gpsModule;
        private static Location newestPosition;
        public static void Setup()
        {
            try
            {
                Configuration.SetPinFunction(16, DeviceFunction.COM3_RX);
                Configuration.SetPinFunction(17, DeviceFunction.COM3_TX);

                Nmea0183Parser.AddParser(new TxtData());

                gpsModule = new GenericSerialGnssDevice("COM3", 9600);

                gpsModule.FixChanged += FixChanged;
                gpsModule.LocationChanged += LocationChanged;
                gpsModule.OperationModeChanged += OperationModeChanged;
                gpsModule.ParsingError += ParsingError;
                gpsModule.ParsedMessage += ParsedMessage;
                gpsModule.UnparsedMessage += UnparsedMessage;

                gpsModule.Start();

                Console.WriteLine("GPS module started successfully.");
                
                Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Console.WriteLine("GPS Setup Exception: " + ex.Message);
            }
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
            newestPosition = position;
        }

        private static void FixChanged(Fix fix)
        {
            Console.WriteLine($"Received Fix changed: {fix}");
            
        }
        public static Location TryGetCurrentPosition()
        { //null if no location, location if availible
            if(gpsModule.Location.Latitude == 0 || gpsModule.Location.Longitude == 0)
            {
                Console.WriteLine("No GPS fix available.");
                Console.WriteLine("Sattelites: " + gpsModule.SatellitesInView);
                Console.WriteLine("Fix: " + gpsModule.Fix);
                return null;
            }
            else
            {
                Console.WriteLine($"Current Position: Latitude: {gpsModule.Location.Latitude}, Longitude: {gpsModule.Location.Longitude}");
                return newestPosition;
            }
        }
    }

}

using nanoFramework.Hardware.Esp32;
using System;
using System.Text;
using System.Threading;

namespace Cyclone_ESP32
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            GPS gpsModule = new GPS();
            while(true)
            { 
                Console.WriteLine($"Latitude: {gpsModule.CurrentPosition.Latitude} Longitude: {gpsModule.CurrentPosition.Longitude}"); 
                Thread.Sleep(1000);
            }
            

        }
    }
}

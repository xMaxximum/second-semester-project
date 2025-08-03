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
            // Initialize the GPS module
            Thread gpsThread = new Thread(gps.Setup)
            {
                
                Priority = ThreadPriority.AboveNormal    // ensures the thread will not block process exit
            };
            gpsThread.Start();
            // Keep the application running
            Console.WriteLine("Press any key to exit...");
            while (true)
            {
                gps.TryGetCurrentPosition();
                Thread.Sleep(5000); // Sleep for 1 second to avoid busy waiting
            }
            
        }
    }
}

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
                Priority = ThreadPriority.BelowNormal    // ensures the thread will not block process exit
            };
            gpsThread.Start();
            // Keep the application running
            Console.WriteLine("Press any key to exit...");
            while (true)
            {
                Thread.Sleep(4000);
                gps.TryGetCurrentPosition();
            }
            
        }
    }
}

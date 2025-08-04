using System;
using System.IO;
using System.Text;
using System.Device.Spi;
using System.Device.Gpio;
using nanoFramework.Hardware.Esp32;
using nanoFramework.System.IO.FileSystem;

namespace Cyclone_ESP32
{
    public class Filesystem
    {
        // sdcard adapter board
        
        private const int SpiBusId = 1;
        /*
        private const int PinCS = 2;   // Beispiel: GPIO22 für CS (angepasst)
        private const int PinSCK = 2;  // Beispiel: GPIO18 für SCK
        private const int PinMISO = 4; // Beispiel: GPIO19 für MISO
        private const int PinMOSI = 16; // Beispiel: GPIO23 für MOSI
        */

        // esp cam onboard sdcard
        private const int PinCS = 2;   // Beispiel: GPIO22 für CS (angepasst)
        private const int PinSCK = 15;  // Beispiel: GPIO18 für SCK
        private const int PinMISO = 13; // Beispiel: GPIO19 für MISO
        private const int PinMOSI = 14; // Beispiel: GPIO23 für MOSI

        private SDCard mycard0;


        private void InitializeSdCard()
        {
            // Pin-Mapping für ESP32
            Configuration.SetPinFunction(PinSCK, DeviceFunction.SPI1_CLOCK);
            Configuration.SetPinFunction(PinMISO, DeviceFunction.SPI1_MISO);
            Configuration.SetPinFunction(PinMOSI, DeviceFunction.SPI1_MOSI);

            try
            {
                // SDCard-Initialisierung mit expliziten Parametern
                mycard0 = new SDCard(new SDCardSpiParameters
                {
                    spiBus = SpiBusId,
                    chipSelectPin = PinCS
                });
                
                mycard0.Mount();
                Console.WriteLine("SD-Karte erfolgreich gemountet.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Mounten der SD-Karte: {ex.Message}");
            }
        }

        public void ReadFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var content = File.ReadAllText(filePath);
                    Console.WriteLine($"Dateiinhalt von {filePath}:");
                    Console.WriteLine(content);
                }
                else
                {
                    Console.WriteLine($"Datei {filePath} nicht gefunden.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Lesen der Datei: {ex.Message}");
            }
        }

        public void WriteFile(string filePath, string content)
        {
            try
            {
                File.WriteAllText(filePath, content);
                Console.WriteLine($"Inhalt erfolgreich in {filePath} geschrieben.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Schreiben der Datei: {ex.Message}");
            }
        }

        public Filesystem()
        {
            InitializeSdCard();
        }
    }
}

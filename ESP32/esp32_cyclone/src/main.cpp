#include "FS.h"
#include "SD.h"
#include "SPI.h"

// write the full array (before esp panics because of full RAM) sensorData to sdcard (every ~8 minutes, takes 220ms)
void writeSensorDataBlock();
// setup sdcard connection over SPI bus
void setupFileSystem();

#define CUSTOM_MOSI 16
#define CUSTOM_MISO 4
#define CUSTOM_SCK 15
#define CUSTOM_CS 2
#define RAM_ARR 24000 // 2000 sensorData packets (2000 * 12 count of sensor values is 24000)
// 24000 * 4 bytes for a float is 96kB of RAM
#define SENSOR_DATA_SIZE 12

// data specific
// this is the address to where the sensor data is stored in the heap (96kB of RAM)
float *sensorData;
uint bufferCount = 0;

// filesystem
uint timeBeforeWrite, timeAfterWrite;
// the object for the sdcard file
File file;

void setup()
{
  Serial.begin(115200);
  // wait for serial monitor to connect
  delay(2000);

  setupFileSystem();

  // reserve memory for sensor data
  sensorData = (float *)malloc(RAM_ARR * sizeof(float));
}

void loop()
{

  // every 200ms the sensor data is put into the array
  // bufferCount++;
  // sensorData[bufferCount * SENSOR_DATA_SIZE] = temperature
  // sensorData[bufferCount * SENSOR_DATA_SIZE + 1] = current speed
  // sensorData[bufferCount * SENSOR_DATA_SIZE + 2] = latitude
  // ...
  // sensorData[bufferCount * SENSOR_DATA_SIZE + 11] = checksum

  // write the full array (before esp panics because of full RAM) sensorData to sdcard (every ~8 minutes, takes 220ms)
  if (bufferCount == 2000)
    writeSensorDataBlock();
}

void setupFileSystem()
{
  SPI.begin(CUSTOM_SCK, CUSTOM_MISO, CUSTOM_MOSI, CUSTOM_CS);

  if (!SD.begin())
  {
    Serial.println("Card Mount Failed");
    return;
  }

  uint64_t cardSize = SD.cardSize() / (1024 * 1024);
  Serial.printf("SD Card Size: %lluMB\n", cardSize);
  Serial.printf("Total space: %lluMB\n", SD.totalBytes() / (1024 * 1024));
  Serial.printf("Used space: %lluMB\n", SD.usedBytes() / (1024 * 1024));
}

void writeSensorDataBlock()
{

  // open the file where the array data is streamed into
  file = SD.open("/sensorData.bin", FILE_WRITE);
  if (!file)
  {
    Serial.println("Error opening file");
    return;
  }

  timeBeforeWrite = millis();
  // write the array to the file
  file.write((uint8_t *)sensorData, RAM_ARR * sizeof(float));
  timeAfterWrite = millis();
  Serial.print("Writetime: ");
  Serial.println(timeAfterWrite - timeBeforeWrite);
  file.close();

  // the space of the array can now be used again
  free(sensorData);

  Serial.println("Writing data is finished.\n");
}

// TEST: read the file from sdcard into a different array for testing
// http rest api client reads data and sends it to the backend
void testRead()
{
  file = SD.open("/sensorData.bin", FILE_READ);
  if (!file)
  {
    Serial.println("Error opening file");
    return;
  }
  // allocate the needed ram for the sensor data
  float *sensorDataRead = (float *)malloc(RAM_ARR * sizeof(float));
  // sensorDataRead contains the actual data of the sdcard now
  file.read((uint8_t *)sensorDataRead, RAM_ARR * sizeof(float));
  file.close();

  Serial.println("This is the file content:");
  for (int i = 0; i < 100; i++)
  {
    Serial.println(sensorDataRead[i]);
  }
  free(sensorDataRead);
}

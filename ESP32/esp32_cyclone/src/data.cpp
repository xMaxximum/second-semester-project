#include <constants.h>
#include <data.h>

void setupFileSystem()
{
  uint64_t cardSize;
  Serial.println("Setting up sdcard...");

  SPI.begin(CUSTOM_SCK, CUSTOM_MISO, CUSTOM_MOSI, CUSTOM_CS);
  if (!SD.begin(CUSTOM_CS, SPI))
  {
    Serial.println("Card Mount Failed");
    delay(1000);
    setupFileSystem();
  }
  cardSize = SD.cardSize() / (1024 * 1024);

  Serial.printf("SD Card Size: %lluMB\n", cardSize);
  Serial.printf("Total space: %lluMB\n", SD.totalBytes() / (1024 * 1024));
  Serial.printf("Used space: %lluMB\n", SD.usedBytes() / (1024 * 1024));
}

void openFile(const char *filename, const char *mode, File &file)
{
  Serial.print("Opening a file named ");
  Serial.print(filename);
  Serial.print(" to ");
  Serial.println(mode);
  file = SD.open(filename, mode, true);

  if (!file)
  {
    Serial.print("Error opening file: ");
    Serial.println(filename);
    setupFileSystem();
    openFile(filename, mode, file);
  }
}

void writeSensorDataBlock(float *sensorData, File &file, int bufferCounter)
{
  // open the file where the array data is streamed into
  openFile("/sensorData.bin", FILE_APPEND, file);

  // write the array to the file
  if (bufferCounter == 0)
  {
    Serial.println("writing the full buffer sensor data to sensorData.bin...");
    file.write((uint8_t *)sensorData, RAM_ARR * sizeof(float)); // write a full buffer to the sdcard
  }
  else
    file.write((uint8_t *)sensorData, bufferCounter * sizeof(float)); // write the not full buffer to the sdcard

  // reset the buffer for the next data collection
  for (size_t i = 0; i < RAM_ARR; i++)
    sensorData[i] = 0;

  file.close();
  Serial.println("Writing data to sdcard finished.\n");
}

#include "FS.h"
#include "SD.h"
#include "SPI.h"
#include <WiFi.h>
#include "SD_MMC.h"
#include <HTTPClient.h>
#include "string.h"

// write the full array (before esp panics because of full RAM) sensorData to sdcard (every ~8 minutes, takes 220ms)
void writeSensorDataBlock();
// setup sdcard connection over SPI bus
void setupFileSystem();
void getSpeed();
// setup wlan connection with sdcard credentials
void setupWlan();
// unified function for opening files depending on the SDMMC or SPI connection type
void openFile(const char *filename, const char *mode);
void testRead();
char *convertBufferToCSV(float *sensorData, int length);

// interface used for sdcard (false is SPI)
#define SDMMC true
#define CUSTOM_MOSI 16
#define CUSTOM_MISO 4
#define CUSTOM_SCK 15
#define CUSTOM_CS 2
#define RAM_ARR 24000 // 2000 sensorData packets (2000 * 12 count of sensor values is 24000)
// 24000 * 4 bytes for a float is 96kB of RAM
#define SENSOR_DATA_SIZE 12

// magnet sensor
#define PIN_MAGNET 17
#define WHEEL_DIAMETER 0.6 // 26 inch wheel

// data specific
// this is the address to where the sensor data is stored in the heap (96kB of RAM)
float *sensorData;
uint bufferCount = 0;

// filesystem
uint timeBeforeWrite, timeAfterWrite;
// the object for the sdcard file
File file;

// helper variables
// magnet sensor positive flank recognition (rpm)
int lastState = LOW, currentState, flankCount = 0, rpm, speed;

// only get data every 200ms
unsigned long currentTime = 0, lastReadTime200ms = 0, lastReadTime1000ms = 0, dtTo1000ms = 0, dtTo200ms = 0;

// http client to send data to rest api with post
HTTPClient http;

void setup()
{
  Serial.begin(115200);
  // wait for serial monitor to connect
  delay(3000);

  setupFileSystem();
  // wifi.begin is a huge problem right now in relation to sdmmc.open function
  setupWlan(); // only use wlan when it is needed because memory management of wifi library fucks up sdmmc library memory access (LoadProhibited error after call of SD_MMC.open after wifi.begin call)

  Serial.print("Free heap: ");
  Serial.println(ESP.getFreeHeap());
  // reserve memory for sensor data
  sensorData = (float *)malloc(RAM_ARR * sizeof(float));
  Serial.print("Free heap: ");
  Serial.println(ESP.getFreeHeap());

  // digital input for rpm sensor (magnet sensor)
  pinMode(PIN_MAGNET, INPUT); // For an input with internal pull-up resistor
}

void loop()
{
  currentTime = millis();

  getSpeed();

  dtTo200ms = currentTime - lastReadTime200ms;
  if (dtTo200ms >= 200)
  {
    bufferCount++;
    sensorData[bufferCount * SENSOR_DATA_SIZE] = 0; // temperature
    sensorData[bufferCount * SENSOR_DATA_SIZE + 1] = (float)speed;
    sensorData[bufferCount * SENSOR_DATA_SIZE + 2] = 0; // latitude
    sensorData[bufferCount * SENSOR_DATA_SIZE + 3] = 0;
    sensorData[bufferCount * SENSOR_DATA_SIZE + 4] = 0;
    sensorData[bufferCount * SENSOR_DATA_SIZE + 5] = 0;
    sensorData[bufferCount * SENSOR_DATA_SIZE + 6] = 0;
    sensorData[bufferCount * SENSOR_DATA_SIZE + 7] = 0;
    sensorData[bufferCount * SENSOR_DATA_SIZE + 8] = 0;
    sensorData[bufferCount * SENSOR_DATA_SIZE + 9] = 0;
    sensorData[bufferCount * SENSOR_DATA_SIZE + 10] = 0;
    sensorData[bufferCount * SENSOR_DATA_SIZE + 11] = speed; // checksum is only speed right now
    lastReadTime200ms = currentTime;
  }

  // write the full array (before esp panics because of full RAM) sensorData to sdcard (every ~8 minutes, takes 220ms)
  if (bufferCount == 2000)
  {
    bufferCount = 0; // reset buffer size because ram is free after save to sdcard
    writeSensorDataBlock();
  }

  testRead();
  delay(5000);
}

void getSpeed()
{
  currentState = digitalRead(PIN_MAGNET);

  dtTo1000ms = currentTime - lastReadTime1000ms;
  // Check for negative flank
  if (lastState == HIGH && currentState == LOW)
    flankCount++;

  lastState = currentState;

  // negative flanks / second (rpm is flanks per minute)
  if (dtTo1000ms >= 1000)
  {
    lastReadTime1000ms = currentTime;
    rpm = flankCount * 60;
    // calculate the speed
    speed = (rpm * PI * WHEEL_DIAMETER) / 60 * 3.6;
    Serial.print("Speed (RPM): ");
    Serial.println(rpm);
    Serial.print("Speed: ");
    Serial.print(speed);
    Serial.println(" km/h");
    flankCount = 0;
  }
}

void setupFileSystem()
{
  uint64_t cardSize;
  Serial.println("Setting up sdcard...");
  if (SDMMC)
  {
    // Initialize the SD card
    if (!SD_MMC.begin("/sdcard", true))
    {
      Serial.println("Failed to mount SD card");
      delay(1000);
      setupFileSystem();
    }
    cardSize = SD_MMC.cardSize() / (1024 * 1024);
  }
  else
  {
    SPI.begin(CUSTOM_SCK, CUSTOM_MISO, CUSTOM_MOSI, CUSTOM_CS);
    if (!SD.begin())
    {
      Serial.println("Card Mount Failed");
      delay(1000);
      setupFileSystem();
    }
    cardSize = SD.cardSize() / (1024 * 1024);
  }

  Serial.printf("SD Card Size: %lluMB\n", cardSize);
  Serial.printf("Total space: %lluMB\n", SD.totalBytes() / (1024 * 1024));
  Serial.printf("Used space: %lluMB\n", SD.usedBytes() / (1024 * 1024));
}

void setupWlan()
{
  File fileForWlanCredentials;
  // openFile("/credentials.txt", FILE_READ);
  fileForWlanCredentials = SD_MMC.open("/credentials.txt", FILE_READ);

  String buffer = fileForWlanCredentials.readString();
  fileForWlanCredentials.close();

  String ssid, password;
  // get the index of the comma char
  int commaIndex = buffer.indexOf(',');
  if (commaIndex != -1)
  {
    ssid = buffer.substring(0, commaIndex);
    password = buffer.substring(commaIndex + 1);

    // remove any newline character from the password if one is there
    password.trim();
  }
  else
  {
    Serial.println("Invalid format for the credentials on the sdcard. Must be ssid,pass");
  }
  Serial.println("Connecting");

  Serial.print("Free heap before wifi.begin: ");
  Serial.println(ESP.getFreeHeap());
  /*WiFi.begin(ssid, password);
  while (WiFi.status() != WL_CONNECTED)
  {
    delay(500);
    Serial.print(".");
  }*/
  Serial.print("Free heap after wifi.begin: ");
  Serial.println(ESP.getFreeHeap());

  Serial.println("");
  Serial.print("Connected to WiFi network with IP Address: ");
  Serial.println(WiFi.localIP());
  WiFi.mode(WIFI_OFF);
  Serial.print("Free heap after wifi.mode (off) ");
  Serial.println(ESP.getFreeHeap());
  Serial.println("This frees some memory but sdmmc will fail from here on......");
}

void writeSensorDataBlock()
{

  // open the file where the array data is streamed into
  openFile("/sensorData.bin", FILE_WRITE);

  timeBeforeWrite = millis();
  // write the array to the file
  file.write((uint8_t *)sensorData, RAM_ARR * sizeof(float));
  file.close();
  timeAfterWrite = millis();
  Serial.print("Writetime: ");
  Serial.println(timeAfterWrite - timeBeforeWrite);

  // the space of the array can now be used again
  free(sensorData);

  Serial.println("Writing data is finished.\n");
}

// TEST: read the file from sdcard into a different array for testing
// http rest api client reads data and sends it to the backend
// function fills two ram buffers -> saves them to sdcard -> reads one and prints data as csv -> reads the next and prints data as csv
// difference to function in loop() is that this buffer there fills until 96kB of RAM and not only two 48 Byte buffers like in here
void testRead()
{
  int testSizeOfBuffer = 90;
  // allocate the needed ram for the sensor data
  float *sensorData1 = (float *)malloc(testSizeOfBuffer * sizeof(float));
  float *sensorData2 = (float *)malloc(testSizeOfBuffer * sizeof(float));
  float *sensorDataRead1 = (float *)malloc(testSizeOfBuffer * sizeof(float));
  float *sensorDataRead2 = (float *)malloc(testSizeOfBuffer * sizeof(float));
  // some test sensor data
  sensorData1[0] = 1337.1337;
  sensorData1[1] = -31337.31337;
  for (size_t i = 2; i < testSizeOfBuffer; i++)
    sensorData1[i] = i;

  // another buffer with test sensor data
  sensorData2[0] = 123456789.12345;
  sensorData2[1] = 50.3;
  for (size_t i = 2; i < testSizeOfBuffer; i++)
    sensorData2[i] = i;

  // open the file where the array data is streamed into
  openFile("/sensorData.bin", FILE_WRITE);

  // write the buffers to the file
  file.write((uint8_t *)sensorData1, testSizeOfBuffer * sizeof(float));
  file.write((uint8_t *)sensorData2, testSizeOfBuffer * sizeof(float));
  file.close();

  // sensorDataRead contains the actual data of the sdcard now
  openFile("/sensorData.bin", FILE_READ);

  // we want to upload the data to the backend now
  file.read((uint8_t *)sensorDataRead1, testSizeOfBuffer * sizeof(float));
  file.read((uint8_t *)sensorDataRead2, testSizeOfBuffer * sizeof(float));
  file.close();

  Serial.println("sensorDataRead1:");
  for (int i = 0; i < testSizeOfBuffer; i++)
    Serial.println(sensorDataRead1[i]);
  Serial.println("sensorDataRead2:");
  for (int i = 0; i < testSizeOfBuffer; i++)
    Serial.println(sensorDataRead2[i]);

  Serial.println(convertBufferToCSV(sensorDataRead1, testSizeOfBuffer));
  Serial.println(convertBufferToCSV(sensorDataRead2, testSizeOfBuffer));
}

char *convertBufferToCSV(float *sensorDataBuffer, int length)
{
  // convert the array to string csv
  char *buffer = (char *)malloc(length * 20 * sizeof(char)); // the needed bytes are calculated by the number of float values (length) that need 20 chars to be displayed at best
  buffer[0] = '\0';

  for (int i = 0; i < length; i++)
  {
    if (i % 9 == 0)
      strcat(buffer, "\n"); // append , to buffer
    else if (i > 0)
      strcat(buffer, ",");                    // append , to buffer
    char temp[20];                            // sufficient length is needed
    dtostrf(sensorDataBuffer[i], 6, 2, temp); // convert float to string and copy it inside temp, this function does only put a max of 6 digits and 2 decimal places
    strcat(buffer, temp);                     // append converted float to string
  }

  return buffer;
}

void openFile(const char *filename, const char *mode)
{
  Serial.print("Opening a file named ");
  Serial.print(filename);
  Serial.print(" to ");
  Serial.println(mode);
  if (SDMMC)
    file = SD_MMC.open(filename, mode, true);
  else
    file = SD.open(filename, mode, true);

  if (!file)
  {
    Serial.print("Error opening file: ");
    Serial.println(filename);
    setupFileSystem();
    openFile(filename, mode);
  }
}

#include "FS.h"
#include "SD.h"
#include "SPI.h"
#include <WiFi.h>
#include "SD_MMC.h"
#include <HTTPClient.h>
#include "string.h"
#include <Preferences.h>
#include "TinyGPS++.h"
#include <Arduino.h>

// write the full array (before esp panics because of full RAM) sensorData to sdcard (every ~8 minutes, takes 220ms)
void writeSensorDataBlock();
// setup sdcard connection over SPI bus
void setupFileSystem();
void getSpeed();
// setup wlan connection with sdcard credentials
void setupWlan();
// unified function for opening files depending on the SDMMC or SPI connection type
void openFile(const char *filename, const char *mode);
void uploadSensorDataToBackend();

// ESP CAM
// interface used for sdcard (false is SPI)
#define SDMMC false
// uncomment pinMode in setup() for other esp dev kit (has enough pins)


// setup GPS serial connection
void setupGPS();
void readGPSData();
void displayInfo();
void updateAllData();

#define CUSTOM_TX_GPS 17
#define CUSTOM_RX_GPS 16
#define CUSTOM_MOSI 18
#define CUSTOM_MISO 4
#define CUSTOM_SCK 15
#define CUSTOM_CS 2
#define RAM_ARR 18000 // 2000 sensorData packets (2000 * 9 count of sensor values is 18000)
// 18000 * 4 bytes for a float is 72kB of RAM, there is only 18kB left!
#define SENSOR_DATA_SIZE 9

// api endpoints
#define API_ENDPOINT "https://mqtt-dhbw-hdh-ai2024.duckdns.org:443"
#define API_APPEND_ACTIVITY "/api/sensor/data"
#define API_STOP_ACTIVITY "/api/sensor/stop-activity"

// magnet sensor
#define PIN_MAGNET 13
#define WHEEL_DIAMETER 0.6 // 26 inch wheel

// defines the task of the esp (record data every 200ms or upload data at once)
bool recordOrUpload = false; // upload from the start (for testing), has to be saved to flash in case the esp loses power

// data specific
// this is the address to where the sensor data is stored in the heap (96kB of RAM)
float *sensorData;
// the position in the current buffer
uint bufferCounter = 0;
// how many buffers are on the sdcard
uint savedBufferToSdcardCount = 0;

// filesystem
uint timeBeforeWrite, timeAfterWrite;
// the object for the sdcard file
File file;
// this is a library object that manages internal esp flash (4MB) access to store structured data in json format automatically
Preferences preferences;

// helper variables
// magnet sensor positive flank recognition (rpm)
int lastState = LOW, currentState, flankCount = 0, rpm, speed;

// only get data every 200ms
unsigned long currentTime = 0, lastReadTime200ms = 0, lastReadTime1000ms = 0, dtTo1000ms = 0, dtTo200ms = 0;
// GPS Serial
TinyGPSPlus gps;

struct gpsData
{
  double latitude;
  double longitude;
  double height;
  time_t time;
};
gpsData gpsdata;

void setup()
{
  Serial.begin(115200);
  // wait for serial monitor to connect
  delay(8000);

  setupWlan();
  setupFileSystem();
  setupGPS();

  Serial.println("Setup complete. Starting main loop...");

  sensorData = (float *)malloc(RAM_ARR * sizeof(float));
  // pinMode(2, OUTPUT);

  // digital input for rpm sensor (magnet sensor)
  // pinMode(PIN_MAGNET, INPUT); // For an input with internal pull-up resistor
}

void loop()
{
  if (recordOrUpload)
  {
    getSpeed();
    readGPSData();

    currentTime = millis();
    dtTo200ms = currentTime - lastReadTime200ms;
    if (dtTo200ms >= 200)
    {

      sensorData[bufferCounter] = 0; // temperature
      sensorData[bufferCounter + 1] = (float)speed;
      sensorData[bufferCounter + 2] = gpsdata.latitude;  // latitude
      sensorData[bufferCounter + 3] = gpsdata.longitude; // longitude
      sensorData[bufferCounter + 4] = gpsdata.height;
      sensorData[bufferCounter + 5] = 0;
      sensorData[bufferCounter + 6] = 0;
      sensorData[bufferCounter + 7] = 0;
      // create the checksum
      for (size_t i = 0; i < SENSOR_DATA_SIZE; i++)
        sensorData[bufferCounter + 8] += sensorData[i];

      bufferCounter += SENSOR_DATA_SIZE; // move the current index one sensor packet further (9 values)
      lastReadTime200ms = currentTime;
    }

    // the buffer is full and needs to be saved to the sdcard
    if (bufferCounter == 2000)
    {
      bufferCounter = 0; // reset buffer size because ram is free after save to sdcard
      writeSensorDataBlock();
      savedBufferToSdcardCount++; // one more buffer on the sdcard
    }
  }
  else
  {
    // this is for testing the upload procedure directly without waiting to collect real data first
    savedBufferToSdcardCount = 20;
    // fill sensor data for testing (2000 sensorData packets)
    for (size_t i = 0; i < RAM_ARR; i = i + 9)
    {
      sensorData[i + 0] = 10.5;
      sensorData[i + 1] = 9.5;
      sensorData[i + 2] = 9.5;
      sensorData[i + 3] = 0.5;
      sensorData[i + 4] = 10;
      sensorData[i + 5] = 0.5;
      sensorData[i + 6] = 1;
      sensorData[i + 7] = 4;
      sensorData[i + 8] = 45.5;
    }
    // write 20 times to the sdcard 40 Minutes activity in sum
    for (size_t i = 0; i < savedBufferToSdcardCount; i++)
      writeSensorDataBlock();
    uploadSensorDataToBackend();
  }

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
    if (!SD_MMC.begin("/sdcard", true, true))
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
  String ssid = "ssid";
  String pass = "pass";
  preferences.begin("credentials", false);
  preferences.putString("ssid", ssid);
  preferences.putString("pass", pass);
  Serial.println("Network credentials saved using Preferences");
  preferences.end();

  // read data from internal flash
  String ssidRead;
  String passRead;
  preferences.begin("credentials", false);
  ssidRead = preferences.getString("ssid", "");
  passRead = preferences.getString("pass", "");
  Serial.println("Network credentials read using Preferences");
  preferences.end();

  WiFi.begin(ssidRead, passRead);
  Serial.println("Connecting");
  while (WiFi.status() != WL_CONNECTED)
  {
    delay(500);
    Serial.print(".");
  }
  Serial.print("Connected to WLAN with ip adress: ");
  Serial.println(WiFi.localIP());
}

void writeSensorDataBlock()
{
  // open the file where the array data is streamed into
  openFile("/sensorData.bin", FILE_APPEND);

  timeBeforeWrite = millis();
  // write the array to the file
  if (bufferCounter == 0)
    file.write((uint8_t *)sensorData, RAM_ARR * sizeof(float)); // write a full buffer to the sdcard
  else
    file.write((uint8_t *)sensorData, bufferCounter * sizeof(float)); // write the not full buffer to the sdcard

  file.close();
  timeAfterWrite = millis();
  Serial.print("Writetime: ");
  Serial.println(timeAfterWrite - timeBeforeWrite);

  // the space of the array can now be used again
  // free(sensorData);

  Serial.println("Writing data is finished.\n");
}

void uploadSensorDataToBackend()
{
  WiFiClientSecure client;
  HTTPClient http;
  unsigned long timePoint1;

  const char *test_root_ca =
      "-----BEGIN CERTIFICATE-----\n"
      "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\n"
      "-----END CERTIFICATE-----\n";

  // sensorDataRead contains the actual data of the sdcard now
  openFile("/sensorData.bin", FILE_READ);

  for (size_t i = savedBufferToSdcardCount; i > 0; i--)
  {
    timePoint1 = millis();
    //  we want to upload the data to the backend now
    // if the last buffer was not filled fully, read only a partial buffer and upload the data
    if (bufferCounter == 0 && i == 1)
    {
      Serial.println("Read the data from sdcard to ram buffer...");
      file.read((uint8_t *)sensorData, RAM_ARR * sizeof(float));
      Serial.println("Convert float buffer to csv string...");
    }
    else
    {
      Serial.println("Read the partial data from sdcard to ram buffer...");
      file.read((uint8_t *)sensorData, bufferCounter * SENSOR_DATA_SIZE * sizeof(float));
      Serial.println("Convert partial float buffer to csv string...");
    }

    client.setCACert(test_root_ca);
    Serial.println("Starting http transmission...");
    http.begin(client, String(API_ENDPOINT) + String(API_APPEND_ACTIVITY));

    // content-type headers
    http.addHeader("Content-Type", "application/octet-stream");
    http.addHeader("Authorization", "Bearer WJVVvXO7zj861hrHUEALrLRsC+YYH6kB0iQpa6KgMweYlwgxK2ShBmO3CiRIcIaZd6kZM1TRI5hkv58jQZTT4w==");

    // convert the float array to a byte array, completely ignore the type
    uint8_t *byteData = reinterpret_cast<uint8_t *>(sensorData);

    // Send HTTP POST request with byte data
    int httpResponseCode = http.POST(byteData, RAM_ARR * sizeof(float));

    Serial.print("HTTP Response code: ");
    Serial.println(httpResponseCode);
    //  Free resources
    http.end();
    Serial.println("Upload finished.");
    // retreived a buffer from sdcard and uploaded it to backend
    savedBufferToSdcardCount--;
    Serial.print("Time for converting to csv and upload to backend: ");
    Serial.println(millis() - timePoint1);

    // get free heap
    uint32_t heapSize = ESP.getFreeHeap();
    Serial.print("Heap size: ");
    Serial.println(heapSize);

    delay(3000);
  }
  Serial.println("Starting stop-activity...");
  http.begin(client, String(API_ENDPOINT) + String(API_STOP_ACTIVITY));

  // content-type headers
  http.addHeader("Content-Type", "application/json");
  http.addHeader("Authorization", "Bearer WJVVvXO7zj861hrHUEALrLRsC+YYH6kB0iQpa6KgMweYlwgxK2ShBmO3CiRIcIaZd6kZM1TRI5hkv58jQZTT4w==");

  // Send HTTP POST request with byte data
  int httpResponseCode = http.POST("{}");

  Serial.print("HTTP Response code: ");
  Serial.println(httpResponseCode);
  //  Free resources
  http.end();
  Serial.println("Activity stopped.");

  file.close();
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

void setupGPS()
{
  Serial2.begin(9600);
  Serial.println("GPS Serial started");
}

void readGPSData()
{
  while (Serial2.available() > 0)
  {
    if (gps.encode(Serial2.read()))
    {
      displayInfo();
      Serial2.flush(); // clear the serial buffer after reading GPS data
    }
  }
}

void displayInfo()
{
  // Displaying Google Maps link with latitude and longitude information if GPS location is valid
  if (gps.location.isValid() && gps.time.isValid())
  {
    updateAllData();
  }
}

void updateAllData()
{
  gpsdata.latitude = gps.location.lat();
  gpsdata.longitude = gps.location.lng();
  gpsdata.height = trunc(gps.altitude.meters());

  int year = gps.date.year();
  int month = gps.date.month();
  int day = gps.date.day();
  int hour = gps.time.hour();
  int minute = gps.time.minute();
  int second = gps.time.second();

  struct tm timeinfo;
  timeinfo.tm_year = year - 1900; // Year since 1900
  timeinfo.tm_mon = month - 1;    // Month from 0 to 11
  timeinfo.tm_mday = day;
  timeinfo.tm_hour = hour;
  timeinfo.tm_min = minute;
  timeinfo.tm_sec = second;
  timeinfo.tm_isdst = 0; // No daylight saving time

  // Convert to epoch time
  time_t epochTime = mktime(&timeinfo);
  gpsdata.time = epochTime;
}
#include "FS.h"
#include "SD.h"
#include "SPI.h"
#include <WiFi.h>
#include "SD_MMC.h"
#include "string.h"
#include "TinyGPS++.h"
#include <Arduino.h>



// write the full array (before esp panics because of full RAM) sensorData to sdcard (every ~8 minutes, takes 220ms)
void writeSensorDataBlock();
// setup sdcard connection over SPI bus
void setupFileSystem();
void getSpeed();
// setup wlan connection with sdcard credentials
void setupWlan();

// interface used for sdcard (false is SPI)
#define SDMMC true
#define CUSTOM_MOSI 16
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
 // GPS Serial
TinyGPSPlus gps;

struct gpsData{
  double latitude;
  double longitude;
  double height;
  time_t time;
};
gpsData gpsdata;
bool hasBeenReadThisCycle = false; // flag to check if the gps data has been read this cycle


void setup()
{
  Serial.begin(115200);
  // wait for serial monitor to connect
  delay(8000);

  setupFileSystem();
  setupGPS();


  Serial.println("Setup complete. Starting main loop...");

  // reserve memory for sensor data
  sensorData = (float *)malloc(RAM_ARR * sizeof(float));
  pinMode(2, OUTPUT);
  

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
  readGPSData();
  if (bufferCount == 2000)
  {
    bufferCount = 0; // reset buffer size because ram is free after save to sdcard
    writeSensorDataBlock();
  }
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
  File file = SD_MMC.open("/credentials.txt", FILE_READ);
  if (!file)
  {
    Serial.println("File not found");
    return;
  }

  String buffer = file.readString();
  file.close();

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
  WiFi.begin(ssid, password);
  while (WiFi.status() != WL_CONNECTED)
  {
    delay(500);
    Serial.print(".");
  }
  Serial.println("");
  Serial.print("Connected to WiFi network with IP Address: ");
  Serial.println(WiFi.localIP());
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


void setupGPS(){
  Serial2.begin(9600);
  Serial.println("GPS Serial started");
}

void readGPSData() {
    while (Serial2.available() > 0) { 
      if (gps.encode(Serial2.read())) { 
        displayInfo(); 
        Serial2.flush(); // clear the serial buffer after reading GPS data
      }
  }
}

void displayInfo() { 
  // Displaying Google Maps link with latitude and longitude information if GPS location is valid 
  if (gps.location.isValid() && gps.time.isValid()) { 
    updateAllData();
  }
}

void updateAllData(){
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
#include "SPI.h"
#include "string.h"
#include <Arduino.h>

// files of this project
#include <constants.h>
#include <data.h>
#include <network.h>
// sensors
#include <sensors/rpm.h>
#include <sensors/gps.h>

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

// defines the task of the esp (record data every 200ms or upload data at once)
bool recordOrUpload = false; // upload from the start (for testing), has to be saved to flash in case the esp loses power

// only get data every 200ms
unsigned long currentTime = 0, lastReadTime200ms = 0, lastReadTime1000ms = 0, dtTo1000ms = 0, dtTo200ms = 0;

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
      writeSensorDataBlock(sensorData, file, bufferCounter);
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
    // write 20 times to the sdcard with 133 Minutes activity (20*2000/5/60=133, 20 buffers * 2000 floats / 5 packets per second / 60 seconds = 133 minutes)
    for (size_t i = 0; i < savedBufferToSdcardCount; i++)
      writeSensorDataBlock(sensorData, file, bufferCounter);
    uploadSensorDataToBackend(sensorData, file, savedBufferToSdcardCount, bufferCounter);
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
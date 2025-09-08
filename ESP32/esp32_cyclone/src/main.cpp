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
#include <sensors/accel-temp.h>

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
bool recordOrUpload = true; // upload from the start (for testing), has to be saved to flash in case the esp loses power

// only get data every 200ms
unsigned long currentTime = 0, lastReadTime200ms = 0, lastReadTime1000ms = 0, dtTo1000ms = 0, dtTo200ms = 0;

// gps related
gpsData gpsdata;
// GPS Serial
TinyGPSPlus gps;

// accelerometer and temperature
Adafruit_MPU6050 mpu;
// struct for the sensor data
sensors_event_t a, g, temp;

void setup()
{
  Serial.begin(115200);
  // wait for serial monitor to connect
  delay(8000);

  // setupWlan();
  // setupFileSystem();
  // setupGPS();
  setupMPU6050(mpu);
  setupRPM();

  Serial.println("Setup complete. Starting main loop...");

  sensorData = (float *)malloc(RAM_ARR * sizeof(float));
  // initialize the buffer
  for (size_t i = 0; i < RAM_ARR; i++)
    sensorData[i] = 0;
}

void loop()
{
  if (recordOrUpload)
  {
    //getSpeed();
    
    readGPSData(gps, gpsdata);

    currentTime = millis();
    dtTo200ms = currentTime - lastReadTime200ms;
    if (dtTo200ms >= 200)
    {
      // reset timer
      lastReadTime200ms = currentTime;
      // get data from accelerometer and temperature sensor (MPU6050)
      mpu.getEvent(&a, &g, &temp);
      sensorData[bufferCounter] = temp.temperature;
      Serial.print("temp: ");
      Serial.print(sensorData[bufferCounter]);
      sensorData[bufferCounter + 1] = calculateSpeed();
      Serial.print(" speed: ");
      Serial.print(sensorData[bufferCounter + 1]);
      sensorData[bufferCounter + 2] = gpsdata.latitude;
      Serial.print(" lat: ");
      Serial.print(sensorData[bufferCounter + 2]);
      sensorData[bufferCounter + 3] = gpsdata.longitude;
      Serial.print(" long: ");
      Serial.print(sensorData[bufferCounter + 3]);
      sensorData[bufferCounter + 4] = gpsdata.height;
      Serial.print(" NN: ");
      Serial.print(sensorData[bufferCounter + 4]);
      sensorData[bufferCounter + 5] = a.acceleration.x;
      Serial.print(" x: ");
      Serial.print(sensorData[bufferCounter + 5]);
      sensorData[bufferCounter + 6] = a.acceleration.y;
      Serial.print(" y: ");
      Serial.print(sensorData[bufferCounter + 6]);
      sensorData[bufferCounter + 7] = a.acceleration.z;
      Serial.print(" z: ");
      Serial.println(sensorData[bufferCounter + 7]);

      // create the checksum
      for (size_t i = 0; i < SENSOR_DATA_SIZE; i++)
        sensorData[bufferCounter + 8] += sensorData[i];

      bufferCounter += SENSOR_DATA_SIZE; // move the current index one sensor packet further (9 values)
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

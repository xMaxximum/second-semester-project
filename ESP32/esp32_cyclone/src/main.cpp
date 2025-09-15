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

uint opModeSwitchPrevState, opModeSwitchCurrentState;
bool uploadData = true;

void setup()
{
  Serial.begin(115200);
  // wait for serial monitor to connect
  delay(8000);
  
  setupFileSystem();
  setupGPS();
  setupMPU6050(mpu);
  setupRPM();

  Serial.println("Setup complete. Starting main loop...");

  sensorData = (float *)malloc(RAM_ARR * sizeof(float));
  // initialize the buffer
  for (size_t i = 0; i < RAM_ARR; i++)
    sensorData[i] = 0;

  // pullup because sensor pulls it down (clear signal states)
  pinMode(PIN_OP_MODE, INPUT);
  opModeSwitchPrevState = digitalRead(PIN_OP_MODE);
}

void checkState()
{
  opModeSwitchCurrentState = digitalRead(PIN_OP_MODE);
  if (opModeSwitchCurrentState != opModeSwitchPrevState)
  {
    // toggle op mode
    recordOrUpload = !recordOrUpload;
    opModeSwitchPrevState = opModeSwitchCurrentState;
  }
}

void loop()
{
  checkState();
  if (recordOrUpload)
  {
    uploadData = true; // data can always be uploaded if data got recorded, this variable is a interlock for the upload data routine that should only run
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
      Serial.print(sensorData[bufferCounter], 10);
      sensorData[bufferCounter + 1] = calculateSpeed();
      Serial.print(" speed: ");
      Serial.print(sensorData[bufferCounter + 1], 10);
      sensorData[bufferCounter + 2] = gpsdata.latitude;
      Serial.print(" lat: ");
      Serial.print(sensorData[bufferCounter + 2], 10);
      sensorData[bufferCounter + 3] = gpsdata.longitude;
      Serial.print(" long: ");
      Serial.print(sensorData[bufferCounter + 3], 10);
      sensorData[bufferCounter + 4] = gpsdata.height;
      Serial.print(" NN: ");
      Serial.print(sensorData[bufferCounter + 4], 10);
      sensorData[bufferCounter + 5] = a.acceleration.x;
      Serial.print(" x: ");
      Serial.print(sensorData[bufferCounter + 5], 10);
      sensorData[bufferCounter + 6] = a.acceleration.y;
      Serial.print(" y: ");
      Serial.print(sensorData[bufferCounter + 6], 10);
      sensorData[bufferCounter + 7] = a.acceleration.z;
      Serial.print(" z: ");
      Serial.println(sensorData[bufferCounter + 7], 10);

      // create the checksum
      for (size_t i = bufferCounter; i < bufferCounter + SENSOR_DATA_SIZE - 1; i++)
        sensorData[bufferCounter + 8] += sensorData[i];

      bufferCounter += SENSOR_DATA_SIZE; // move the current index one sensor packet further (9 values)
      Serial.print(" bufferCounter:");
      Serial.println(bufferCounter);
    }

    // the buffer is full and needs to be saved to the sdcard, this is 6,6 minutes of data
    if (bufferCounter == RAM_ARR)
    {
      bufferCounter = 0;
      // bufferCounter = 0; // reset buffer size because ram is free after save to sdcard
      writeSensorDataBlock(sensorData, file, bufferCounter);
      savedBufferToSdcardCount++; // one more buffer on the sdcard
    }
  }
  else
  {
    if (uploadData)
    {
      
      if (bufferCounter > 0) // save remaining buffer first before upload
      {
        // save remaining buffer
        writeSensorDataBlock(sensorData, file, bufferCounter);
        savedBufferToSdcardCount++; // one more buffer on the sdcard
      }

      setupWlan();
      uploadSensorDataToBackend(sensorData, file, savedBufferToSdcardCount, bufferCounter);
      SD.remove("/sensorData.bin");
      uploadData = false; // do not upload again after upload complete
      bufferCounter = 0;  // start from the beginning of the sensor buffer again with filling it with data
      savedBufferToSdcardCount = 0;
      Serial.print("Wait for the user to start a activity again");
    }
    else
    {
      for (size_t i = 0; i < 5; i++)
      {
        Serial.print(".");
        delay(1000);
      }
    }
  }
}

void setupWlan()
{
  String ssid = "60GB";
  String pass = "EXt57j6Im8SD";
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

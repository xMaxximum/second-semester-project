#include "FS.h"
#include "SD.h"
#include "SPI.h"
#include <WiFi.h>
#include "SD_MMC.h"
#include <HTTPClient.h>
#include "string.h"
#include <Preferences.h>

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
void uploadSensorDataToBackend();

// ESP CAM
// interface used for sdcard (false is SPI)
#define SDMMC true
// uncomment pinMode in setup() for other esp dev kit (has enough pins)

#define CUSTOM_MOSI 16
#define CUSTOM_MISO 4
#define CUSTOM_SCK 15
#define CUSTOM_CS 2
#define RAM_ARR 18000 // 2000 sensorData packets (2000 * 9 count of sensor values is 18000)
// 24000 * 4 bytes for a float is 96kB of RAM
#define SENSOR_DATA_SIZE 9

// api endpoint
#define API_ENDPOINT "http://192.168.132.180:5085/api/sensordata/data"

// magnet sensor
#define PIN_MAGNET 17
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

void setup()
{
  Serial.begin(115200);
  // wait for serial monitor to connect
  delay(3000);

  setupWlan();
  setupFileSystem();

  // reserve memory for sensor data
  sensorData = (float *)malloc(RAM_ARR * sizeof(float));

  // digital input for rpm sensor (magnet sensor)
  // pinMode(PIN_MAGNET, INPUT); // For an input with internal pull-up resistor  
}

void loop()
{
  if (recordOrUpload)
  {
    currentTime = millis();

    getSpeed();

    dtTo200ms = currentTime - lastReadTime200ms;
    if (dtTo200ms >= 200)
    {

      sensorData[bufferCounter] = 0; // temperature
      sensorData[bufferCounter + 1] = (float)speed;
      sensorData[bufferCounter + 2] = 0; // latitude
      sensorData[bufferCounter + 3] = 0;
      sensorData[bufferCounter + 4] = 0;
      sensorData[bufferCounter + 5] = 0;
      sensorData[bufferCounter + 6] = 0;
      sensorData[bufferCounter + 7] = 0;
      sensorData[bufferCounter + 8] = speed; // checksum is only speed right now
      bufferCounter += SENSOR_DATA_SIZE;     // move the current index one sensor packet further (9 values)
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
    // for testing
    savedBufferToSdcardCount = 1;
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
  // save data to internal flash  
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
  openFile("/sensorData.bin", FILE_WRITE);

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
  WiFiClient client;
  HTTPClient http;
  unsigned long timePoint1;
  char *buffer; // the csv containing string

  // sensorDataRead contains the actual data of the sdcard now
  openFile("/sensorData.bin", FILE_READ);  

  for (size_t i = savedBufferToSdcardCount; i > 0; i--)
  {

    timePoint1 = millis();
    //  we want to upload the data to the backend now
    // if the last buffer was not filled fully, read only a partial buffer and upload the data
    if (bufferCounter == 0 && i == 1)
    {
      file.read((uint8_t *)sensorData, bufferCounter * SENSOR_DATA_SIZE * sizeof(float));
      buffer = convertBufferToCSV(sensorData, bufferCounter * SENSOR_DATA_SIZE);
    }
    else
    {
      file.read((uint8_t *)sensorData, RAM_ARR * sizeof(float));
      buffer = convertBufferToCSV(sensorData, RAM_ARR);
    }
    
    http.begin(client, API_ENDPOINT);

    // Specify content-type header
    http.addHeader("Content-Type", "application/json");
    // Data to send with HTTP POST
    String httpRequestData = "{\"userId\":1,\"csvData\":\"" + String(buffer) + "\",\"deviceId\":\"ESP32-ABC123\"}";
    
    // Send HTTP POST request
    int httpResponseCode = http.POST(httpRequestData);

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

    delay(5000);
  }

  file.close();
}

char *convertBufferToCSV(float *sensorDataBuffer, int length)
{
  // convert the array to string csv
  char *buffer = (char *)malloc(length * 10 * sizeof(char)); // the needed bytes are calculated by the number of float values (length) that need 20 chars to be displayed at best
  buffer[0] = '\0';

  for (int i = 0; i < length; i++)
  {
    if (i % 9 == 0)
      strcat(buffer, "\\n"); // append , to buffer (append the crlf as is)
    else if (i > 0)
      strcat(buffer, ",");                    // append , to buffer
    char temp[10];                            // sufficient length is needed
    dtostrf(sensorDataBuffer[i], 6, 2, temp); // convert float to string and copy it inside temp, this function does only put a max of 6 digits and 2 decimal places
    // Serial.print("Appending to csv buffer: ");
    // Serial.println(temp);
    strcat(buffer, temp); // append converted float to string
  }
  // Serial.println(buffer);

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

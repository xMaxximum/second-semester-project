#include "FS.h"
#include "SD.h"
#include "SPI.h"
#include <WiFi.h>
#include "SD_MMC.h"
#include "string.h"
#include "TinyGPS++.h"
#include <Arduino.h>
#include <EEPROM.h>



// write the full array (before esp panics because of full RAM) sensorData to sdcard (every ~8 minutes, takes 220ms)
void writeSensorDataBlock();
// setup sdcard connection over SPI bus
void setupFileSystem();
void getSpeed();
// setup wlan connection with sdcard credentials
void setupWlan();

// setup the first time configuration (if not done yet)
void checkFirstTimeConfig();
void setupSequence();
String urlDC(String input);

#define setupMode true

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
#define CUSTOM_MISO 4
#define CUSTOM_SCK 15
#define CUSTOM_CS 2
#define RAM_ARR 24000 // 2000 sensorData packets (2000 * 12 count of sensor values is 24000)
// 24000 * 4 bytes for a float is 96kB of RAM
#define SENSOR_DATA_SIZE 12

// magnet sensor
#define PIN_MAGNET 13
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

void setup()
{
  if(setupMode){
    EEPROM.begin(128);
    EEPROM.write(0, 0); // Mark setup as not done
    EEPROM.commit();
    EEPROM.end();
  }
  Serial.begin(115200);
  // wait for serial monitor to connect
  delay(8000);

  //setupFileSystem();
  checkFirstTimeConfig();
  setupWlan();
  setupGPS();


  Serial.println("Setup complete. Starting main loop...");

  sensorData = (float *)malloc(RAM_ARR * sizeof(float));
  pinMode(2, OUTPUT);
  

}

void loop()
{
  currentTime = millis();

  getSpeed();
  readGPSData();

  dtTo200ms = currentTime - lastReadTime200ms;;
  if (dtTo200ms >= 200)
  {
    bufferCount++;
    sensorData[bufferCount * SENSOR_DATA_SIZE] = 0; // temperature
    sensorData[bufferCount * SENSOR_DATA_SIZE + 1] = (float)speed;
    sensorData[bufferCount * SENSOR_DATA_SIZE + 2] = gpsdata.latitude; // latitude
    sensorData[bufferCount * SENSOR_DATA_SIZE + 3] = gpsdata.longitude; // longitude
    sensorData[bufferCount * SENSOR_DATA_SIZE + 4] = gpsdata.height;
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
  String ssid, password;
  EEPROM.begin(128);
  // read the ssid and password from the sdcard 
  ssid = EEPROM.readString(1);
  password = EEPROM.readString(64);

  password.trim();
  ssid.trim();
  
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


void checkFirstTimeConfig(){
  EEPROM.begin(128);
  
  if(EEPROM.read(0) != 1){
    setupSequence();
  }
  else{
    Serial.println("Setup already done, skipping setup sequence.");
    file.close();
  }
}

// Helper function to decode URL-encoded strings (e.g. spaces as '+', %20, etc.)
String urlDC(String input) {
  input.replace("+", " ");
  String decoded = "";
  char temp[] = "00";
  unsigned int len = input.length();
  unsigned int i = 0;
  while (i < len) {
    char c = input.charAt(i);
    if (c == '%') {
      if (i + 2 < len) {
        temp[0] = input.charAt(i + 1);
        temp[1] = input.charAt(i + 2);
        decoded += (char) strtol(temp, NULL, 16);
        i += 3;
      }
    } else {
      decoded += c;
      i++;
    }
  }
  return decoded;
}


void setupSequence() {
  WiFiServer server(80);
  String wifiSSID = "";
  String wifiPassword = "";
  String userNumber = "";
  Serial.println("Running setup sequence...");
  // Start AP

  WiFi.softAPConfig(IPAddress(192, 168, 1, 1), IPAddress(192, 168, 1, 1), IPAddress(255, 255, 255, 0));
  WiFi.softAP("CycloneSetup", "INF2024AI");
  server.begin();

  Serial.println("Access Point started: CycloneSetup / INF2024AI");

  bool setupFinished = false;
  unsigned long previousTime = millis();
  const unsigned long timeoutTime = 300000; // 5 minutes timeout

  while (!setupFinished) {
    WiFiClient client = server.available();
    if (!client) continue;

    Serial.println("New client connected");
    String request = "";
    unsigned long currentTime = millis();
    previousTime = currentTime;

    while (client.connected() && (millis() - previousTime <= timeoutTime)) {
      if (client.available()) {
        char c = client.read();
        request += c;

        if (c == '\n') {
          // End of headers
          if (request.endsWith("\r\n\r\n")) {
            break;
          }
        }
      }
    }

    // Check for POST data
    if (request.indexOf("POST") >= 0) {
      // Extract body
      int bodyIndex = request.indexOf("\r\n\r\n");
      if (bodyIndex >= 0) {
        String body = request.substring(bodyIndex + 4);

        // Expecting form data like: ssid=MyWiFi&password=abc123&number=42
        int ssidIndex = body.indexOf("ssid=");
        int passIndex = body.indexOf("password=");
        int numIndex  = body.indexOf("number=");
        
        if (ssidIndex >= 0 && passIndex >= 0 && numIndex >= 0) {
          wifiSSID = urlDC(body.substring(ssidIndex + 5, body.indexOf("&", ssidIndex)));
          wifiPassword = urlDC(body.substring(passIndex + 9, body.indexOf("&", passIndex)));
          userNumber = body.substring(numIndex + 7);

          Serial.println("Received configuration:");
          Serial.println("SSID: " + wifiSSID);
          Serial.println("Password: " + wifiPassword);
          Serial.println("Number: " + userNumber);

          setupFinished = true;
        }
      }
    }

    // Serve HTML page
    client.println("HTTP/1.1 200 OK");
    client.println("Content-type:text/html");
    client.println("Connection: close");
    client.println();

    file = SD.open("/website.html", FILE_READ);
    if (!file) {
      Serial.println("Error opening website.html");
      client.println("<!DOCTYPE html><html><body><h1>Error</h1><p>Failed to open website.html</p> <p>SDCard not found or file defective</p></body></html>");
    } else {
      while (file.available()) {
        client.write(file.read());
      }
      file.close();
    }

    client.stop();
    Serial.println("Client disconnected");
  }

  // Save setup state to EEPROM
  EEPROM.write(0, 1);
  EEPROM.put(1, wifiSSID);
  EEPROM.put(64, wifiPassword); 
  EEPROM.commit();
  EEPROM.end();
  
  // Stop the AP
  WiFi.softAPdisconnect(true);
  WiFi.disconnect();

  Serial.println("Setup complete. Stored credentials:");
  Serial.println("SSID: " + wifiSSID);
  Serial.println("Password: " + wifiPassword);
  Serial.println("Number: " + String(userNumber));
  Serial.println("You can now connect the ESP to your WiFi network.");
}

// Helper function to decode URL-encoded strings (e.g. spaces as '+', %20, etc.)
String urlDecode(String input) {
  input.replace("+", " ");
  String decoded = "";
  char temp[] = "00";
  unsigned int len = input.length();
  unsigned int i = 0;
  while (i < len) {
    char c = input.charAt(i);
    if (c == '%') {
      if (i + 2 < len) {
        temp[0] = input.charAt(i + 1);
        temp[1] = input.charAt(i + 2);
        decoded += (char) strtol(temp, NULL, 16);
        i += 3;
      }
    } else {
      decoded += c;
      i++;
    }
  }
  return decoded;
}
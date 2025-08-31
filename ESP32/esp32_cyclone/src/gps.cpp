#include <sensors/gps.h>


void setupGPS()
{
  Serial2.begin(9600);
  Serial.println("GPS Serial started");
}

void readGPSData(TinyGPSPlus &gps, gpsData &gpsdata)
{
  while (Serial2.available() > 0)
  {
    if (gps.encode(Serial2.read()))
    {
      displayInfo(gps, gpsdata);
      Serial2.flush(); // clear the serial buffer after reading GPS data
    }
  }
}

void displayInfo(TinyGPSPlus &gps, gpsData &gpsdata)
{
  // Displaying Google Maps link with latitude and longitude information if GPS location is valid
  if (gps.location.isValid() && gps.time.isValid())
  {
    updateAllData(gps, gpsdata);
  }
}

void updateAllData(TinyGPSPlus &gps, gpsData &gpsdata)
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
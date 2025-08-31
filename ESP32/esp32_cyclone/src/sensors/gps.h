#ifndef GPS_H
#define GPS_H

#include "TinyGPS++.h"

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

// setup GPS serial connection
void setupGPS();
void readGPSData();
void displayInfo();
void updateAllData();

#endif
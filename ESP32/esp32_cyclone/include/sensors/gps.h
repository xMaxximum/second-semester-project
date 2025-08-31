#ifndef GPS_H
#define GPS_H

#include "TinyGPS++.h"

struct gpsData
{
  double latitude;
  double longitude;
  double height;
  time_t time;
};

// setup GPS serial connection
void setupGPS();
void readGPSData(TinyGPSPlus &gps, gpsData &gpsdata);
void displayInfo(TinyGPSPlus &gps, gpsData &gpsdata);
void updateAllData(TinyGPSPlus &gps, gpsData &gpsdata);

#endif
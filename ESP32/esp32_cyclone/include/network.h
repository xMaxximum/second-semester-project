#ifndef NETWORK_H
#define NETWORK_H

#include <WiFi.h>
#include <HTTPClient.h>
#include <data.h>

// setup wlan connection with sdcard credentials
void setupWlan();
// upload the accumulated sensor data from the sdcard to the backend
void uploadSensorDataToBackend(float *sensorData, File & file, uint &savedBufferToSdcardCount, int bufferCounter);

#endif

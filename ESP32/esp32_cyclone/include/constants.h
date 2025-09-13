#pragma once

#define CUSTOM_SCL_MPU 22
#define CUSTOM_SDA_MPU 23
#define CUSTOM_TX_GPS 17
#define CUSTOM_RX_GPS 16
#define CUSTOM_MOSI 25
#define CUSTOM_MISO 26
#define CUSTOM_SCK 33
#define CUSTOM_CS 32
#define RAM_ARR 3600 // 2000 sensorData packets (2000 * 9 count of sensor values is 18000)
// 18000 * 4 bytes for a float is 72kB of RAM, there is only 18kB left!
#define SENSOR_DATA_SIZE 9

// api endpoints
#define API_ENDPOINT "https://mqtt-dhbw-hdh-ai2024.duckdns.org:443"
#define API_APPEND_ACTIVITY "/api/sensor/data"
#define API_STOP_ACTIVITY "/api/sensor/stop-activity"

// magnet sensor
#define PIN_MAGNET 21
#define WHEEL_DIAMETER 0.6 // 26 inch wheel

#define PIN_OP_MODE 15
    
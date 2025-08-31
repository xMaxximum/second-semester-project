#pragma once

#include "FS.h"
#include "SD.h"
#include <Preferences.h>

// write the full array (before esp panics because of full RAM) sensorData to sdcard (every ~8 minutes, takes 220ms)
void writeSensorDataBlock(float * sensorData, File &file, int bufferCounter);
// setup sdcard connection over SPI bus
void setupFileSystem();
// unified function for opening files depending on the SDMMC or SPI connection type
void openFile(const char *filename, const char *mode, File &file);

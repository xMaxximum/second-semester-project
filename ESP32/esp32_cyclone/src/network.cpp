#include <network.h>
#include <constants.h>
#include <data.h>

void uploadSensorDataToBackend(float *sensorData, File &file, uint &savedBufferToSdcardCount, int bufferCounter)
{
  unsigned long timePoint1;
  uint partialBufferSize = bufferCounter * sizeof(float);
  uint bytes;

  const char *test_root_ca =
      "-----BEGIN CERTIFICATE-----\n"
      "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\n"
      "-----END CERTIFICATE-----\n";

  // sensorDataRead contains the actual data of the sdcard now
  openFile("/sensorData.bin", FILE_READ, file);

  for (size_t i = savedBufferToSdcardCount; i > 0; i--)
  {
    timePoint1 = millis();
    //  we want to upload the data to the backend now
    // the last buffer is never saved fully, switch can not be pulled exactly on buffer maximum, that is super luck
    if (i != 1) // upload all the full buffers
    {
      Serial.println("savedBufferToSdcardCount: ");
      Serial.println(savedBufferToSdcardCount);
      Serial.println("Read the full data buffer from sdcard to ram buffer...");
      file.read((uint8_t *)sensorData, RAM_ARR * sizeof(float));
      bytes = RAM_ARR * sizeof(float);
    }
    else // upload the last not full buffer
    {
      Serial.println("savedBufferToSdcardCount: ");
      Serial.println(savedBufferToSdcardCount);
      Serial.println("Read the partial data that was saved after op mode changed from sdcard to ram buffer...");
      file.read((uint8_t *)sensorData, partialBufferSize);
      bytes = partialBufferSize;
    }
/*
    for (size_t i = 0; i < bufferCounter; i++)
    {
      Serial.println(sensorData[i]);
    }*/
    Serial.print("bufferCounter: ");
    Serial.println(bufferCounter);

    Serial.print("partial buffer size: ");
    Serial.println(partialBufferSize);

    // convert the float array to a byte array, completely ignore the type
    uint8_t *byteData = reinterpret_cast<uint8_t *>(sensorData);

    Serial.print("HTTP Response code: ");
    Serial.println(postBytes(byteData, bytes, test_root_ca));
    Serial.println("Upload finished.");
    // retreived a buffer from sdcard and uploaded it to backend
    savedBufferToSdcardCount--;
    Serial.print("Time for upload to backend: ");
    Serial.println(millis() - timePoint1);

    // get free heap
    uint32_t heapSize = ESP.getFreeHeap();
    Serial.print("Heap size: ");
    Serial.println(heapSize);

    delay(1000);
  }

  sendStop(test_root_ca);
  file.close();
}

void printFloats(const float *fdata, size_t count) {
  for (size_t i = 0; i < count; ++i) {
    Serial.println(fdata[i], 6); // 6 decimal places
    Serial.println(i);
  }
}

int postBytes(uint8_t *data, size_t bytes, const char *ca)
{
  WiFiClientSecure client;
  client.setCACert(ca);

  HTTPClient http;
  const String url = String(API_ENDPOINT) + String(API_APPEND_ACTIVITY);
  if (!http.begin(client, url))
    return -1;

  http.addHeader("Content-Type", "application/octet-stream");
  http.addHeader("Authorization", "Bearer x50sPbCyrXc0JCL7G/ftX4LLNb907Gx5GvcAjJsb9cWp20/RRA5nTIzgsHlqC4+JhSLZLjbIMO9ooI0e59GxIw==");

  Serial.println("bytearray: ");
  //printFloats((float*)data, bytes / 4);
  Serial.print("bytes to send: ");
  Serial.println(bytes);
  int code = http.sendRequest("POST", data, bytes);
  Serial.println(http.getString());
  http.end();
  client.stop();
  delay(1000);
  return code;
}

void sendStop(const char *ca)
{
  WiFiClientSecure client;
  client.setCACert(ca);
  HTTPClient http;
  Serial.println("Starting stop-activity...");
  http.begin(client, String(API_ENDPOINT) + String(API_STOP_ACTIVITY));

  // content-type headers
  http.addHeader("Content-Type", "application/json");
  http.addHeader("Authorization", "Bearer x50sPbCyrXc0JCL7G/ftX4LLNb907Gx5GvcAjJsb9cWp20/RRA5nTIzgsHlqC4+JhSLZLjbIMO9ooI0e59GxIw==");

  int httpResponseCode = http.POST("{}");

  Serial.print("HTTP Response code: ");
  Serial.println(httpResponseCode);
  //  Free resources
  http.end();
  Serial.println("Activity stopped.");
}

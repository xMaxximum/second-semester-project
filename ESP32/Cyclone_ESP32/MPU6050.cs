using System;
using System.Device.I2c;
using System.Diagnostics;
using System.Threading;
using Iot.Device.Imu;
using Iot.Device.Magnetometer;
using nanoFramework.Hardware.Esp32;

namespace Cyclone_ESP32
{
    internal class MPU6050
    {

        public MPU6050()
        {
            // when connecting to an ESP32 device, need to configure the I2C GPIOs
            // used for the bus
            // GPIO 21 is the default SDA pin, same for GPIO 22 as SCL pin
            //Configuration.SetPinFunction(21, DeviceFunction.I2C1_DATA);
            //Configuration.SetPinFunction(22, DeviceFunction.I2C1_CLOCK);
            MainTest();
        }
        void MainTest()
        {
            I2cConnectionSettings mpui2CConnectionSettingmpus = new(1, Mpu6050.DefaultI2cAddress);
            using Mpu6050 mpu6050 = new Mpu6050(I2cDevice.Create(mpui2CConnectionSettingmpus));

            mpu6050.CalibrateGyroscopeAccelerometer();
            Debug.WriteLine("Calibration results:");
            Debug.WriteLine($"Gyro X bias = {mpu6050.GyroscopeBias.X}");
            Debug.WriteLine($"Gyro Y bias = {mpu6050.GyroscopeBias.Y}");
            Debug.WriteLine($"Gyro Z bias = {mpu6050.GyroscopeBias.Z}");
            Debug.WriteLine($"Acc X bias = {mpu6050.AccelerometerBias.X}");
            Debug.WriteLine($"Acc Y bias = {mpu6050.AccelerometerBias.Y}");
            Debug.WriteLine($"Acc Z bias = {mpu6050.AccelerometerBias.Z}");

            mpu6050.GyroscopeBandwidth = GyroscopeBandwidth.Bandwidth0250Hz;
            mpu6050.AccelerometerBandwidth = AccelerometerBandwidth.Bandwidth0460Hz;

            Debug.WriteLine("This will read 200 positions in a row");
            for (int i = 0; i < 200; i++)
            {
                var gyro = mpu6050.GetGyroscopeReading();
                Debug.WriteLine($"Gyro X = {gyro.X,15}");
                Debug.WriteLine($"Gyro Y = {gyro.Y,15}");
                Debug.WriteLine($"Gyro Z = {gyro.Z,15}");
                var acc = mpu6050.GetAccelerometer();
                Debug.WriteLine($"Acc X = {acc.X,15}");
                Debug.WriteLine($"Acc Y = {acc.Y,15}");
                Debug.WriteLine($"Acc Z = {acc.Z,15}");
                Debug.WriteLine($"Temp = {mpu6050.GetTemperature().DegreesCelsius.ToString("0.00")} °C");

                Thread.Sleep(100);
            }

            // SetWakeOnMotion
            mpu6050.SetWakeOnMotion(300, AccelerometerLowPowerFrequency.Frequency0Dot24Hz);
            // You'll need to attach the INT pin to a GPIO and read the level. Once going up, you have
            // some data and the sensor is awake
            // In order to simulate this without a GPIO pin, you will see that the refresh rate is very low
            // Setup here at 0.24Hz which means, about every 4 seconds

            Debug.WriteLine("This will read 10 positions in a row");
            for (int i = 0; i < 10; i++)
            {
                var acc = mpu6050.GetAccelerometer();
                Debug.WriteLine($"Acc X = {acc.X,15}");
                Debug.WriteLine($"Acc Y = {acc.Y,15}");
                Debug.WriteLine($"Acc Z = {acc.Z,15}");
                Thread.Sleep(100);
            }
        }
    }
}

# install the flash tool (.NET SDK 8 recommended, on linux configure apt sources as stated in .NET wiki to install dotnet)
    dotnet tool install -g nanoff


## flashing esp (nanoFramework)
    nanoff --platform esp32 --serialport /dev/ttyUSB0 --update -v diag --baud 115200

## build prerequisites
- nanoFramework extension for .NET runs the commands for build and deploy (deploy did fail with a access error so I had to do it with manually nanoff command anyway)

some packages (add apt repository from the official mono site (mono-complete)):
```
sudo apt install mono-roslyn mono-complete msbuild
```


Open the bashrc:
```
nano ~/.bashrc
```

Add some alias for nuget (download the exe from microsoft):
```bash
nuget() {
  if [ "$1" = "restore" ]; then
    mono /home/sebi/Dokumente/dhbw/_CycleCC/second-semester-project/ESP32/nuget.exe restore
  else
    echo "Unsupported NuGet command"
  fi
}
```

## build the solution
### install all the nuget packages
Ctrl +Shift + P and nanoFramework: Build
Select solution after that.

## deploy (flash program code)
```
nanoff --target ESP32_PSRAM_REV0 --serialport /dev/ttyUSB0 --deploy --image ./Blinky/bin/Debug/Blinky.bin --baud 115200
```

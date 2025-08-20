# 🚴‍♂️ Cyclone - Cycling Data Tracking & Analytics Platform

Cyclone is a comprehensive cycling data tracking and analytics platform that combines embedded hardware with modern web technologies to provide real-time cycling metrics, route tracking, and performance analytics for cyclists of all levels.

## 🌟 Features

- **🛰️ Live GPS Tracking**: Real-time location tracking using ESP32-based hardware
- **📊 Performance Analytics**: Speed, distance, elevation, and performance metrics
- **📈 AI-Driven Insights**: Advanced analytics with charts and detailed reports
- **👥 Community Features**: Challenges, route sharing, badges, and competitions
- **📱 Cross-Platform**: Web-based interface accessible on any device
- **🔄 Real-Time Updates**: Live data streaming from embedded devices
- **☁️ Weather Integration**: Weather data integration for enhanced tracking

## 🏗️ Architecture

Cyclone consists of three main components:

### 1. **ESP32 Embedded System** (`/ESP32/`)
- **Hardware**: ESP32-based GPS tracking device
- **Framework**: nanoFramework (C# for embedded systems)
- **Communication**: Serial communication with MQTT support
- **Sensors**: GPS, accelerometer, temperature sensors

### 2. **Web Application** (`/Server/`)
- **Backend**: ASP.NET Core Web API
- **Frontend**: Blazor WebAssembly
- **Database**: Entity Framework with SQLite/SQL Server
- **Authentication**: JWT-based authentication
- **Real-time**: SignalR for live data updates

### 3. **Test Data & Tools** (`/Testdata/`)
- Python-based test data generator
- Sample cycling data for development and testing

## 🛠️ Technology Stack

### Embedded System
- **nanoFramework** - C# for ESP32 development
- **ESP32** - Microcontroller with WiFi/Bluetooth
- **GPS Module** - Location tracking
- **MQTT** - IoT communication protocol

### Backend
- **ASP.NET Core 9.0** - Web API framework
- **Entity Framework Core** - ORM for database operations
- **SignalR** - Real-time web communication
- **JWT Authentication** - Secure user authentication
- **SQLite/SQL Server** - Database

### Frontend  
- **Blazor WebAssembly** - C# web framework
- **MudBlazor** - Material Design component library
- **Authentication** - Integrated auth system

### DevOps & Deployment
- **Docker** - Containerization
- **Caddy** - Reverse proxy and HTTPS
- **Docker Compose** - Multi-container orchestration

## 🚀 Quick Start (Production)

> ⚠️ **Note**: The ESP32 embedded system requires separate setup (see [ESP32 Setup](#esp32-setup) below)

### Prerequisites
- [Docker](https://www.docker.com/get-started) installed
- [Git](https://git-scm.com/) installed

### Running the Web Application

1. **Clone the repository**
   ```bash
   git clone https://github.com/xMaxximum/second-semester-project.git
   cd second-semester-project
   ```

2. **Navigate to Server directory**
   ```bash
   cd Server
   ```

3. **Start the application**
   ```bash
   docker compose up
   ```

4. **Access the application**
   - Open your browser and visit: http://localhost:8080
   - The application includes both backend API and frontend

5. **Stop the application**
   ```bash
   docker compose down
   ```

## 💻 Development Setup

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/)
- [Docker](https://www.docker.com/get-started) (optional, for production-like environment)

### Backend Development

1. **Navigate to Server directory**
   ```bash
   cd Server
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Run the backend**
   ```bash
   cd Server
   dotnet run
   ```
   The API will be available at `https://localhost:7227` and `http://localhost:5227`

### Frontend Development

1. **Navigate to Frontend.Client directory**
   ```bash
   cd Server/Frontend.Client
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Run the frontend**
   ```bash
   dotnet run
   ```
   The frontend will be available at `https://localhost:7067`

### Full Solution

1. **Open in Visual Studio**
   ```bash
   cd Server
   start Server.sln  # Windows
   # or
   open Server.sln   # macOS
   ```

2. **Set multiple startup projects**
   - Right-click solution → Properties → Multiple startup projects
   - Set both `Server` and `Frontend.Client` to "Start"

3. **Press F5 to run**

## 🔧 ESP32 Setup

The ESP32 component provides the embedded GPS tracking functionality.

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- ESP32 development board
- USB cable for programming
- [nanoFramework](https://www.nanoframework.net/) development environment

### Setting up nanoFramework

1. **Install nanoff tool**
   ```bash
   dotnet tool install -g nanoff
   ```

2. **Flash nanoFramework to ESP32**
   ```bash
   nanoff --platform esp32 --serialport /dev/ttyUSB0 --update -v diag --baud 115200
   # On Windows, use COM port like COM3 instead of /dev/ttyUSB0
   ```

### Building and Deploying ESP32 Code

1. **Navigate to ESP32 directory**
   ```bash
   cd ESP32/Cyclone_ESP32
   ```

2. **Open in Visual Studio**
   ```bash
   start Cyclone_ESP32.sln  # Windows
   ```

3. **Install nanoFramework extension** in Visual Studio

4. **Build the solution**
   - Use Ctrl+Shift+P → "nanoFramework: Build"
   - Select the solution

5. **Deploy to ESP32**
   ```bash
   nanoff --target ESP32_PSRAM_REV0 --serialport /dev/ttyUSB0 --deploy --image ./bin/Debug/Cyclone_ESP32.bin --baud 115200
   ```

### ESP32 Configuration

When working with ESP32, edit the `.nfproj` file and add the build constant:

```xml
<DefineConstants>$(DefineConstants);BUILD_FOR_ESP32;</DefineConstants>
```

## 📁 Project Structure

```
second-semester-project/
├── ESP32/                          # Embedded system code
│   ├── Cyclone_ESP32/              # Main ESP32 project
│   ├── SerialCommunication/        # Communication examples
│   ├── Blinky/                     # Basic LED example
│   └── setup-nanoFramework.md      # ESP32 setup guide
├── Server/                         # Web application
│   ├── Server/                     # ASP.NET Core backend
│   ├── Frontend.Client/            # Blazor WebAssembly frontend
│   ├── Shared/                     # Shared models and DTOs
│   ├── docker-compose.yml          # Production deployment
│   └── Dockerfile                  # Container configuration
├── Testdata/                       # Test data and tools
│   ├── generateTestdata.py         # Test data generator
│   └── testdata.json              # Sample cycling data
└── README.md                       # This file
```

## 🌐 API Documentation

The backend provides a REST API for all cycling data operations:

- **Authentication**: `/api/auth/*` - User registration, login, logout
- **Activities**: `/api/activities/*` - Cycling activity data
- **Users**: `/api/users/*` - User management
- **Real-time**: SignalR hub at `/dataHub` - Live data streaming

API documentation is available at `/swagger` when running in development mode.

## 🗄️ Database

The application uses Entity Framework Core with:
- **Development**: SQLite database (Server/Data/app.db)
- **Production**: Configurable via connection strings

### Database Migrations

```bash
cd Server/Server
dotnet ef migrations add MigrationName
dotnet ef database update
```

## 🧪 Testing

### Generating Test Data

The repository includes a Python script to generate realistic cycling activity data for development and testing:

```bash
cd Testdata
python3 generateTestdata.py
```

**Requirements**: Python 3.6+ (uses only standard library modules)

This creates a `testdata.json` file with simulated cycling activities including:
- GPS coordinates and routes
- Speed and distance data  
- Temperature readings
- Acceleration data
- Timestamps and duration

## 🐳 Docker Deployment

### Development
```bash
cd Server
docker compose up
```

### Production
```bash
cd Server
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

The production setup includes:
- Caddy reverse proxy with automatic HTTPS
- Volume persistence for data
- Optimized container configurations

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Guidelines
- Follow C# coding conventions
- Add unit tests for new features
- Update documentation for API changes
- Test embedded code on actual hardware when possible

## 📄 License

This project is a non-commercial educational project developed by students at DHBW Heidenheim for learning purposes.

## 👨‍💻 Contact

**Leon Scharf**  
Email: [Scharfl.tin24@student.dhbw-heidenheim.de](mailto:Scharfl.tin24@student.dhbw-heidenheim.de)

**Institution**: DHBW Heidenheim  
**Project Type**: Second Semester Student Project

---

*🚴‍♂️ Happy cycling with Cyclone! Track every turn, analyze every ride.*

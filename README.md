# 🚴‍♂️ Cyclone - Cycling Data Tracking & Analytics Platform

Cyclone is a comprehensive cycling data tracking and analytics platform that combines embedded hardware with modern web technologies to provide real-time cycling metrics, route tracking, and performance analytics for cyclists of all levels.

## 🏗️ Architecture

Cyclone consists of three main components:

### 1. **ESP32 Embedded System** (`/ESP32/`)
- **Hardware**: ESP32-based, battery controller, sensors and sdcard reader
- **Framework**: C++
- **Communication**: REST API data upload and device registration
- **Sensors**: GPS, accelerometer, temperature and magnet (for rpm) sensors

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
- **FreeRTOS** - real-time operating system that runs the C++ program
- **ESP32** - Microcontroller with WiFi/Bluetooth

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

> The ESP is flashed with PlatformIO vscode extension, open folder ESP32/esp32_cyclone and press flash button

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

### Full Solution

**Open in Visual Studio**
```bash
cd Server
dotnet run
```
The frontend will be available at `https://localhost:5058`



## 📁 Project Structure

```
second-semester-project/
├── ESP32/                          # Embedded system code
│   ├── Cyclone_ESP32/              
│   ├── SerialCommunication/        
│   ├── Blinky/                     
│   └── setup-nanoFramework.md      
├── Server/                         # Web application
│   ├── Server/                     # ASP.NET Core backend
│   ├── Frontend.Client/            # Blazor WebAssembly frontend
│   ├── Shared/                     # Shared models and DTOs
│   ├── docker-compose.yml          # Production deployment
│   └── Dockerfile                  # Container configuration
├── Testdata/                       
│   ├── generateTestdata.py         
│   └── testdata.json              
└── README.md                       # This file
```

## 🌐 API Documentation

The backend provides a REST API for all cycling data operations:

- **Authentication**: `/api/auth/*` - User registration, login, logout
- **Activities**: `/api/activities/*` - Cycling activity data
- **Users**: `/api/users/*` - User management

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

## 📄 License

This project is a non-commercial educational project developed by students at DHBW Heidenheim for learning purposes.

## 👨‍💻 Contact

**Institution**: DHBW Heidenheim  
**Project Type**: Second Semester Student Project

---

*🚴‍♂️ Happy cycling with Cyclone! Track every turn, analyze every ride.*

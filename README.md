# 🚴‍♂️ Cyclone - Cycling Data Tracking & Analytics Platform

Cyclone is a comprehensive cycling data tracking and analytics platform that combines embedded hardware with modern web technologies to provide real-time cycling metrics, route tracking, and performance analytics for cyclists of all levels.

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

> ⚠️ **Note**: The ESP (Embedded System Project) part is currently not included in this setup.

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

**Leon Scharf**  
Email: [Scharfl.tin24@student.dhbw-heidenheim.de](mailto:Scharfl.tin24@student.dhbw-heidenheim.de)

**Institution**: DHBW Heidenheim  
**Project Type**: Second Semester Student Project

---

*🚴‍♂️ Happy cycling with Cyclone! Track every turn, analyze every ride.*

@echo off
REM Quick Start Script for Damn Vulnerable Web API (Windows)
REM WARNING: FOR EDUCATIONAL USE ONLY IN ISOLATED ENVIRONMENTS

echo.
echo ======================================================================
echo    Damn Vulnerable Web API - Quick Start (Windows)
echo ======================================================================
echo.
echo WARNING: This application contains intentional security vulnerabilities!
echo Use only in isolated Docker environments for security training.
echo.

REM Check if Docker is installed
docker --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: Docker is not installed. Please install Docker Desktop first.
    echo        Visit: https://docs.docker.com/desktop/install/windows-install/
    pause
    exit /b 1
)

echo [OK] Docker is installed
echo.

REM Stop and remove existing containers
echo Cleaning up existing containers...
docker-compose down -v 2>nul

REM Build and start containers
echo.
echo Building and starting containers...
docker-compose up --build -d

if %errorlevel% equ 0 (
    echo.
 echo ======================================================================
    echo    Vulnerable API is running!
    echo ======================================================================
    echo.
    echo Access Points:
    echo   API Base URL:http://localhost:5000
    echo   Swagger UI:  http://localhost:5000
    echo   Database (Adminer): http://localhost:8080
    echo     - Server: postgres
  echo     - Username: postgres
    echo     - Password: postgres123
    echo     - Database: vulnerabledb
    echo.
    echo Default Test Accounts:
    echo   Admin:  username=admin, password=admin123
    echo   User 1: username=john,  password=12345
    echo   User 2: username=jane,  password=password
    echo.
    echo Quick Test Commands:
    echo.
    echo 1. Test Authentication (Weak Password):
    echo    curl -X POST http://localhost:5000/api/auth/register ^
    echo   -H "Content-Type: application/json" ^
    echo   -d "{\"username\":\"hacker\",\"password\":\"1\",\"email\":\"h@test.com\",\"fullName\":\"Hacker\"}"
    echo.
    echo 2. Login:
    echo    curl -X POST http://localhost:5000/api/auth/login ^
    echo      -H "Content-Type: application/json" ^
    echo      -d "{\"username\":\"john\",\"password\":\"12345\"}"
    echo.
    echo 3. Test BOLA/IDOR:
    echo    curl -H "Authorization: Bearer YOUR_TOKEN" http://localhost:5000/api/user/1
    echo.
    echo 4. Test SQL Injection:
    echo    curl "http://localhost:5000/api/product/search?keyword=' OR '1'='1"
    echo.
    echo 5. Test SSRF:
    echo    curl "http://localhost:5000/api/utility/fetch-image?url=http://localhost:5000/api/user/1"
    echo.
    echo View Logs:
    echo    docker logs vulnerable-api
    echo.
    echo Stop All Services:
    echo    docker-compose down
    echo.
    echo For detailed documentation, see README.md and VULNERABILITY_MATRIX.md
    echo.
    echo ======================================================================
) else (
    echo.
    echo ERROR: Failed to start containers. Check Docker logs for details.
    pause
    exit /b 1
)

pause

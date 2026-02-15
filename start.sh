#!/bin/bash
# Quick Start Script for Damn Vulnerable Web API
# ?? FOR EDUCATIONAL USE ONLY IN ISOLATED ENVIRONMENTS

echo "?? Damn Vulnerable Web API - Quick Start"
echo "=========================================="
echo ""
echo "??  WARNING: This application contains intentional security vulnerabilities!"
echo "    Use only in isolated Docker environments for security training."
echo ""

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo "? Docker is not installed. Please install Docker first."
    echo "   Visit: https://docs.docker.com/get-docker/"
    exit 1
fi

# Check if Docker Compose is installed
if ! command -v docker-compose &> /dev/null && ! docker compose version &> /dev/null; then
    echo "? Docker Compose is not installed. Please install Docker Compose first."
    exit 1
fi

echo "? Docker is installed"
echo ""

# Stop and remove existing containers
echo "?? Cleaning up existing containers..."
docker-compose down -v 2>/dev/null

# Build and start containers
echo ""
echo "???  Building containers..."
if docker compose version &> /dev/null; then
    docker compose up --build -d
else
    docker-compose up --build -d
fi

if [ $? -eq 0 ]; then
    echo ""
    echo "? Vulnerable API is running!"
 echo ""
echo "?? Access Points:"
    echo " ?? API Base URL:        http://localhost:5000"
    echo "   ?? Swagger UI:   http://localhost:5000"
    echo "   ???  Database (Adminer):  http://localhost:8080"
    echo "   - Server: postgres"
    echo "      - Username: postgres"
  echo "   - Password: postgres123"
    echo "      - Database: vulnerabledb"
  echo ""
    echo "?? Default Test Accounts:"
    echo "   Admin:  username=admin,  password=admin123"
    echo "   User 1: username=john,   password=12345"
    echo "   User 2: username=jane,   password=password"
    echo ""
    echo "?? Quick Test Commands:"
    echo ""
    echo "# 1. Test Authentication (Weak Password)"
    echo "curl -X POST http://localhost:5000/api/auth/register \\"
    echo "  -H 'Content-Type: application/json' \\"
    echo "  -d '{\"username\":\"hacker\",\"password\":\"1\",\"email\":\"h@test.com\",\"fullName\":\"Hacker\"}'"
    echo ""
    echo "# 2. Login"
    echo "curl -X POST http://localhost:5000/api/auth/login \\"
    echo "  -H 'Content-Type: application/json' \\"
    echo "  -d '{\"username\":\"john\",\"password\":\"12345\"}'"
    echo ""
    echo "# 3. Test BOLA/IDOR (access other user's data)"
    echo "curl -H 'Authorization: Bearer YOUR_TOKEN' http://localhost:5000/api/user/1"
    echo ""
    echo "# 4. Test SQL Injection"
    echo "curl \"http://localhost:5000/api/product/search?keyword=' OR '1'='1\""
  echo ""
    echo "# 5. Test SSRF"
    echo "curl \"http://localhost:5000/api/utility/fetch-image?url=http://localhost:5000/api/user/1\""
    echo ""
    echo "?? View Logs:"
    echo "   docker logs vulnerable-api"
    echo ""
    echo "?? Stop All Services:"
    echo "   docker-compose down"
    echo ""
    echo "?? For detailed vulnerability documentation, see:"
    echo "   - README.md"
    echo "   - VULNERABILITY_MATRIX.md"
    echo ""
else
    echo "? Failed to start containers. Check Docker logs for details."
    exit 1
fi

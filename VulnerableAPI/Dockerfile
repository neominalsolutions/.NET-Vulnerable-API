# Damn Vulnerable Web API - Dockerfile
# ?? WARNING: This container contains intentional security vulnerabilities
# DO NOT use in production environments!

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000
EXPOSE 5001

# VULNERABILITY: Running as root user (should use non-root user)
# VULNERABILITY: No security scanning or hardening

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["VulnerableAPI.csproj", "./"]
RUN dotnet restore "VulnerableAPI.csproj"

# Copy all source files
COPY . .

# Build the application
WORKDIR "/src"
RUN dotnet build "VulnerableAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "VulnerableAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# VULNERABILITY: Exposing sensitive environment variables
ENV ASPNETCORE_ENVIRONMENT=Development
ENV ASPNETCORE_URLS=http://+:5000
ENV ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=vulnerabledb;Username=admin;Password=password123"

# VULNERABILITY: No health checks configured
# VULNERABILITY: No resource limits

ENTRYPOINT ["dotnet", "VulnerableAPI.dll"]

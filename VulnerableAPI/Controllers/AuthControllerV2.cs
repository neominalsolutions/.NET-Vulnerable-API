using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using VulnerableAPI.Data;
using VulnerableAPI.Models;

namespace VulnerableAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthControllerV2 : ControllerBase
{
    private readonly VulnerableDbContext _context;
    private readonly ILogger<AuthController> _logger;
    private readonly IConfiguration _configuration;
    private readonly UserManager<AppUser> userManager;
    private readonly RoleManager<AppRole> roleManager;


    public AuthControllerV2(VulnerableDbContext context, ILogger<AuthController> logger, IConfiguration configuration, UserManager<AppUser> userManager, RoleManager<AppRole> roleManager)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        this.userManager = userManager;
        this.roleManager = roleManager;
    }

    // Register ve Login Endpointler Brute Force ataklarına karşı korumak için rate limiting yapılıdır. ?
    // Account Lockout işlemi manuel yapılacak. Çünkü SingIn Manager kullanmıyoruz. Bizim her hatalı durumda güvenli loglama ve 
    // Account Lockout işlemlerini yapmamız gerekir. 
    // Resource Based Access Control Sample. 
    // UserController.GetUserById(int id) için IDOR ataklarına karşı Id koruması sağlayacak Data Protection API kullım örneği ekleyelim.
    // UserController.GetUserById(int id) güvenlik açığında ortadan kaldırmalıyız. 
    // Sonarqube üzerinde Migration Rolsyn üzerinden sonarqube analiz edilmemesi için ne yapmalıyız ?.

    /// <summary>
    /// User registration endpoint with weak password policy
    /// </summary>
    [HttpPost("register")]
    [EnableRateLimiting("AuthEndpoints")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        // VULNERABILITY: No password strength validation (API2:2023 - Broken Authentication)
        // Allows weak passwords like "12345", "password", etc.

        // Security Logging: IP adresini al
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var timestamp = DateTime.UtcNow;

        _logger.LogInformation("[SECURITY] Registration attempt started - Username: {Username}, Email: {Email}, IP: {IpAddress}, Timestamp: {Timestamp}",
            request.Username, request.Email, ipAddress, timestamp);

        var user = await userManager.FindByNameAsync(request.Username);

        if (user != null)
        {
            _logger.LogWarning("[SECURITY] Registration failed - Username already exists - Username: {Username}, IP: {IpAddress}",
                request.Username, ipAddress);

            return BadRequest(new { message = "Username already exists" });
        }

        var entity = new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = request.Username,
            Email = request.Email,
        };

        var result = await userManager.CreateAsync(entity, request.Password);

        if (!result.Succeeded)
        {
            _logger.LogWarning("[SECURITY] Registration failed - UserId: {UserId}, Username: {Username}, IP: {IpAddress}, Errors: {Errors}",
                entity.Id, entity.UserName, ipAddress, string.Join(", ", result.Errors.Select(e => e.Code)));

            return BadRequest(new { message = "User registration failed", errors = result.Errors.Select(e => e.Description) });
        }

        _logger.LogInformation("[SECURITY] User registered successfully - UserId: {UserId}, Username: {Username}, Email: {Email}, IP: {IpAddress}",
            entity.Id, entity.UserName, entity.Email, ipAddress);

        return Ok(new { message = "User registered successfully", userId = entity.Id });
    }

    // Not: Sadece burada 12.05 de hesaba giriş yapıp yamadığımızı test edicez. 

    // Features -> enable Rate limiting + Account Locked feature ile korunuyor. Security Log eklendi.  

    /// <summary>
    /// Login endpoint with multiple vulnerabilities
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("AuthEndpoints")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // Security Logging: IP adresini al
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var timestamp = DateTime.UtcNow;

        _logger.LogInformation("[SECURITY] Login attempt started - Username: {Username}, IP: {IpAddress}, Timestamp: {Timestamp}",
            request.Username, ipAddress, timestamp);

        var user = await userManager.FindByNameAsync(request.Username);

        if (user != null)
        {
            // Önce hesabın kilitli olup olmadığını kontrol et
            if (user.LockoutEnabled && user.LockoutEnd > DateTime.UtcNow)
            {
                _logger.LogWarning("[SECURITY] Login attempt on locked account - UserId: {UserId}, Username: {Username}, IP: {IpAddress}, LockoutEnd: {LockoutEnd}",
                    user.Id, user.UserName, ipAddress, user.LockoutEnd);

                return Unauthorized(new { message = "Account is locked. Try again later." });
            }

            var result = await userManager.CheckPasswordAsync(user, request.Password); // Hash kıyaslaması yapar

            if (!result)
            {
                // Başarısız giriş denemesi
                user.AccessFailedCount += 1; // Hatalı giriş denemelerinde AccessFailedCount arttırılır.

                _logger.LogWarning("[SECURITY] Failed login attempt - UserId: {UserId}, Username: {Username}, IP: {IpAddress}, FailedAttempts: {FailedAttempts}",
                    user.Id, user.UserName, ipAddress, user.AccessFailedCount);

                if(user.AccessFailedCount >= 5) // 5 hatalı deneme sonrası hesabı kilitle
                {
                    user.LockoutEnabled = true; // Hesap kilitleme özelliği aktif edilir
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(15); // Hesap 15 dakika kilitlenir
                    await userManager.UpdateAsync(user); // Kullanıcı güncellenir

                    _logger.LogWarning("[SECURITY] Account locked due to multiple failed attempts - UserId: {UserId}, Username: {Username}, IP: {IpAddress}, LockoutEnd: {LockoutEnd}",
                        user.Id, user.UserName, ipAddress, user.LockoutEnd);

                    return Unauthorized(new { message = "Invalid credentials" });
                }
                else
                {
                    await userManager.UpdateAsync(user); // Hatalı deneme sayısı güncellenir
                    return Unauthorized(new { message = "Invalid credentials" });
                }


            }
            else
            {
                // Şifre doğru - hesap kilidini kontrol et ve gerekirse aç
                if(user.LockoutEnabled && user.LockoutEnd <= DateTime.UtcNow)
                {
                    _logger.LogInformation("[SECURITY] Account unlock after lockout period expired - UserId: {UserId}, Username: {Username}, IP: {IpAddress}",
                        user.Id, user.UserName, ipAddress);

                    user.LockoutEnabled = false;
                    user.AccessFailedCount = 0; // Hatalı deneme sayısını sıfırla
                    user.LockoutEnd = null;
                    await userManager.UpdateAsync(user); // Kullanıcı güncellenir
                }
                else if (!user.LockoutEnabled)
                {
                    // Normal başarılı giriş - failed count'u sıfırla
                    if (user.AccessFailedCount > 0)
                    {
                        user.AccessFailedCount = 0;
                        await userManager.UpdateAsync(user);
                    }
                }

                var userclaims = await userManager.GetClaimsAsync(user);

                // VULNERABILITY: Hardcoded JWT secret key (API2:2023 - Broken Authentication)
                var secretKey = "512249ca47e811669bcce502e986eefdab679635c5714c29e4bc410ccf04f5059ed9f1b73e91e85e0008820562f223e8b3e91fcc52530f9370726d47299d318d";
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
                var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha512); // Not. 256 bit yerine eğer simetrik key kullanılacak ise 512 öneriyoruz.

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.UserName),
                };

                userclaims.ToList().ForEach(uc =>
                {
                    claims.Add(new Claim(uc.Type, uc.Value));
                });

                var token = new JwtSecurityToken(
                    issuer: "VulnerableAPI",
                    audience: "VulnerableAPI",
                    claims: claims,
                    expires: DateTime.UtcNow.AddMinutes(5), // En fazla 5-15 dakikadan fazlası önerilmiyor. 365 gün verilmişti. 
                    signingCredentials: credentials
                );

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

                _logger.LogInformation("[SECURITY] Successful login - UserId: {UserId}, Username: {Username}, IP: {IpAddress}, TokenExpires: {TokenExpires}",
                    user.Id, user.UserName, ipAddress, token.ValidTo);

                return Ok(new TokenResponse
                {
                    AccessToken = tokenString,
                    RefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                });
            }
        }
        else
        {
            // Kullanıcı bulunamadı - Timing attack'a karşı dikkatli olmalıyız
            // SECURITY: Username'in tamamını loglama, sadece ilk 3 karakterini göster-> log injection saldırılarına karşı username 'i kısaltarak loglama yapıyoruz. Data masking.
            _logger.LogWarning("[SECURITY] Login attempt with non-existent username - Username: {Username}, IP: {IpAddress}",
                request.Username?.Substring(0, Math.Min(3, request.Username?.Length ?? 0)) + "***", ipAddress);

            return Unauthorized(new { message = "Invalid credentials" });
        }

    }

    [Authorize(Policy = "Only_IT_Department")]
    [HttpGet("departmentOnly")]
    public IActionResult DepartmentITOnly()
    {
        return Ok("Only Department Clain is IT see this endpoint");
    }
        

}
  
   


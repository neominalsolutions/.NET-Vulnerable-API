using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
    // UserController.TokeGetUserById(int id) için IDOR ataklarına karşı Id koruması sağlayacak Data Protection API kullım örneği ekleyelim.
    // UserController.TokeGetUserById(int id) güvenlik açığında ortadan kaldırmalıyız. 
    // Sonarqube üzerinde Migration Rolsyn üzerinden sonarqube analiz edilmemesi için ne yapmalıyız ?.

    /// <summary>
    /// User registration endpoint with weak password policy
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        // VULNERABILITY: No password strength validation (API2:2023 - Broken Authentication)
        // Allows weak passwords like "12345", "password", etc.

        var user = await userManager.FindByNameAsync(request.Username);

        if (user != null)
        {
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
            return BadRequest(new { message = "User registration failed", errors = result.Errors.Select(e => e.Description) });
        }


        return Ok(new { message = "User registered successfully", userId = entity.Id });
    }

    /// <summary>
    /// Login endpoint with multiple vulnerabilities
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        
        var user = await userManager.FindByNameAsync(request.Username);
       

        if (user != null)
        {

            var result = await userManager.CheckPasswordAsync(user, request.Password); // Hash kıyaslaması yapar
            var userclaims = await userManager.GetClaimsAsync(user);

            if (!result)
            {
                return Unauthorized(new { message = "Invalid credentials" });
            }
            else
            {
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

                return Ok(new TokenResponse
                {
                    AccessToken = tokenString,
                    RefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                });
            }
        }
        else
        {
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
  
   


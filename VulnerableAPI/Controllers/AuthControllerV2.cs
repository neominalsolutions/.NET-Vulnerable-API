using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
        // VULNERABILITY: Logging sensitive data (Logging Flaws)
        //Console.WriteLine($"Login attempt - Username: {request.Username}, Password: {request.Password}");
        //_logger.LogInformation("User login attempt: {Username} with password: {Password}", request.Username, request.Password);

        // VULNERABILITY: Plain text password comparison (API2:2023)
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.Password == request.Password);

        if (user == null)
        {
            return Unauthorized(new { message = "Invalid credentials" });
        }

        // VULNERABILITY: Hardcoded JWT secret key (API2:2023 - Broken Authentication)
        var secretKey = "512249ca47e811669bcce502e986eefdab679635c5714c29e4bc410ccf04f5059ed9f1b73e91e85e0008820562f223e8b3e91fcc52530f9370726d47299d318d";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha512); // Not. 256 bit yerine eğer simetrik key kullanılacak ise 512 öneriyoruz.
                                                                                      // SecurityAlgorithms.HmacSha256 

        var claims = new[]
        {
      new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
       new Claim(ClaimTypes.Name, user.Username),
  new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User")
        };

        // VULNERABILITY: Token with excessive expiration (API2:2023)
        // 365 days expiration is way too long
        var token = new JwtSecurityToken(
            issuer: "VulnerableAPI",
            audience: "VulnerableAPI",
       claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5), // En fazla 5-15 dakikadan fazlası önerilmiyor. 365 gün verilmişti. 
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        // VULNERABILITY: Logging the generated token (Logging Flaws)
        Console.WriteLine($"Generated JWT token for user {user.Username}: {tokenString}");
        _logger.LogInformation("JWT Token generated: {Token}", tokenString);

        return Ok(new LoginResponse
        {
            Token = tokenString,
            Username = user.Username,
            IsAdmin = user.IsAdmin
        });
    }

}
  
   


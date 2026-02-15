using Microsoft.AspNetCore.Authorization;
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
public class AuthController : ControllerBase
{
    private readonly VulnerableDbContext _context;
    private readonly ILogger<AuthController> _logger;
    private readonly IConfiguration _configuration;

  public AuthController(VulnerableDbContext context, ILogger<AuthController> logger, IConfiguration configuration)
    {
        _context = context;
   _logger = logger;
      _configuration = configuration;
    }

    /// <summary>
    /// User registration endpoint with weak password policy
    /// </summary>
[HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        // VULNERABILITY: No password strength validation (API2:2023 - Broken Authentication)
        // Allows weak passwords like "12345", "password", etc.
        
        if (await _context.Users.AnyAsync(u => u.Username == request.Username))
        {
      return BadRequest(new { message = "Username already exists" });
        }

        var user = new User
        {
    Username = request.Username,
     Email = request.Email,
   // VULNERABILITY: Storing plain text password (API2:2023)
     Password = request.Password,
       FullName = request.FullName,
            PhoneNumber = request.PhoneNumber ?? string.Empty,
  Address = request.Address ?? string.Empty,
  IsAdmin = false
 };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new { message = "User registered successfully", userId = user.Id });
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
    var secretKey = "ThisIsAHardcodedSecretKeyThatIsVeryInsecure123!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

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
            expires: DateTime.UtcNow.AddDays(365),
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

    /// <summary>
    /// Endpoint to verify token (useful for testing)
    /// </summary>
    [HttpGet("verify")]
    [Authorize]
    public IActionResult Verify()
    {
    // Debug: Check authentication status
        var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
        
        if (!isAuthenticated)
 {
     _logger.LogWarning("Verify endpoint called but user is not authenticated");
return Unauthorized(new { message = "User is not authenticated" });
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
      var role = User.FindFirst(ClaimTypes.Role)?.Value;

  _logger.LogInformation("Token verified successfully for user: {Username}", username);

        return Ok(new { userId, username, role, message = "Token is valid" });
    }

    /// <summary>
    /// Test endpoint without authorization to check if API is working
    /// </summary>
    [HttpGet("ping")]
    public IActionResult Ping()
    {
var hasAuthHeader = Request.Headers.ContainsKey("Authorization");
    var authHeader = hasAuthHeader ? Request.Headers["Authorization"].ToString() : "No Authorization header";
     
 return Ok(new 
  { 
          message = "API is working", 
 timestamp = DateTime.UtcNow,
      hasAuthHeader = hasAuthHeader,
   authHeaderPreview = hasAuthHeader ? authHeader.Substring(0, Math.Min(20, authHeader.Length)) + "..." : authHeader
    });
    }

    /// <summary>
    /// VULNERABILITY: JWT Token Manipulation Helper (Educational)
    /// This endpoint helps demonstrate JWT signature vulnerabilities
    /// 
    /// ATTACK SCENARIOS:
    /// 1. Algorithm Confusion Attack (alg: none)
    ///    - Change header algorithm to "none" and remove signature
    ///    - Modify payload (e.g., change role from "User" to "Admin")
    /// 
    /// 2. Weak Secret Key
    ///    - The secret key "ThisIsAHardcodedSecretKeyThatIsVeryInsecure123!" is hardcoded
    ///    - Attacker can brute force or guess this key
    ///    - Then create valid tokens with any claims they want
    /// 
    /// 3. Token Manipulation
    ///    - Decode existing token, modify role claim, re-sign with known secret
    /// </summary>
    [HttpPost("decode-token")]
    public IActionResult DecodeToken([FromBody] TokenDecodeRequest request)
    {
        try
 {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(request.Token);

            // VULNERABILITY: Exposing token structure and secret key hint
            var response = new
            {
       header = token.Header,
     payload = new
          {
        userId = token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value,
      username = token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value,
            role = token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value,
           issuer = token.Issuer,
      audience = token.Audiences.FirstOrDefault(),
   expiration = token.ValidTo,
                    allClaims = token.Claims.Select(c => new { c.Type, c.Value })
   },
           signature = request.Token.Split('.').LastOrDefault(),
                
      // VULNERABILITY: Giving hints about the secret key
        vulnerabilityHints = new
        {
  algorithm = token.Header.Alg,
   secretKeyHint = "The secret key is hardcoded in the source code",
       secretKeyPattern = "ThisIsA...123!",
     attackVectors = new[]
    {
        "1. Try changing 'alg' to 'none' and remove signature",
                 "2. Brute force the weak secret key",
  "3. Modify the 'role' claim from 'User' to 'Admin'",
     "4. Use tools like jwt.io or jwt_tool to manipulate the token"
     }
    }
    };

        _logger.LogWarning("Token decoded and exposed: {Token}", request.Token);
  
       return Ok(response);
        }
        catch (Exception ex)
        {
    return BadRequest(new { message = "Invalid token format", error = ex.Message });
}
    }

    /// <summary>
    /// VULNERABILITY: Token Generator with Custom Claims
    /// Allows anyone to generate tokens with arbitrary claims if they know the secret
    /// </summary>
    [HttpPost("generate-token")]
    public IActionResult GenerateCustomToken([FromBody] CustomTokenRequest request)
    {
        // VULNERABILITY: No authentication required to generate tokens
// VULNERABILITY: Allows setting IsAdmin claim arbitrarily
        
        _logger.LogWarning("Generating custom token for user: {Username} with role: {Role}", 
            request.Username, request.Role);

        var secretKey = "ThisIsAHardcodedSecretKeyThatIsVeryInsecure123!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, request.UserId.ToString()),
            new Claim(ClaimTypes.Name, request.Username),
  new Claim(ClaimTypes.Role, request.Role) // ? No validation!
        };

        var token = new JwtSecurityToken(
      issuer: "VulnerableAPI",
 audience: "VulnerableAPI",
   claims: claims,
        expires: DateTime.UtcNow.AddDays(365),
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

     Console.WriteLine($"?? SECURITY WARNING: Generated custom token with Role={request.Role}");
     Console.WriteLine($"Token: {tokenString}");

  return Ok(new
        {
            token = tokenString,
        userId = request.UserId,
            username = request.Username,
            role = request.Role,
     warning = "This endpoint should never exist in production! Anyone can generate admin tokens!"
        });
    }

    /// <summary>
    /// VULNERABILITY: Expose the secret key (for educational purposes)
    /// </summary>
    [HttpGet("secret-key")]
    public IActionResult GetSecretKey()
    {
        // VULNERABILITY: Exposing the JWT secret key
        _logger.LogCritical("?? SECRET KEY EXPOSED via API endpoint!");
        
        return Ok(new
        {
            secretKey = "ThisIsAHardcodedSecretKeyThatIsVeryInsecure123!",
         algorithm = "HS256",
    warning = "Never expose your secret key in production!",
         howToExploit = new[]
            {
      "1. Use this secret key with jwt.io to create/modify tokens",
         "2. Change any user's role from 'User' to 'Admin'",
      "3. Create tokens for any user ID",
        "4. Bypass all authorization checks"
         }
        });
    }
}

// Request models
public class TokenDecodeRequest
{
    public string Token { get; set; } = string.Empty;
}

public class CustomTokenRequest
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
  public string Role { get; set; } = "User";
}

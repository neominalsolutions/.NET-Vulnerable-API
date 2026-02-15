using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VulnerableAPI.Data;
using VulnerableAPI.Models;

namespace VulnerableAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly VulnerableDbContext _context;
    private readonly ILogger<UserController> _logger;

    public UserController(VulnerableDbContext context, ILogger<UserController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // Not: Login olduktan sonra farklı bir kullanıcının useridsine göre bilgilerini görüntüleme IDOR 

    /// <summary>
    /// VULNERABILITY: API1:2023 - Broken Object Level Authorization (BOLA/IDOR)
    /// Any authenticated user can access ANY user's details by changing the ID
    /// 
    /// ATTACK SCENARIO:
    /// 1. User 'john' (ID: 2) logs in and gets a valid JWT token
    /// 2. John can access /api/user/2 (his own profile) ✓
    /// 3. John changes URL to /api/user/1 (admin's profile)
    /// 4. John can now see admin's password, credit card, and all sensitive data! ❌
    /// 
    /// SECURE VERSION SHOULD:
    /// - Check if (currentUserId != id && !User.IsInRole("Admin")) return Forbid();
    /// - Use DTOs to exclude sensitive fields like Password, CreditCardNumber
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserById(int id)
    {
        // VULNERABILITY: No authorization check - doesn't verify if the requested user ID
        // belongs to the currently authenticated user
    // Any user can access other users' sensitive data (credit cards, addresses, etc.)
 
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
      _logger.LogInformation("User {CurrentUserId} is accessing user {RequestedUserId} details", currentUserId, id);

      // ❌ VULNERABILITY: No check like this:
        // if (currentUserId != id.ToString() && !User.IsInRole("Admin"))
        //   return Forbid(new { message = "You can only access your own profile" });

        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
       return NotFound(new { message = "User not found" });
        }

        // VULNERABILITY: Returning sensitive data without proper authorization
    // Credit card, password, address, phone number should be protected
        // Should use a DTO (Data Transfer Object) instead of returning the full User entity
        return Ok(user);
    }


  // Token Signature doğrulaması bypass etmek için JWT'nin payload kısmına "role": "Admin" ekleyerek admin yetkisi elde edilebilir.

  /// <summary>
  /// Get all users - only admins should be able to do this, but let's make it weak
  /// </summary>
  [HttpGet]
    public async Task<IActionResult> GetAllUsers()
    {
        // VULNERABILITY: Weak authorization - checking role in a way that can be bypassed
        // Better would be to use [Authorize(Roles = "Admin")] attribute
  var role = User.FindFirst(ClaimTypes.Role)?.Value;
        
        if (role != "Admin")
        {
            // Still returns some info even for non-admins
      var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var currentUser = await _context.Users.FindAsync(currentUserId);
          return Ok(new[] { currentUser });
        }

        var users = await _context.Users.ToListAsync();
        return Ok(users);
    }

    /// <summary>
    /// VULNERABILITY: API3:2023 - Broken Object Property Level Authorization (Mass Assignment)
    /// Allows users to update properties they shouldn't have access to
    /// </summary>
[HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] User updatedUser)
  {
        // VULNERABILITY: No check if the user is updating their own profile
        // VULNERABILITY: Mass Assignment - allows updating ALL properties including IsAdmin
        
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _logger.LogInformation("User {CurrentUserId} is updating user {TargetUserId}", currentUserId, id);

        var user = await _context.Users.FindAsync(id);
     
        if (user == null)
        {
         return NotFound(new { message = "User not found" });
        }

        // VULNERABILITY: Directly assigning all properties from the request
        // A regular user can set IsAdmin = true by sending it in the JSON body!
  user.Username = updatedUser.Username;
        user.Email = updatedUser.Email;
        user.Password = updatedUser.Password;
        user.FullName = updatedUser.FullName;
    user.PhoneNumber = updatedUser.PhoneNumber;
        user.Address = updatedUser.Address;
   user.CreditCardNumber = updatedUser.CreditCardNumber;
        
      // VULNERABILITY: Mass Assignment vulnerability (API3:2023)
 // Regular users can escalate privileges by setting this to true
        user.IsAdmin = updatedUser.IsAdmin;

        await _context.SaveChangesAsync();

      _logger.LogWarning("User {UserId} updated. IsAdmin is now: {IsAdmin}", id, user.IsAdmin);

        return Ok(new { message = "User updated successfully", user });
    }

    /// <summary>
    /// Delete user endpoint with IDOR vulnerability
/// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        // VULNERABILITY: Any authenticated user can delete any user account
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _logger.LogWarning("User {CurrentUserId} is attempting to delete user {TargetUserId}", currentUserId, id);

     var user = await _context.Users.FindAsync(id);
        
        if (user == null)
        {
 return NotFound(new { message = "User not found" });
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return Ok(new { message = $"User {id} deleted successfully" });
    }
}

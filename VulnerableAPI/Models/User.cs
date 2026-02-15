namespace VulnerableAPI.Models;

/// <summary>
/// User entity with intentional vulnerabilities for educational purposes
/// </summary>
public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    
    // VULNERABILITY: Storing plain text password (API2:2023 - Broken Authentication)
    public string Password { get; set; } = string.Empty;
  
    // VULNERABILITY: Mass Assignment target (API3:2023)
    // This property should NOT be modifiable by regular users
    public bool IsAdmin { get; set; }
    
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    
    // Sensitive data that should be protected
    public string? CreditCardNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

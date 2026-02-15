using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VulnerableAPI.Data;
using VulnerableAPI.Models;

namespace VulnerableAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductController : ControllerBase
{
    private readonly VulnerableDbContext _context;
    private readonly ILogger<ProductController> _logger;

    public ProductController(VulnerableDbContext context, ILogger<ProductController> logger)
    {
        _context = context;
  _logger = logger;
    }

    /// <summary>
    /// Get all products
    /// </summary>
  [HttpGet]
    public async Task<IActionResult> GetAllProducts()
    {
    var products = await _context.Products.ToListAsync();
        return Ok(products);
    }

    /// <summary>
    /// VULNERABILITY: SQL Injection via Raw SQL Query
    /// This endpoint is vulnerable to SQL injection attacks
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchProducts([FromQuery] string keyword)
    {
        if (string.IsNullOrEmpty(keyword))
        {
       return BadRequest(new { message = "Keyword is required" });
    }

    // VULNERABILITY: SQL Injection - Using string concatenation instead of parameterized queries
    // An attacker can inject SQL: ?keyword=' OR '1'='1
    // Or extract user data (PostgreSQL syntax - must match ALL Product columns): 
    // ?keyword=' UNION SELECT "Id", "Username", "Email", 0, 0,"Password", "CreatedAt" FROM "Users"--
    // Column mapping: Id, Name, Description, Price, Stock, Category, CreatedAt
    // Note: PostgreSQL table/column names are case-sensitive when quoted
    var query = $"SELECT \"Id\", \"Name\", \"Description\", \"Price\", \"Stock\", \"Category\", \"CreatedAt\" FROM \"Products\" WHERE \"Name\" LIKE '%{keyword}%' OR \"Description\" LIKE '%{keyword}%'";

        _logger.LogInformation("Executing SQL query: {Query}", query);
   Console.WriteLine($"SQL Query: {query}");

        try
        {
   var products = await _context.Products
        .FromSqlRaw(query)
           .ToListAsync();

   return Ok(products);
        }
    catch (Exception ex)
      {
// VULNERABILITY: Detailed error messages exposed (API8:2023)
     _logger.LogError(ex, "SQL query failed");
          return StatusCode(500, new { 
 message = "Database error occurred", 
     error = ex.Message,
  stackTrace = ex.StackTrace,
 query = query 
  });
    }
    }

  /// <summary>
  /// 
  /// </summary>
  /// <param name="minPrice">0</param>
  /// <param name="maxPrice">999999 OR 1=1--</param>
  /// <returns></returns>
  [HttpGet("search-by-price")]
    public async Task<IActionResult> SearchByPrice([FromQuery] string minPrice, [FromQuery] string maxPrice)
    {
     // VULNERABILITY: SQL Injection through numeric parameters
        // No validation or parameterization
 
  // VULNERABILITY: No type checking - accepts any string
        if (string.IsNullOrWhiteSpace(minPrice) || string.IsNullOrWhiteSpace(maxPrice))
      {
 return BadRequest(new { message = "Both minPrice and maxPrice are required" });
 }

        // VULNERABILITY: Direct string concatenation without sanitization
   var query = $"SELECT * FROM \"Products\" WHERE \"Price\" BETWEEN {minPrice} AND {maxPrice}";
        
        _logger.LogInformation("Price search query: {Query}", query);
 Console.WriteLine($"SQL Query: {query}");
        Console.WriteLine($"minPrice: [{minPrice}]");
        Console.WriteLine($"maxPrice: [{maxPrice}]");

        try
{
            var products = await _context.Products
       .FromSqlRaw(query)
  .ToListAsync();

       return Ok(products);
  }
        catch (Exception ex)
        {
            // VULNERABILITY: Exposing detailed error information
     return StatusCode(500, new { 
      message = "Error in price search",
        error = ex.Message,
       innerException = ex.InnerException?.Message,
         query = query,
                hint = "Try: ?minPrice=0&maxPrice=9999 UNION SELECT ... or ?minPrice=0) OR 1=1--&maxPrice=9999"
   });
        }
    }

    /// <summary>
    /// Get product by ID with SQL Injection
    /// </summary>
    [HttpGet("{id}")]
 public async Task<IActionResult> GetProductById(string id)
    {
        // VULNERABILITY: SQL Injection - accepting string parameter for ID
      // Attacker can inject: /api/product/1 OR 1=1--
        var query = $"SELECT * FROM \"Products\" WHERE \"Id\" = {id}";
        
   try
        {
            var product = await _context.Products
    .FromSqlRaw(query)
                .FirstOrDefaultAsync();

    if (product == null)
     {
     return NotFound(new { message = "Product not found" });
   }

     return Ok(product);
    }
        catch (Exception ex)
        {
   return StatusCode(500, new { 
        message = "Database error",
 error = ex.Message,
        stackTrace = ex.StackTrace
         });
        }
}

    /// <summary>
    /// Create product - Admin only (but with weak checks)
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateProduct([FromBody] Product product)
    {
      // VULNERABILITY: Not properly checking admin role
        _context.Products.Add(product);
      await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetProductById), new { id = product.Id }, product);
    }

    /// <summary>
    /// Update product
 /// </summary>
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateProduct(int id, [FromBody] Product updatedProduct)
    {
        var product = await _context.Products.FindAsync(id);
      
        if (product == null)
        {
            return NotFound();
        }

        product.Name = updatedProduct.Name;
        product.Description = updatedProduct.Description;
        product.Price = updatedProduct.Price;
  product.Stock = updatedProduct.Stock;
        product.Category = updatedProduct.Category;

        await _context.SaveChangesAsync();

        return Ok(product);
    }

    /// <summary>
    /// Delete product
    /// </summary>
    [HttpDelete("{id}")]
 [Authorize]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var product = await _context.Products.FindAsync(id);
  
        if (product == null)
        {
         return NotFound();
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Product deleted successfully" });
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace VulnerableAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UtilityController : ControllerBase
{
    private readonly ILogger<UtilityController> _logger;
 private readonly HttpClient _httpClient;

    public UtilityController(ILogger<UtilityController> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        // VULNERABILITY: Not setting timeout, allowing potential DoS
    }

    /// <summary>
    /// VULNERABILITY: API7:2023 - Server Side Request Forgery (SSRF)
    /// Fetches content from a user-provided URL without validation
    /// </summary>
 [HttpGet("fetch-image")]
    public async Task<IActionResult> FetchImage([FromQuery] string url)
 {
        if (string.IsNullOrEmpty(url))
    {
   return BadRequest(new { message = "URL parameter is required" });
     }

        // VULNERABILITY: SSRF - No URL validation or whitelist
        // Attackers can:
        // 1. Access internal network resources: http://localhost:5000/admin
        // 2. Scan internal ports: http://192.168.1.1:22
   // 3. Access cloud metadata: http://169.254.169.254/latest/meta-data/
        // 4. Read local files: file:///etc/passwd
        
      _logger.LogInformation("Fetching image from URL: {Url}", url);
Console.WriteLine($"SSRF: Attempting to fetch from: {url}");

        try
        {
      var response = await _httpClient.GetAsync(url);
   
 if (!response.IsSuccessStatusCode)
     {
       return StatusCode((int)response.StatusCode, new { 
       message = "Failed to fetch resource",
statusCode = response.StatusCode,
    url = url
      });
   }

          var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
   var content = await response.Content.ReadAsByteArrayAsync();

        // VULNERABILITY: No content type validation
        // Could return any file type, not just images
return File(content, contentType);
        }
        catch (Exception ex)
        {
       // VULNERABILITY: Detailed error messages (API8:2023)
            _logger.LogError(ex, "Error fetching URL");
            return StatusCode(500, new { 
      message = "Error fetching resource",
   error = ex.Message,
         stackTrace = ex.StackTrace,
    url = url,
       innerException = ex.InnerException?.Message
  });
        }
  }

    /// <summary>
    /// VULNERABILITY: SSRF via webhook
    /// </summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> TriggerWebhook([FromQuery] string webhookUrl, [FromBody] object data)
    {
  // VULNERABILITY: SSRF - No validation of webhook URL
        // Can be used to attack internal services or cloud metadata endpoints
        
        _logger.LogInformation("Triggering webhook: {WebhookUrl}", webhookUrl);

        try
 {
    var content = new StringContent(
           System.Text.Json.JsonSerializer.Serialize(data),
        System.Text.Encoding.UTF8,
         "application/json"
        );

       var response = await _httpClient.PostAsync(webhookUrl, content);
         var responseBody = await response.Content.ReadAsStringAsync();

            return Ok(new { 
     message = "Webhook triggered",
     statusCode = response.StatusCode,
   response = responseBody
            });
      }
        catch (Exception ex)
      {
            return StatusCode(500, new { 
        message = "Webhook failed",
        error = ex.Message,
 url = webhookUrl
     });
        }
    }



  


  /// <summary>
  /// VULNERABILITY: Open redirect
  /// </summary>
  [HttpGet("redirect")]
    public IActionResult Redirect([FromQuery] string targetUrl)
    {
  // VULNERABILITY: Open redirect - no validation
        // Can be used for phishing attacks
        
        if (string.IsNullOrEmpty(targetUrl))
      {
    return BadRequest(new { message = "targetUrl is required" });
  }

        _logger.LogInformation("Redirecting to: {TargetUrl}", targetUrl);
        
        return Redirect(targetUrl);
    }


  // Olmasý gereken senaryo: Webhook URL'leri bir whitelist'te saklanýr ve sadece bu URL'lere istek atýlmasýna izin verilir
  //[HttpPost("integrations/test-webhook")]
  //[Authorize(Roles = "Admin")]
  //public async Task<IActionResult> TestWebhook([FromBody] WebhookTestRequest request)
  //{
  //  // Admin panel'den entegrasyon test etmek için
  //  // Yine de whitelist ile korunmalý
  //  if (!_webhookWhitelist.Contains(request.Url))
  //    return BadRequest("URL not in whitelist");

  //  var result = await _httpClient.PostAsync(request.Url, ...);
  //  return Ok(new { success = true });
  //}

  /// <summary>
  /// Bazen sunucu tarafýnda istek yapma ihtiyacý olabilir, ancak bu endpoint'in kötüye kullanýlabileceði unutulmamalýdýr. Sunucu bir proxy gibi davranarak herhangi bir URL'ye istek yapabilir, bu da SSRF saldýrýlarýna yol açabilir. Bu tür bir endpoint'e eriþimi sýký bir þekilde kontrol etmek ve mümkünse tamamen kaldýrmak en iyisidir.
  /// Proxy endpoint - another SSRF vector
  /// </summary>
  [HttpGet("proxy")]
    public async Task<IActionResult> Proxy([FromQuery] string target)
    {
      // VULNERABILITY: Acts as an open proxy
        // No validation of target destination
 
        if (string.IsNullOrEmpty(target))
        {
    return BadRequest(new { message = "target parameter is required" });
        }

     try
    {
            _logger.LogInformation("Proxying request to: {Target}", target);
          
         var response = await _httpClient.GetAsync(target);
            var content = await response.Content.ReadAsStringAsync();

        return Ok(new {
         url = target,
     statusCode = response.StatusCode,
            content = content,
     headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value))
          });
        }
        catch (Exception ex)
        {
        return StatusCode(500, new {
      message = "Proxy request failed",
            error = ex.Message,
  stackTrace = ex.StackTrace,
     target = target
    });
        }
    }


  // Sunucu tarafýndan belirli bir API'ye eriþim saðlamak için proxy endpoint'i oluþturulabilir, ancak bu endpoint'in kötüye kullanýlabileceði unutulmamalýdýr. Bu tür bir endpoint'e eriþimi sýký bir þekilde kontrol etmek ve mümkünse tamamen kaldýrmak en iyisidir.

  //[HttpGet("external-api/weather")]
  //public async Task<IActionResult> GetWeather([FromQuery] string city)
  //{
  //  Client CORS nedeniyle direkt eriþemiyor
  //  Ama biz sadece SPESIFIK bir servis için proxy yapýyoruz
  //  var url = $"https://api.weather.com/v1/forecast?city={city}";
  //  var response = await _httpClient.GetAsync(url);
  //  var content = await response.Content.ReadAsStringAsync();

  //  return Content(content, "application/json");
  //}



  /// <summary>
  /// Health check endpoint that exposes too much information
  /// </summary>
  [HttpGet("health")]
    public IActionResult Health()
    {
        // VULNERABILITY: Information disclosure (API8:2023)
     // Exposing internal system information
        
        return Ok(new {
            status = "healthy",
     version = "1.0.0-VULNERABLE",
       environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
    machineName = Environment.MachineName,
            osVersion = Environment.OSVersion.ToString(),
   processorCount = Environment.ProcessorCount,
          dotnetVersion = Environment.Version.ToString(),
workingSet = Environment.WorkingSet,
          currentDirectory = Environment.CurrentDirectory,
   timestamp = DateTime.UtcNow
});
    }
}

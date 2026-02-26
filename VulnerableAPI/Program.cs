using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using VulnerableAPI.Auth;
using VulnerableAPI.Data;
using VulnerableAPI.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static System.Runtime.InteropServices.JavaScript.JSType;

var builder = WebApplication.CreateBuilder(args);



builder.Services.AddControllers();

// Add HttpClient for SSRF vulnerabilities
builder.Services.AddHttpClient();

// Swagger/OpenAPI configuration (Essential for DAST tools)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Damn Vulnerable Web API",
        Version = "v1.0",
   Description = "?? WARNING: This API contains intentional security vulnerabilities for educational purposes only. " +
           "DO NOT deploy this in production! This is designed to demonstrate OWASP API Security Top 10 vulnerabilities.",
        Contact = new OpenApiContact
  {
        Name = "Security Training Lab",
            Email = "security@training.local"
        }
    });

    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
    Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
    Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
 {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
            {
  Type = ReferenceType.SecurityScheme,
       Id = "Bearer"
  }
 },
    Array.Empty<string>()
        }
    });
});

// VULNERABILITY: API8:2023 - Security Misconfiguration
// Disable HTTPS redirection and allow all CORS origins
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        // VULNERABILITY: Allowing all origins, headers, and methods
        policy.AllowAnyOrigin()
         .AllowAnyMethod()
    .AllowAnyHeader();
    });
});

// Add PostgreSQL Database Context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Database=vulnerabledb;Username=admin;Password=password123";

builder.Services.AddDbContext<VulnerableDbContext>(options =>
    options.UseNpgsql(connectionString));

// VULNERABILITY: API2:2023 - Broken Authentication
// Hardcoded JWT secret key
var jwtSecretKey = "512249ca47e811669bcce502e986eefdab679635c5714c29e4bc410ccf04f5059ed9f1b73e91e85e0008820562f223e8b3e91fcc52530f9370726d47299d318d";

// Not: SignatureKey doğru olduğu sürece ValidateIssuerSigningKey:false olsa dahi signature key kontrolü yapıyor. 



builder.Services.AddDbContext<AppIdentityDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<AppUser, AppRole>(opt =>
{
    opt.User.RequireUniqueEmail = true;
    opt.SignIn.RequireConfirmedPhoneNumber = true;
    opt.SignIn.RequireConfirmedEmail = true;
    opt.Lockout.MaxFailedAccessAttempts = 3; // 3 hatada hesap kitlensin -> Brute Force ataklarına karşı bir önlem
    opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromSeconds(30); // 30 dk boyunca kitlesin
    opt.Password.RequireDigit = true;
    opt.Password.RequireNonAlphanumeric = true;
    opt.Password.RequiredLength = 12;
    opt.Password.RequireUppercase = true;
    opt.Password.RequireLowercase = true;
}).AddEntityFrameworkStores<AppIdentityDbContext>().AddDefaultTokenProviders();


builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true, // VULNERABILITY: Not validating token expiration
        ValidateIssuerSigningKey = true,
        ValidIssuer = "VulnerableAPI",
        ValidAudience = "VulnerableAPI",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))
    };

    // VULNERABILITY: Logging authentication details
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"Authentication failed: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine($"Token validated for user: {context.Principal?.Identity?.Name}");
            return Task.CompletedTask;
        },
        // VULNERABILITY: Not properly handling security token exceptions
        OnMessageReceived = context =>
        {
            var token = context.Token;
            if (!string.IsNullOrEmpty(token))
            {
                Console.WriteLine($"Received token: {token.Substring(0, Math.Min(20, token.Length))}...");
            }
            return Task.CompletedTask;
        }
    };
});


builder.Services.AddAuthorization(opt =>
{
    opt.AddPolicy("Only_IT_Department", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("Department", "IT");
    });
});

// Controller seviyesinde kullanılacak ise tanımlamak lazım. 

builder.Services.AddAuthorization(opt =>
{
    opt.AddPolicy("DocumentOwner", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.Requirements.Add(new DocumentRequirement());
    });
});

// DocumentRequirementHandler -> her access kontrolünde bu servis uygulama genelinde çalışabilir olacak şekilde
// IoC eklenmiş olsun. 

// Controllerda IAuthorizationService bunun içinde aşağıdaki IoC tanımı yapılmalıdır. 
builder.Services.AddSingleton<IAuthorizationHandler, DocumentRequirementHandler>();


var app = builder.Build();

// VULNERABILITY: API8:2023 - Security Misconfiguration
// Enable detailed error pages in all environments (should only be in Development)
app.UseDeveloperExceptionPage(); // VULNERABILITY: Exposes stack traces in production

// VULNERABILITY: Swagger enabled in all environments (information disclosure)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Vulnerable API V1");
    c.RoutePrefix = string.Empty; // Swagger at root URL
});

// VULNERABILITY: CORS allowing all origins
app.UseCors("AllowAll");

// Global exception handler that reveals too much information
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        
var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
        var exception = exceptionHandlerPathFeature?.Error;

     // VULNERABILITY: Exposing detailed exception information
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Internal Server Error",
  message = exception?.Message,
  stackTrace = exception?.StackTrace,
 innerException = exception?.InnerException?.Message,
          source = exception?.Source
  });
    });
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Initialize database with seed data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<VulnerableDbContext>();
        
        // VULNERABILITY: Auto-migrate database (can cause issues in production)
        context.Database.Migrate();
 
        Console.WriteLine("Database initialized successfully with vulnerable data!");
      Console.WriteLine("??  WARNING: This API contains intentional security vulnerabilities!");
   Console.WriteLine("- API1:2023 - Broken Object Level Authorization (BOLA/IDOR)");
      Console.WriteLine("   - API2:2023 - Broken Authentication");
        Console.WriteLine("   - API3:2023 - Broken Object Property Level Authorization");
        Console.WriteLine("   - API7:2023 - Server Side Request Forgery (SSRF)");
        Console.WriteLine("   - API8:2023 - Security Misconfiguration");
        Console.WriteLine(" - SQL Injection Vulnerabilities");
      Console.WriteLine("   - Sensitive Data Logging");
  Console.WriteLine("");
      Console.WriteLine("Default users created:");
        Console.WriteLine("  - Username: admin, Password: admin123 (IsAdmin: true)");
        Console.WriteLine("  - Username: john, Password: 12345 (IsAdmin: false)");
 Console.WriteLine("  - Username: jane, Password: password (IsAdmin: false)");
 }
    catch (Exception ex)
    {
        // VULNERABILITY: Logging sensitive database connection information
        Console.WriteLine($"Database initialization failed: {ex.Message}");
        Console.WriteLine($"Connection String: {connectionString}");
        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
    }
}

Console.WriteLine($"?? Vulnerable API is running on: {builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5000"}");
Console.WriteLine($"?? Swagger UI available at: http://localhost:5000");

app.Run();

// CLI üzerinden
// dotnet sonarscanner begin /k:"VulnerableAPI-Local-V3" /d:sonar.host.url="http://localhost:9000"  /d:sonar.token="sqp_f3a69c5c7f197286c65ae0c79ecb826e46c3e657" /d:sonar.exclusions="**/Migrations/**/*.cs"
// dotnet build
// dotnet sonarscanner end /d:sonar.login="sqp_f3a69c5c7f197286c65ae0c79ecb826e46c3e657"

// SonarQube UI üzerinden;
// Project Settings / General Settings / Anaysis Scope / Exclusions kısmında **/Migrations/**/*.cs ekleyelim. Migrations klasöründe yapılan değişiklikler ve eklenen kodlar ile ilgili detayları göremeyiz.

// v2. Taratma
// dotnet sonarscanner begin /k:"VulnerableAPI-Local-V3" /v:"2.0" /d:sonar.host.url="http://localhost:9000" /d:sonar.token="sqp_f3a69c5c7f197286c65ae0c79ecb826e46c3e657" /d:sonar.exclusions="**/Migrations/**/*.cs"
// dotnet build
// History kısmında 2.0 versiyonunu görebiliriz. 2.0 versiyonunda yapılan değişiklikler ve eklenen kodlar ile ilgili detayları görebiliriz.
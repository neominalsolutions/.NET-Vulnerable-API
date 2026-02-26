# Identity Migrations & Database Update — Notes

Purpose
- Quick reference for creating/applying EF Core migrations for the `AppIdentityDbContext` in this solution.

Important files
- Migration generated: `VulnerableAPI\Migrations\Identity\20260220125219_App_Identity_Context.cs`
- DbContext: `VulnerableAPI\Data\AppIdentityDbContext.cs`
- Project that contains the DbContext: `VulnerableAPI` (ensure PMC Default Project or CLI `--project` points here)

Correct commands

1) Add a migration
- dotnet CLI (from project folder or supply --project / --startup-project):
  `dotnet ef migrations add Identity_First --context AppIdentityDbContext --output-dir Migrations/Identity`

- Visual Studio Package Manager Console (set Default Project to the project that contains the DbContext):
  `Add-Migration Identity_First -Context AppIdentityDbContext -OutputDir "Migrations\Identity"`

Notes:
- Do NOT use a leading slash in `--output-dir` / `-OutputDir`. Use `Migrations\Identity` (Windows-style in PMC) or `Migrations/Identity` (CLI).
- `--output-dir` / `-OutputDir` is only for `Add-Migration`, not for `Update-Database`.

2) Apply migrations to the database (Update-Database)
- Package Manager Console:
  `Update-Database -Context AppIdentityDbContext`

- dotnet CLI:
  `dotnet ef database update --context AppIdentityDbContext`
  If your DbContext project differs from the startup project, add:
  `--project <ProjectContainingDbContext> --startup-project <StartupProject>`

Common pitfalls & troubleshooting
- Wrong context name: `AppIdentityDb` is incorrect. Use `AppIdentityDbContext`.
- Ensure `Microsoft.EntityFrameworkCore.Design` is referenced in the project and `dotnet-ef` is installed for CLI: `dotnet tool install --global dotnet-ef` (if needed).
- In PMC, set the Default Project to the project containing the DbContext before running `Add-Migration` / `Update-Database`.
- Confirm the connection string in `appsettings.json` and that PostgreSQL is reachable.
- If you need to apply a specific migration by name or timestamp:
  - PMC: `Update-Database -Migration 20260220125219_App_Identity_Context -Context AppIdentityDbContext`
  - CLI: `dotnet ef database update 20260220125219_App_Identity_Context --context AppIdentityDbContext`

Commit guidance
- Add the generated migration files under `Migrations\Identity` to source control and commit with a message like:
  `Add identity migration: Identity_First (AppIdentityDbContext)`

## Rate Limiting Configuration

The API implements rate limiting on authentication endpoints to protect against brute force attacks.

Configuration
- Policy name: `AuthEndpoints`
- Window: 30 seconds
- Permit limit: 10 requests per window
- Queue limit: 0 (no queuing - immediate 429 response when limit exceeded)
- Auto replenishment: true (counter resets after 30 seconds)
- Response status: 429 Too Many Requests with custom JSON message

Protected endpoints
- `POST /api/AuthControllerV2/register` - User registration
- `POST /api/AuthControllerV2/login` - User login

How it works
- Each endpoint can receive maximum 10 requests within a 30-second window
- After exceeding the limit, the API immediately returns HTTP 429 (Too Many Requests)
- The counter automatically resets after the 30-second window expires
- Response includes `Retry-After: 30` header

Testing rate limiting
```powershell
# Test 1: Using PowerShell to trigger rate limit (should get 429 after 10 requests)
for ($i=1; $i -le 15; $i++) {
    Write-Host "Request $i - $(Get-Date -Format 'HH:mm:ss')"
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5000/api/AuthControllerV2/register" `
            -Method Post `
            -ContentType "application/json" `
            -Body '{"username":"test'$i'","email":"test'$i'@test.com","password":"Test@123456789"}' `
            -ErrorAction Stop
        Write-Host "Status: $($response.StatusCode)" -ForegroundColor Green
    }
    catch {
        Write-Host "Status: $($_.Exception.Response.StatusCode.value__) - $($_.Exception.Message)" -ForegroundColor Red
    }
    Start-Sleep -Milliseconds 100
}
```

```bash
# Test 2: Using curl (Linux/Mac/Git Bash)
for i in {1..15}; do
  echo "Request $i - $(date +%T)"
  curl -X POST "http://localhost:5000/api/AuthControllerV2/register" \
    -H "Content-Type: application/json" \
    -d "{\"username\":\"test$i\",\"email\":\"test$i@test.com\",\"password\":\"Test@123456789\"}" \
    -w "\nHTTP Status: %{http_code}\n" \
    -s -o /dev/null
  sleep 0.1
done
```

Expected behavior
- Requests 1-10: Should return 200 OK or 400 Bad Request (if user exists)
- Requests 11+: Should return 429 Too Many Requests with JSON response:
  ```json
  {
    "error": "Too Many Requests",
    "message": "Rate limit exceeded. Maximum 10 requests per 30 seconds allowed.",
    "retryAfterSeconds": 30
  }
  ```

Troubleshooting
- If rate limiting doesn't work:
  1. Ensure `app.UseRateLimiter()` is called BEFORE `app.UseAuthentication()` in Program.cs
  2. Check `QueueLimit = 0` (not 2 or higher, which would allow queued requests)
  3. Verify `[EnableRateLimiting("AuthEndpoints")]` attribute is on the endpoints
  4. Restart the application after configuration changes

Note: Rate limiting is configured at the application level. For production, consider additional measures like IP-based blocking, CAPTCHA, distributed rate limiting with Redis, or using a reverse proxy/API gateway.

## Security Logging

The API implements comprehensive security logging for authentication events on `AuthControllerV2`.

Logged events

### Registration (`POST /api/AuthControllerV2/register`)
1. **Registration attempt started** (Information)
   - Username, Email, IP Address, Timestamp
2. **Registration failed - Username already exists** (Warning)
   - Username, IP Address
3. **Registration failed - Validation errors** (Warning)
   - UserId, Username, IP Address, Error codes
4. **User registered successfully** (Information)
   - UserId, Username, Email, IP Address

### Login (`POST /api/AuthControllerV2/login`)
1. **Login attempt started** (Information)
   - Username, IP Address, Timestamp
2. **Login attempt on locked account** (Warning)
   - UserId, Username, IP Address, Lockout End Time
3. **Failed login attempt** (Warning)
   - UserId, Username, IP Address, Failed Attempt Count
4. **Account locked due to multiple failed attempts** (Warning)
   - UserId, Username, IP Address, Lockout End Time
5. **Account unlock after lockout period expired** (Information)
   - UserId, Username, IP Address
6. **Successful login** (Information)
   - UserId, Username, IP Address, Token Expiration Time
7. **Login attempt with non-existent username** (Warning)
   - Masked Username (first 3 chars + ***), IP Address

Log format
All security logs use the prefix `[SECURITY]` for easy filtering and monitoring.

Example log output:
```
[2026-02-26 10:30:45] [Information] [SECURITY] Registration attempt started - Username: john_doe, Email: john@example.com, IP: 192.168.1.100, Timestamp: 2026-02-26T10:30:45Z
[2026-02-26 10:30:45] [Information] [SECURITY] User registered successfully - UserId: abc123, Username: john_doe, Email: john@example.com, IP: 192.168.1.100

[2026-02-26 10:31:00] [Information] [SECURITY] Login attempt started - Username: john_doe, IP: 192.168.1.100, Timestamp: 2026-02-26T10:31:00Z
[2026-02-26 10:31:00] [Warning] [SECURITY] Failed login attempt - UserId: abc123, Username: john_doe, IP: 192.168.1.100, FailedAttempts: 1

[2026-02-26 10:31:30] [Warning] [SECURITY] Account locked due to multiple failed attempts - UserId: abc123, Username: john_doe, IP: 192.168.1.100, LockoutEnd: 2026-02-26T10:46:30Z

[2026-02-26 10:32:00] [Information] [SECURITY] Login attempt started - Username: admin, IP: 192.168.1.100, Timestamp: 2026-02-26T10:32:00Z
[2026-02-26 10:32:00] [Information] [SECURITY] Successful login - UserId: xyz789, Username: admin, IP: 192.168.1.100, TokenExpires: 2026-02-26T10:37:00Z
```

Viewing logs
- Console output: Logs are written to the console by default
- Application Insights: Configure in `appsettings.json` for production monitoring
- File logging: Add `Serilog` or other file logging providers

Filtering security logs
```powershell
# PowerShell: Filter security logs from console output
Get-Content -Path "app.log" | Select-String -Pattern "\[SECURITY\]"

# PowerShell: Filter only failed login attempts
Get-Content -Path "app.log" | Select-String -Pattern "\[SECURITY\] Failed login"

# PowerShell: Filter account lockouts
Get-Content -Path "app.log" | Select-String -Pattern "\[SECURITY\] Account locked"
```

Best practices
1. **Never log passwords** - Passwords are never logged in plaintext
2. **Mask sensitive data** - Non-existent usernames are partially masked (first 3 chars + ***)
3. **Include IP addresses** - Essential for tracking suspicious activity
4. **Use structured logging** - Use log placeholders (`{Username}`) instead of string interpolation
5. **Set appropriate log levels** - Information for normal operations, Warning for suspicious activity
6. **Monitor lockout events** - Set up alerts for multiple account lockout events from the same IP
7. **Compliance** - Security logs help meet GDPR, PCI-DSS, and other compliance requirements

Security reminder
- This project intentionally contains vulnerabilities for training. Do not deploy this configuration to production.


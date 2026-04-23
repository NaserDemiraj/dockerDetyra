$api = "http://localhost:5000/api"

for ($i = 20; $i -le 24; $i++) {
    $username = "testuser$i"
    $email = "user$i@test.com"
    $password = "pass123"
    
    # Register
    try {
        $regResp = Invoke-RestMethod -Uri "$api/auth/register" -Method POST `
          -Headers @{"Content-Type"="application/json"} `
          -Body ("{`"username`":`"$username`",`"email`":`"$email`",`"password`":`"$password`"}")
    } catch { }
    
    # Login (creates container)
    try {
        $loginResp = Invoke-RestMethod -Uri "$api/auth/login" -Method POST `
          -Headers @{"Content-Type"="application/json"} `
          -Body ("{`"username`":`"$username`",`"password`":`"$password`"}")
    } catch { }
    
    Write-Host "✓ User $i logged in"
    Start-Sleep -Milliseconds 500
}

Write-Host "`n✓ Checking all containers..."
docker ps --format "table {{.Names}}`t{{.Status}}"

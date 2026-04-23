# Load Test: 5 Concurrent Users
# Tests CodeLab with simultaneous users executing code

$API_URL = "http://localhost:5000/api"

# Sample users for load test
$users = @(
    @{username="loadtest1"; email="loadtest1@test.com"; password="Pass@123"}
    @{username="loadtest2"; email="loadtest2@test.com"; password="Pass@123"}
    @{username="loadtest3"; email="loadtest3@test.com"; password="Pass@123"}
    @{username="loadtest4"; email="loadtest4@test.com"; password="Pass@123"}
    @{username="loadtest5"; email="loadtest5@test.com"; password="Pass@123"}
)

# Code samples to execute
$pythonCode1 = @"
# Calculate factorial
def factorial(n):
    if n <= 1:
        return 1
    return n * factorial(n-1)

result = factorial(10)
print(f'10! = {result}')
"@

$pythonCode2 = @"
# Fibonacci sequence
fib = [0, 1]
for i in range(8):
    fib.append(fib[-1] + fib[-2])
print(f'Fibonacci: {fib}')
"@

$pythonCode3 = @"
# Prime checker
def is_prime(n):
    if n < 2:
        return False
    for i in range(2, int(n**0.5) + 1):
        if n % i == 0:
            return False
    return True

primes = [n for n in range(2, 100) if is_prime(n)]
print(f'Primes < 100: {len(primes)} found')
"@

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "CodeLab Load Test - 5 Concurrent Users" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

# Function to register and login
function Register-User {
    param($username, $email, $password)
    
    try {
        $body = @{
            username = $username
            email = $email
            password = $password
        } | ConvertTo-Json
        
        $response = Invoke-WebRequest -Uri "$API_URL/auth/register" `
            -Method POST `
            -ContentType "application/json" `
            -Body $body
        
        $result = $response.Content | ConvertFrom-Json
        if ($result.success) {
            Write-Host "✅ User '$username' registered and logged in" -ForegroundColor Green
            return $result.token
        }
    }
    catch {
        Write-Host "❌ Registration failed for $username" -ForegroundColor Red
    }
    return $null
}

# Function to execute code
function Execute-Code {
    param($token, $language, $code, $userId)
    
    try {
        $body = @{
            language = $language
            code = $code
        } | ConvertTo-Json
        
        $response = Invoke-WebRequest -Uri "$API_URL/code/execute" `
            -Method POST `
            -ContentType "application/json" `
            -Body $body `
            -Headers @{Authorization = "Bearer $token"}
        
        $result = $response.Content | ConvertFrom-Json
        if ($result.success) {
            Write-Host "✅ User$userId executed code (${result.executionTimeMs}ms)" -ForegroundColor Green
            Write-Host "   Output: $($result.output.Substring(0, [Math]::Min(50, $result.output.Length)))..." -ForegroundColor Gray
        } else {
            Write-Host "❌ User$userId execution failed: $($result.error)" -ForegroundColor Red
        }
        return $result.success
    }
    catch {
        Write-Host "❌ Execution error for User$userId: $_" -ForegroundColor Red
    }
    return $false
}

# Register all users
Write-Host "`n[STEP 1] Registering 5 users..." -ForegroundColor Yellow
$tokens = @()
for ($i = 0; $i -lt $users.Count; $i++) {
    $token = Register-User $users[$i].username $users[$i].email $users[$i].password
    if ($token) {
        $tokens += $token
    }
}

if ($tokens.Count -eq 5) {
    Write-Host "✅ All 5 users registered successfully" -ForegroundColor Green
} else {
    Write-Host "❌ Only $($tokens.Count) users registered. Aborting." -ForegroundColor Red
    exit 1
}

# Show running containers
Write-Host "`n[STEP 2] Docker containers for users:" -ForegroundColor Yellow
docker ps --filter "name=codelab-user" --format "table {{.Names}}\t{{.Status}}" | Select-Object -First 6

# Execute code concurrently
Write-Host "`n[STEP 3] Starting concurrent code execution..." -ForegroundColor Yellow
$codeSamples = @($pythonCode1, $pythonCode2, $pythonCode3, $pythonCode1, $pythonCode2)
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

$jobs = @()
for ($i = 0; $i -lt $tokens.Count; $i++) {
    $job = Start-Job -ScriptBlock {
        param($apiUrl, $token, $code, $id)
        
        $body = @{
            language = "python"
            code = $code
        } | ConvertTo-Json
        
        $response = Invoke-WebRequest -Uri "$apiUrl/code/execute" `
            -Method POST `
            -ContentType "application/json" `
            -Body $body `
            -Headers @{Authorization = "Bearer $token"}
        
        @{
            userId = $id
            response = $response.Content | ConvertFrom-Json
        }
    } -ArgumentList @($API_URL, $tokens[$i], $codeSamples[$i], $i+1)
    
    $jobs += $job
}

# Wait for all jobs
Write-Host "Waiting for all 5 users to execute..." -ForegroundColor Cyan
$results = $jobs | Wait-Job | ForEach-Object { Receive-Job $_ }
$stopwatch.Stop()

# Display results
Write-Host "`n[STEP 4] Execution Results:" -ForegroundColor Yellow
foreach ($result in $results) {
    if ($result.response.success) {
        Write-Host "  ✅ User$($result.userId): Success (${$result.response.executionTimeMs}ms)" -ForegroundColor Green
    } else {
        Write-Host "  ❌ User$($result.userId): Failed - $($result.response.error)" -ForegroundColor Red
    }
}

# Show docker stats
Write-Host "`n[STEP 5] Docker Resource Usage:" -ForegroundColor Yellow
Write-Host "Command: docker stats --no-stream" -ForegroundColor Gray
docker stats --no-stream codelab-user-1 codelab-user-2 codelab-user-3 codelab-user-4 codelab-user-5 2>$null

Write-Host "`n[SUMMARY]" -ForegroundColor Cyan
Write-Host "- Users registered: 5 ✅" -ForegroundColor Green
Write-Host "- Code executions: $($results.Count) concurrent" -ForegroundColor Green
Write-Host "- Total time: $($stopwatch.ElapsedMilliseconds)ms" -ForegroundColor Green
Write-Host "- Containers running: $(docker ps --filter 'name=codelab-user' --format '{{json .}}' 2>$null | Measure-Object -Line | Select-Object -ExpandProperty Lines)" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Cyan

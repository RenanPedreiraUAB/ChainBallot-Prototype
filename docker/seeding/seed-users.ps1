param(
    [string]$AuthBaseUrl = "http://localhost:5015",
    [string]$AuthorizationBaseUrl = "http://localhost:5147",
    [string]$ElectionId = "election-demo",
    [string]$AdminSeedKey = "CHANGE_ME_LOCAL_DOCKER_AUTHZ_ADMIN_SEED_KEY",
    [int]$UserCount = 200,
    [switch]$SeedEligibility,
    [switch]$OverwriteCredentialsFile
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$credentialsPath = Join-Path $PSScriptRoot "cynetauth-users.csv"

if ((-not (Test-Path $credentialsPath)) -or $OverwriteCredentialsFile) {
    $rows = for ($i = 1; $i -le $UserCount; $i++) {
        $username = "voter{0:D3}" -f $i
        $password = "ChainBallot!2026-{0:D3}" -f $i
        [PSCustomObject]@{
            username = $username
            password = $password
        }
    }

    $rows | Export-Csv -Path $credentialsPath -NoTypeInformation -Encoding UTF8
    Write-Host "Generated credentials file: $credentialsPath"
}

$users = Import-Csv -Path $credentialsPath

$registered = 0
$alreadyExists = 0
$failed = 0
$seeded = 0

foreach ($user in $users) {
    $registerBody = @{
        username = $user.username
        password = $user.password
    } | ConvertTo-Json

    try {
        $response = Invoke-WebRequest -Method Post -Uri "$AuthBaseUrl/api/auth/register" -ContentType "application/json" -Body $registerBody -UseBasicParsing
        if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
            $registered++
        }
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq 400) {
            $alreadyExists++
        }
        else {
            $failed++
            Write-Warning "Failed registering $($user.username): $($_.Exception.Message)"
            continue
        }
    }

    if ($SeedEligibility) {
        $loginBody = @{
            username = $user.username
            password = $user.password
        } | ConvertTo-Json

        $subject = $null
        try {
            $loginResponse = Invoke-RestMethod -Method Post -Uri "$AuthBaseUrl/api/auth/login" -ContentType "application/json" -Body $loginBody
            $subject = $loginResponse.subject
        }
        catch {
            Write-Warning "Failed login for $($user.username) while resolving subject for eligibility seed: $($_.Exception.Message)"
            continue
        }

        if ([string]::IsNullOrWhiteSpace($subject)) {
            Write-Warning "No subject returned for $($user.username); skipping eligibility seed."
            continue
        }

        $seedBody = @{
            subject = $subject
            isEligible = $true
        } | ConvertTo-Json

        try {
            Invoke-WebRequest -Method Post `
                -Uri "$AuthorizationBaseUrl/api/authorization/admin/elections/$ElectionId/eligibility" `
                -Headers @{ "X-Admin-Seed-Key" = $AdminSeedKey } `
                -ContentType "application/json" `
                -Body $seedBody `
                -UseBasicParsing | Out-Null
            $seeded++
        }
        catch {
            Write-Warning "Failed eligibility seed for ${subject}: $($_.Exception.Message)"
        }
    }
}

Write-Host "Done. registered=$registered alreadyExists=$alreadyExists failed=$failed seeded=$seeded"
Write-Host "Credentials file: $credentialsPath"

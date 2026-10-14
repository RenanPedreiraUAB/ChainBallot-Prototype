param(
    [string]$BaseUrl = "http://localhost:5301",
    [string]$ElectionId = "election-scale-demo",
    [int]$TotalRequests = 3000,
    [int]$ParallelJobs = 24,
    [int]$RequestTimeoutSeconds = 20,
    [string]$AuthHeaderName = "X-Processing-Auth",
    [string]$AuthToken = "processing-ingest-dev-token"
)

$ErrorActionPreference = "Stop"

if ($TotalRequests -lt 1) {
    throw "TotalRequests must be at least 1."
}

if ($ParallelJobs -lt 1) {
    throw "ParallelJobs must be at least 1."
}

$runId = [Guid]::NewGuid().ToString("N")
Write-Host "Spike run id: $runId"
Write-Host "Target: $BaseUrl/api/processing/ingest"
Write-Host "Total requests: $TotalRequests"
Write-Host "Parallel jobs: $ParallelJobs"

$requestsPerJob = [Math]::Ceiling($TotalRequests / $ParallelJobs)
$jobs = @()

for ($jobIndex = 0; $jobIndex -lt $ParallelJobs; $jobIndex++) {
    $start = $jobIndex * $requestsPerJob
    $end = [Math]::Min($start + $requestsPerJob, $TotalRequests)

    if ($start -ge $end) {
        continue
    }

    $jobs += Start-Job -ScriptBlock {
        param($BaseUrlParam, $ElectionIdParam, $RunIdParam, $RequestTimeoutParam, $StartParam, $EndParam, $AuthHeaderNameParam, $AuthTokenParam)

        function Get-LowerHexSha256 {
            param([string]$Text)

            $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
            $sha = [System.Security.Cryptography.SHA256]::Create()
            try {
                $hash = $sha.ComputeHash($bytes)
            }
            finally {
                $sha.Dispose()
            }

            return ([System.BitConverter]::ToString($hash) -replace "-", "").ToLowerInvariant()
        }

        $ok = 0
        $failed = 0
        $headers = @{ $AuthHeaderNameParam = $AuthTokenParam }

        for ($i = $StartParam; $i -lt $EndParam; $i++) {
            $candidateId = if ($i % 2 -eq 0) { "cand-a" } else { "cand-b" }
            $submissionNonce = [Guid]::NewGuid().ToString("N")
            $rand = [Guid]::NewGuid().ToString("N")
            $vid = "vid_" + (Get-LowerHexSha256 -Text "vid-scale-$RunIdParam-$i")
            $ballotCommitment = Get-LowerHexSha256 -Text "commit-$RunIdParam-$i"
            $ballotNullifier = Get-LowerHexSha256 -Text "$ElectionIdParam|$vid|$submissionNonce"

            $payload = @{
                electionId = $ElectionIdParam
                vid = $vid
                selectedCandidateId = $candidateId
                ballotCommitment = $ballotCommitment
                submissionNonce = $submissionNonce
                ballotNullifier = $ballotNullifier
                encryptedVotePayload = @{
                    algorithm = "A256GCM"
                    nonceBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("nonce-$rand"))
                    tagBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("tag-$rand"))
                    ciphertextBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("cipher-$rand"))
                }
                createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
            } | ConvertTo-Json -Depth 6

            try {
                Invoke-RestMethod -Method Post -Uri "$BaseUrlParam/api/processing/ingest" -ContentType "application/json" -Body $payload -Headers $headers -TimeoutSec $RequestTimeoutParam | Out-Null
                $ok++
            }
            catch {
                $failed++
            }
        }

        [PSCustomObject]@{
            Ok = $ok
            Failed = $failed
            Start = $StartParam
            End = $EndParam
        }
    } -ArgumentList $BaseUrl, $ElectionId, $runId, $RequestTimeoutSeconds, $start, $end, $AuthHeaderName, $AuthToken
}

Wait-Job -Job $jobs | Out-Null
$results = Receive-Job -Job $jobs
$jobs | Remove-Job -Force | Out-Null

$totalOk = ($results | Measure-Object -Property Ok -Sum).Sum
$totalFailed = ($results | Measure-Object -Property Failed -Sum).Sum

Write-Host ""
Write-Host "Spike completed."
Write-Host "Accepted HTTP posts: $totalOk"
Write-Host "Failed HTTP posts: $totalFailed"
Write-Host "ElectionId used: $ElectionId"

if ($totalFailed -gt 0) {
    throw "Spike run completed with failures."
}

param(
    [string]$ComposeFile = ".\docker-compose.local.yml",
    [string]$ElectionId = "election-demo",
    [int]$VoterCount = 200,
    [switch]$SkipDockerUp,
    [switch]$SkipSeeding,
    [switch]$FullCycle,
    [switch]$UseFreshElectionId,
    [string]$PortalBaseUrl = "http://localhost:5181",
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"

# Matches docker-compose.local.yml Processing__Authentication__Credentials / CHAINBALLOT_TRANSPARENCY__AUTHENTICATION__CREDENTIALS.
$processingIngestHeaders = @{ "X-Processing-Auth" = "processing-ingest-dev-token" }
$transparencyIngestHeaders = @{ "X-Transparency-Auth" = "transparency-ingestion-dev-token" }

function Wait-Healthy {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Url,
        [Parameter(Mandatory = $true)][int]$Timeout
    )

    $deadline = (Get-Date).AddSeconds($Timeout)
    while ((Get-Date) -lt $deadline) {
        try {
            $resp = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 5
            if ($resp.StatusCode -ge 200 -and $resp.StatusCode -lt 300) {
                Write-Host "[ok] $Name healthy at $Url"
                return
            }
        }
        catch {
            Start-Sleep -Seconds 2
        }
    }

    throw "Timed out waiting for $Name health at $Url"
}

function Wait-Condition {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Condition,
        [Parameter(Mandatory = $true)][int]$Timeout
    )

    $deadline = (Get-Date).AddSeconds($Timeout)
    while ((Get-Date) -lt $deadline) {
        if (& $Condition) {
            Write-Host "[ok] $Name"
            return
        }

        Start-Sleep -Seconds 2
    }

    throw "Timed out waiting for condition: $Name"
}

function To-LowerHexSha256 {
    param([Parameter(Mandatory = $true)][string]$Text)

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

function Initialize-FreshElectionArtifacts {
    param(
        [Parameter(Mandatory = $true)][string]$TemplateElectionId,
        [Parameter(Mandatory = $true)][string]$TargetElectionId
    )

    $metadataRoot = Join-Path $PSScriptRoot "metadata"
    $electionTemplatePath = Join-Path $metadataRoot "elections\$TemplateElectionId.json"
    $electionTargetPath = Join-Path $metadataRoot "elections\$TargetElectionId.json"
    $metadataDigestTemplatePath = Join-Path $metadataRoot "digests\metadata\$TemplateElectionId.json"
    $metadataDigestTargetPath = Join-Path $metadataRoot "digests\metadata\$TargetElectionId.json"
    $auditDigestTemplatePath = Join-Path $metadataRoot "digests\audit\$TemplateElectionId.json"
    $auditDigestTargetPath = Join-Path $metadataRoot "digests\audit\$TargetElectionId.json"

    if (-not (Test-Path $electionTemplatePath)) {
        throw "Election metadata template not found: $electionTemplatePath"
    }

    Copy-Item -Path $electionTemplatePath -Destination $electionTargetPath -Force
    $electionJson = Get-Content -Raw -Path $electionTargetPath
    $electionJson = $electionJson.Replace('"id": "' + $TemplateElectionId + '"', '"id": "' + $TargetElectionId + '"')
    Set-Content -Path $electionTargetPath -Value $electionJson -Encoding UTF8

    if (Test-Path $metadataDigestTemplatePath) {
        Copy-Item -Path $metadataDigestTemplatePath -Destination $metadataDigestTargetPath -Force
        $metadataDigest = Get-Content -Raw -Path $metadataDigestTargetPath
        $metadataDigest = $metadataDigest.Replace($TemplateElectionId, $TargetElectionId)
        Set-Content -Path $metadataDigestTargetPath -Value $metadataDigest -Encoding UTF8
    }

    if (Test-Path $auditDigestTemplatePath) {
        Copy-Item -Path $auditDigestTemplatePath -Destination $auditDigestTargetPath -Force
        $auditDigest = Get-Content -Raw -Path $auditDigestTargetPath
        $auditDigest = $auditDigest.Replace($TemplateElectionId, $TargetElectionId)
        Set-Content -Path $auditDigestTargetPath -Value $auditDigest -Encoding UTF8
    }

    return $TargetElectionId
}

if (-not $PSBoundParameters.ContainsKey('FullCycle')) {
    $FullCycle = $true
}

if ($FullCycle) {
    $UseFreshElectionId = $true
}

if ($UseFreshElectionId) {
    $timestampSuffix = Get-Date -Format "yyyyMMddHHmmss"
    $ElectionId = Initialize-FreshElectionArtifacts -TemplateElectionId $ElectionId -TargetElectionId ("election-demo-cycle-$timestampSuffix")
}

if (-not $SkipDockerUp) {
    Write-Host "Starting local stack..."
    docker compose -f $ComposeFile up --build -d
}
else {
    try {
        docker ps | Out-Null
    }
    catch {
        throw "Docker does not appear to be available. Start Docker Desktop or run without -SkipDockerUp."
    }
}

Write-Host "Waiting for service health endpoints..."
Wait-Healthy -Name "CynetAuth" -Url "http://localhost:5015/health" -Timeout $TimeoutSeconds
Wait-Healthy -Name "Authorization" -Url "http://localhost:5147/health" -Timeout $TimeoutSeconds
Wait-Healthy -Name "Outbound" -Url "http://localhost:5301/health" -Timeout $TimeoutSeconds
Wait-Healthy -Name "Transparency" -Url "http://localhost:5264/api/transparency/health" -Timeout $TimeoutSeconds
Wait-Healthy -Name "Watcher" -Url "http://localhost:5161/api/watcher/health" -Timeout $TimeoutSeconds

if (-not $SkipSeeding) {
    Write-Host "Seeding demo users and eligibility..."
    & ".\docker\seeding\seed-users.ps1" -ElectionId $ElectionId -UserCount $VoterCount -SeedEligibility -OverwriteCredentialsFile
}

$users = Import-Csv ".\docker\seeding\cynetauth-users.csv"
if ($null -eq $users -or $users.Count -eq 0) {
    throw "No users found in docker/seeding/cynetauth-users.csv"
}

$submittedUsers = @()
$firstSubmittedProfile = $null
$selectedUsername = $null
$headers = $null
$profile = $null
$grant = $null
$submittedBallots = @()
$candidateAShare = [Math]::Floor($users.Count * 0.6)
$candidateAKey = "cand-a"
$candidateBKey = "cand-b"
$candidateACount = 0
$candidateBCount = 0

for ($index = 0; $index -lt $users.Count; $index++) {
    $user = $users[$index]
    Write-Host "Attempting login/grant for $($user.username)..."

    $loginBody = @{ username = $user.username; password = $user.password } | ConvertTo-Json
    $login = Invoke-RestMethod -Method Post -Uri "http://localhost:5015/api/auth/login" -ContentType "application/json" -Body $loginBody
    if ([string]::IsNullOrWhiteSpace($login.accessToken)) {
        throw "Login failed for $($user.username): access token missing."
    }

    $candidateHeaders = @{ Authorization = "Bearer $($login.accessToken)" }
    $candidateProfile = Invoke-RestMethod -Method Get -Uri "http://localhost:5147/api/authorization/profile" -Headers $candidateHeaders
    try {
        $candidateGrant = Invoke-RestMethod -Method Post -Uri "http://localhost:5147/api/authorization/elections/$ElectionId/grant" -Headers $candidateHeaders -ContentType "application/json" -Body "{}"
    }
    catch {
        if (-not $FullCycle -and $_.Exception.Response -ne $null -and $_.Exception.Response.StatusCode.value__ -eq 409) {
            continue
        }

        throw
    }

    $candidateKey = if ($index -lt $candidateAShare) { $candidateAKey } else { $candidateBKey }
    if ($candidateKey -eq $candidateAKey) {
        $candidateACount++
    }
    else {
        $candidateBCount++
    }

    $submissionNonce = [Guid]::NewGuid().ToString("N")
    $ballotNullifier = To-LowerHexSha256 "$ElectionId|$($candidateProfile.vid)|$submissionNonce"
    $ballotCommitment = To-LowerHexSha256 ([Guid]::NewGuid().ToString("N"))
    $rand = [Guid]::NewGuid().ToString("N")

    $package = @{
        electionId = $ElectionId
        vid = $candidateProfile.vid
        selectedCandidateId = $candidateKey
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

    Write-Host "Publishing ballot package for $($user.username) through outbound ingest (Kafka-backed)..."
    Invoke-RestMethod -Method Post -Uri "http://localhost:5301/api/processing/ingest" -Headers $processingIngestHeaders -ContentType "application/json" -Body $package | Out-Null

    $consume = @{
        grantId = $candidateGrant.grantId
        submissionNonce = $submissionNonce
    } | ConvertTo-Json
    Invoke-RestMethod -Method Post -Uri "http://localhost:5147/api/authorization/elections/$ElectionId/consume" -Headers $candidateHeaders -ContentType "application/json" -Body $consume | Out-Null

    $submittedUsers += $user.username
    $submittedBallots += [PSCustomObject]@{
        Vid = $candidateProfile.vid
        BallotCommitment = $ballotCommitment
        SubmissionNonce = $submissionNonce
        CandidateId = $candidateKey
        BatchId = "batch-final-1"
        LeafIndex = $index
        RecordedAtUtc = [DateTimeOffset]::UtcNow
    }
    if ($null -eq $firstSubmittedProfile) {
        $firstSubmittedProfile = $candidateProfile
    }

    if (-not $FullCycle) {
        $selectedUsername = $user.username
        $headers = $candidateHeaders
        $profile = $candidateProfile
        $grant = $candidateGrant
        break
    }
}

if ($FullCycle) {
    if ($submittedUsers.Count -ne $users.Count) {
        throw "Full-cycle submission incomplete. Submitted $($submittedUsers.Count) of $($users.Count) voters."
    }

    Wait-Condition -Name "Outbound anchored all ballots" -Timeout $TimeoutSeconds -Condition {
        try {
            $state = Invoke-RestMethod -Method Get -Uri "http://localhost:5301/api/processing/elections/$ElectionId/state"
            return $state.acceptedBallotCount -eq $submittedUsers.Count -and
                $state.anchoredBallotCount -eq $submittedUsers.Count -and
                $state.pendingBallotCount -eq 0 -and
                $null -ne $state.lastAnchoredBatch
        }
        catch {
            return $false
        }
    }

    $currentSnapshot = Invoke-RestMethod -Method Get -Uri "http://localhost:5264/api/transparency/elections/$ElectionId/snapshot"
    $currentProjectionStatus = Invoke-RestMethod -Method Get -Uri "http://localhost:5264/api/transparency/elections/$ElectionId/projection-status"

    $ballotCommitmentChain = ($submittedBallots | ForEach-Object { $_.BallotCommitment }) -join "|"
    $batchRoot = To-LowerHexSha256 $ballotCommitmentChain

    $vidEntries = @()
    foreach ($submission in $submittedBallots) {
        $leafIndex = [int]$submission.LeafIndex
        $consistencyDigest = To-LowerHexSha256 "$ElectionId|$($submission.Vid)|$($submission.BatchId)|$($submission.BallotCommitment)|$batchRoot|$leafIndex"
        $vidEntries += @{
            vid = $submission.Vid
            batchId = $submission.BatchId
            ballotCommitment = $submission.BallotCommitment
            inclusionProofReference = @{
                batchId = $submission.BatchId
                merkleRoot = $batchRoot
                leafId = "leaf-$($leafIndex + 1)"
                leafIndex = $leafIndex
                proofUri = "docker://chainballot/$ElectionId/$($submission.Vid)"
                proofHash = $consistencyDigest
            }
            recordedAtUtc = $submission.RecordedAtUtc.ToString("o")
            consistencyDigest = $consistencyDigest
        }
    }

    $decryptionHeaders = @{ "X-Operator-Key" = "decryption-operator-key" }
    $ceremonyRequest = @{
        requiredShares = 3
        requiredApprovals = 2
        actor = "local-e2e-smoke"
    } | ConvertTo-Json
    $ceremony = Invoke-RestMethod -Method Post -Uri "http://localhost:5287/api/decryption/elections/$ElectionId/ceremony" -Headers $decryptionHeaders -ContentType "application/json" -Body $ceremonyRequest

    foreach ($approver in @("governor-a", "governor-b")) {
        $approveBody = @{ approverId = $approver; note = "local-e2e-smoke approval" } | ConvertTo-Json
        Invoke-RestMethod -Method Post -Uri "http://localhost:5287/api/decryption/elections/$ElectionId/ceremony/approve" -Headers $decryptionHeaders -ContentType "application/json" -Body $approveBody | Out-Null
    }

    $trustees = @("trustee-a", "trustee-b", "trustee-c")
    foreach ($trustee in $trustees) {
        $shareDigest = To-LowerHexSha256 "$ElectionId|$trustee|share"
        $shareBody = @{ trusteeId = $trustee; shareDigest = $shareDigest } | ConvertTo-Json
        Invoke-RestMethod -Method Post -Uri "http://localhost:5287/api/decryption/elections/$ElectionId/ceremony/share" -Headers $decryptionHeaders -ContentType "application/json" -Body $shareBody | Out-Null
    }

    $finalTallies = [ordered]@{
        $candidateAKey = [long]$candidateACount
        $candidateBKey = [long]$candidateBCount
    }

    $finalizeBody = @{
        actor = "local-e2e-smoke"
        tallies = $finalTallies
    } | ConvertTo-Json -Depth 4
    $finalizedCeremony = Invoke-RestMethod -Method Post -Uri "http://localhost:5287/api/decryption/elections/$ElectionId/ceremony/finalize" -Headers $decryptionHeaders -ContentType "application/json" -Body $finalizeBody

    $publishBody = @{ actor = "local-e2e-smoke" } | ConvertTo-Json
    $publishedCeremony = Invoke-RestMethod -Method Post -Uri "http://localhost:5287/api/decryption/elections/$ElectionId/publish-final-tally" -Headers $decryptionHeaders -ContentType "application/json" -Body $publishBody

    if ($publishedCeremony.status -ne "Completed" -or [string]::IsNullOrWhiteSpace($publishedCeremony.publicationReference)) {
        throw "Decryption final tally publication did not complete successfully."
    }

    $finalProjectionBody = @{
        lifecycleState = "Finalized"
        resultDigest = $publishedCeremony.resultDigest
        settlementReference = $currentSnapshot.settlementReference
        candidateTallies = $finalTallies
        vidEntries = $vidEntries
        metadataDigest = $currentProjectionStatus.metadataDigest
        auditDigest = $currentProjectionStatus.auditDigest
        isStale = $false
        staleReason = $null
    } | ConvertTo-Json -Depth 8

    Invoke-RestMethod -Method Post -Uri "http://localhost:5264/api/transparency/elections/$ElectionId/ingest" -Headers $transparencyIngestHeaders -ContentType "application/json" -Body $finalProjectionBody | Out-Null

    Wait-Condition -Name "Finalized transparency snapshot available" -Timeout $TimeoutSeconds -Condition {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri "http://localhost:5264/api/transparency/elections/$ElectionId/snapshot"
            return $snapshot.lifecycleState -eq "Finalized" -and
                $snapshot.candidateTallies."cand-a" -eq $candidateACount -and
                $snapshot.candidateTallies."cand-b" -eq $candidateBCount
        }
        catch {
            return $false
        }
    }

    $verification = Invoke-RestMethod -Method Get -Uri "http://localhost:5264/api/transparency/elections/$ElectionId/verify/$($submittedBallots[0].Vid)"
    $verificationOutcome = [string]$verification.outcome
    $isVerified = $verificationOutcome -eq "Verified" -or $verificationOutcome -eq "0"
    if (-not $isVerified) {
        throw "Finalized VID verification did not succeed for $($submittedBallots[0].Vid). Outcome=$verificationOutcome"
    }
}
else {
    if ($null -eq $grant) {
        throw "No seeded user could obtain a new grant. Clear authorization state or reseed with fresh users."
    }

    Write-Host "Using voter $selectedUsername"

    Wait-Condition -Name "Outbound anchored batch" -Timeout $TimeoutSeconds -Condition {
        try {
            $state = Invoke-RestMethod -Method Get -Uri "http://localhost:5301/api/processing/elections/$ElectionId/state"
            return $null -ne $state.lastAnchoredBatch
        }
        catch {
            return $false
        }
    }
}

Wait-Condition -Name "Transparency snapshot available" -Timeout $TimeoutSeconds -Condition {
    try {
        $snap = Invoke-RestMethod -Method Get -Uri "http://localhost:5264/api/transparency/elections/$ElectionId/snapshot"
        return $null -ne $snap
    }
    catch {
        return $false
    }
}

$snapshot = Invoke-RestMethod -Method Get -Uri "http://localhost:5264/api/transparency/elections/$ElectionId/snapshot"
if ($FullCycle) {
    $candidateATally = [long]$snapshot.CandidateTallies.$candidateAKey
    $candidateBTally = [long]$snapshot.CandidateTallies.$candidateBKey
    $recordedTotal = $candidateATally + $candidateBTally

    if ($recordedTotal -ne $submittedUsers.Count) {
        throw "Transparency snapshot tally total does not match submitted ballots. Expected $($submittedUsers.Count), got $recordedTotal."
    }
}

$portalResponse = Invoke-WebRequest -UseBasicParsing -Uri "$PortalBaseUrl/Results?ElectionId=$ElectionId" -TimeoutSec 10
if ($portalResponse.StatusCode -lt 200 -or $portalResponse.StatusCode -ge 300) {
    throw "Result portal did not return a successful response. Status=$($portalResponse.StatusCode)"
}

if ($FullCycle) {
    if ($portalResponse.Content -notmatch [regex]::Escape($ElectionId)) {
        throw "Result portal page does not mention the election id $ElectionId."
    }

    if (
        $portalResponse.Content -notmatch [regex]::Escape($candidateAKey) -or
        $portalResponse.Content -notmatch [regex]::Escape([string]$candidateACount) -or
        $portalResponse.Content -notmatch [regex]::Escape($candidateBKey) -or
        $portalResponse.Content -notmatch [regex]::Escape([string]$candidateBCount)
    ) {
        throw "Result portal page does not show finalized candidate tallies for the full-cycle election."
    }
}

$watcherHeaders = @{ "X-Operator-Key" = "watcher-operator-key" }
$auditRequest = @{ requestedBy = "local-e2e-smoke" } | ConvertTo-Json
$audit = Invoke-RestMethod -Method Post -Uri "http://localhost:5161/api/watcher/elections/$ElectionId/run-audit" -Headers $watcherHeaders -ContentType "application/json" -Body $auditRequest

Write-Host ""
Write-Host "Smoke test completed successfully."
Write-Host "ElectionId: $ElectionId"
Write-Host "VID: $($firstSubmittedProfile.vid)"
if ($FullCycle) {
    Write-Host "Final tally: cand-a=$candidateACount cand-b=$candidateBCount"
}
Write-Host "Watcher status: $($audit.status)"
Write-Host "Watcher reportId: $($audit.reportId)"

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$solutions = @(
    @{ Name = "Configuration"; Path = ".\Configuration\ConfigurationComponent.sln" },
    @{ Name = "CynetAuth"; Path = ".\CynetAuth\CynetAuth.sln" },
    @{ Name = "Authorization"; Path = ".\Authorization\ElectionAuthorization.sln" },
    @{ Name = "Voter"; Path = ".\Voter\VoterComponent\VoterComponent.sln" },
    @{ Name = "ElectionProcessing"; Path = ".\ElectionProcessing\ElectionProcessing\ElectionProcessing.sln" },
    @{ Name = "Transparency"; Path = ".\Transparency\TransparencyAudit.sln" },
    @{ Name = "Result"; Path = ".\Result\ResultPortal.sln" },
    @{ Name = "Watcher"; Path = ".\Watcher\WatcherAudit.sln" },
    @{ Name = "Decryption"; Path = ".\Decryption\ThresholdDecryption.sln" }
)

foreach ($solution in $solutions) {
    Write-Host "Running $($solution.Name)..."
    dotnet test $solution.Path --configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

Write-Host "All component test suites completed successfully."

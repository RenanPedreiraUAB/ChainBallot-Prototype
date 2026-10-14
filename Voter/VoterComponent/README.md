# Voter Component

The Voter Component is the inbound, server-rendered ballot workspace for the current ChainBallot implementation.

Responsibilities:

- Authenticate voters through CynetAuth.
- Store the signed-in state in an encrypted HTTP-only cookie.
- Resolve VID and election authorization state through the Election Authorization component.
- Load election metadata when available for ballot rendering.
- Encrypt ballot submissions with AES-GCM.
- Compute a SHA-256 commitment for the submission bundle.
- Durably stage the submission package in the outbox BEFORE consuming the one-time authorization grant, so no voting right is ever consumed without a recoverable record of the submission it authorizes.
- Consume the authorization grant, then publish the already-staged package to Kafka as the canonical prototype queue transport.
- Recover staged/authorized entries automatically (including re-attempting grant consumption for entries staged but not yet authorized) and expose outbox operator endpoints for state inspection, manual retry, compaction, and targeted removal.

Implementation notes:

- The component is a Razor Pages app on .NET 9.
- Auth, authorization, metadata, queue, encryption, and commitment concerns are separated behind local service abstractions.
- Queue publishing is Kafka-first in runtime configuration; the in-memory publisher remains available only as a test/local fallback mode.
- The outbox is part of the normal submission path (staged before authorization consumption, marked authorized before publish, removed only once publish is confirmed) — not merely a fallback created after a publish failure. Outbox governance supports TTL retention, max retry count, and max capacity policies.
- Integration tests cover login and ballot submission.

Run the app:

```powershell
Set-Location D:\GitHub\ChainBallot
dotnet run --project .\Voter\VoterComponent\VoterComponent.Web\VoterComponent.Web.csproj --no-restore
```

Run tests:

```powershell
Set-Location D:\GitHub\ChainBallot
dotnet test .\Voter\VoterComponent\VoterComponent.sln --no-restore
```
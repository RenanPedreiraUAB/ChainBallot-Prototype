# Election Authorization Component

This component implements the Election Authorization boundary from Architecture V2.

Responsibilities:

- Trust and validate identity tokens from the external TTP authenticator (CynetAuth).
- Create and retrieve a stable VID for authenticated subjects.
- Maintain election-level eligibility and vote-consumption state.
- Issue one-time vote authorization grants.
- Consume grants atomically to prevent multi-tab/device double voting races.
- Support controlled eligibility seeding for local scenarios.
- Emit local audit events for grant and eligibility decisions.

## Internal Structure Pattern

- `Application/` - dependency registration and startup wiring.
- `Data/` - EF Core entities and database context.
- `Repositories/` - persistence abstractions and implementations.
- `Services/` - authorization business logic.
- `Security/` - trusted JWT and VID generation utilities.
- `Endpoints/` - HTTP endpoint mapping.

## Endpoints

- `GET /api/authorization/profile`
- `GET /api/authorization/elections/{electionId}/state`
- `POST /api/authorization/elections/{electionId}/grant`
- `POST /api/authorization/elections/{electionId}/consume`
- `POST /api/authorization/admin/elections/{electionId}/eligibility` (requires `X-Admin-Seed-Key`)
- `GET /health`

## Behavior Notes

- Grant consumption supports idempotent replay for the same `(grantId, submissionNonce)` pair.
- Parallel consume attempts still allow only one successful vote consumption.
- Authorization decisions are persisted to a local `authorization_audit_events` table.

## Environment Overrides

- `CHAINBALLOT_AUTHZ_TRUSTEDAUTH_SIGNING_KEY` overrides `TrustedAuth:SigningKey`.

## Run

From repository root:

```powershell
dotnet run --project .\Authorization\ElectionAuthorization.Api\ElectionAuthorization.Api.csproj --no-restore
```

## Test

From repository root:

```powershell
dotnet test .\Authorization\ElectionAuthorization.sln --no-restore
```

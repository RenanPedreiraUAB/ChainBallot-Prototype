# CynetAuth

CynetAuth is the Trusted Third Party authenticator used in the current Chain Ballot implementation.

Scope for this component:

- User registration with password hashing (PBKDF2).
- Username/password login with JWT issuance.
- Authenticated identity endpoint used by downstream middleware.

## Internal Structure Pattern

To stay consistent with other Chain Ballot components, CynetAuth follows a layered structure:

- `Application/` - dependency registration and startup wiring.
- `Contracts/` - API request/response contracts.
- `Data/` - EF Core entities and database context.
- `Repositories/` - persistence abstractions and implementations.
- `Services/` - authentication business logic.
- `Security/` - hashing and token services.
- `Endpoints/` - HTTP endpoint mapping.

Out of scope for current implementation slice:

- External social identity providers.
- Multi-factor authentication.
- Enterprise-grade account recovery flows.

## API Endpoints

- `POST /api/auth/register`
- `POST /api/auth/login`
- `GET /api/auth/me` (requires Bearer token)
- `GET /health`

## Run

From repository root:

```powershell
dotnet run --project .\CynetAuth\CynetAuth.Api\CynetAuth.Api.csproj --no-restore
```

## Test

From repository root:

```powershell
dotnet test .\CynetAuth\CynetAuth.sln --no-restore
```

## Security notes

- Passwords are hashed with PBKDF2-SHA256 and per-user random salt.
- JWT includes `sub`, `unique_name`, `amr`, and `auth_time` claims.
- Usernames are normalized for case-insensitive uniqueness.

Environment override:

- `CHAINBALLOT_CYNETAUTH_JWT_SIGNING_KEY` overrides `Jwt:SigningKey`.

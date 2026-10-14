# Result Portal

This component provides the user-facing read surface for election result publication and VID verification.

## Projects

- `ResultPortal.Web` - ASP.NET Core Razor Pages app.
- `ResultPortal.Web.Tests` - unit tests for Transparency API client behavior.

## Current Scope

Implemented:

- Verified result snapshot page (`/Results`) backed by Transparency API.
- VID verification page (`/Verify`) backed by Transparency API.
- Typed client integration for `VerifiedResultSnapshot` and `VidVerificationResponse` contracts.
- Runtime configuration for Transparency endpoint base URL and timeout.
- Configurable election visibility policy (public/private by election id).
- Configurable authentication mode (`Open`, `HeaderToken`, or `Oidc`) for staged access-control rollout.
- Role-gated access for private elections with optional election existence hiding.
- Improved UX messaging for not-found/unavailable/unauthorized read states.
- Component integration tests for page access-control behavior.
- OIDC-mode challenge flow for private-election access in production identity mode.

Not yet implemented:

- End-to-end portal integration tests against a live Transparency API host.
- End-to-end validation against a live enterprise identity provider (current OIDC mode is configuration-driven and test-validated).

## Configuration

`ResultPortal` section:

- `TransparencyBaseUrl`
- `TransparencyTimeoutSeconds`
- `AuthMode` (`Open`, `HeaderToken`, or `Oidc`)
- `HeaderAuthHeaderName`
- `HeaderAuthToken`
- `HeaderAuthRoles`
- `OidcAuthority`
- `OidcClientId`
- `OidcClientSecret`
- `OidcResponseType`
- `OidcCallbackPath`
- `OidcSignedOutCallbackPath`
- `OidcRequireHttpsMetadata`
- `OidcUsePkce`
- `OidcScopes`
- `DefaultElectionVisibility` (`Public` or `Private`)
- `PublicElectionIds`
- `PrivateElectionIds`
- `PrivateElectionViewerRole`
- `HidePrivateElectionExistence`

Environment override prefix is `CHAINBALLOT_`.

## Run

From repository root:

```powershell
Set-Location D:\GitHub\ChainBallot
dotnet run --project .\Result\ResultPortal.Web\ResultPortal.Web.csproj --no-restore
```

## Test

From repository root:

```powershell
Set-Location D:\GitHub\ChainBallot
dotnet test .\Result\ResultPortal.sln --no-restore
```

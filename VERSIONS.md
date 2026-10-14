# Dependency & Runtime Versions

Single-page reference for every pinned dependency version used by the prototype. This complements, rather than replaces, the authoritative sources: `.csproj` files (NuGet packages) and `docker-compose.local.yml` (container images). If this file and a source file ever disagree, the source file wins — update this file to match.

## Runtime / SDK

| Dependency | Version | Source |
|---|---|---|
| .NET SDK / runtime | 9.0 | `Dockerfile.service` (`mcr.microsoft.com/dotnet/sdk:9.0`, `mcr.microsoft.com/dotnet/aspnet:9.0`); all component `.csproj` target frameworks |

## Container images (`docker-compose.local.yml`)

| Image | Version | Used by |
|---|---|---|
| `docker.redpanda.com/redpandadata/redpanda` | v24.1.8 | Kafka-compatible broker (`redpanda` service) |
| `node` | 22-alpine | Static election metadata server (`metadata` service) |
| `solanalabs/solana` | v1.18.26 | Local Solana test validator (`solana-test-validator` service) |
| `chainballot-local-*` (cynetauth, authorization, voter, outbound, transparency, result, watcher, decryption) | built locally from `Dockerfile.service` | Component services, see `docker-compose.local.yml` for per-service `PROJECT_PATH`/`APP_DLL` build args |

## Key NuGet packages

| Package | Version | Used by |
|---|---|---|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 9.0.17 | CynetAuth.Api, ElectionAuthorization.Api |
| `Microsoft.EntityFrameworkCore.Sqlite` / `.Design` | 9.0.8 | CynetAuth.Api, ElectionAuthorization.Api |
| `Confluent.Kafka` | 2.5.3 | ElectionProcessing.Api, VoterComponent.Web |
| `Avalonia` / `Avalonia.Desktop` / `Avalonia.Themes.Fluent` / `Avalonia.Fonts.Inter` | 12.0.5 | ConfigurationComponent |
| `Avalonia.Controls.DataGrid` | 12.0.1 | ConfigurationComponent |
| `AvaloniaUI.DiagnosticsSupport` | 2.2.3 | ConfigurationComponent |
| `xunit` | 2.9.2 | All `*.Tests` projects |
| `xunit.runner.visualstudio` | 2.8.2 | All `*.Tests` projects |
| `coverlet.collector` | 6.0.2 | All `*.Tests` projects |

## Data stores

| Store | Notes |
|---|---|
| SQLite (via EF Core) | Only relational store used in the prototype (CynetAuth, Election Authorization). No PostgreSQL/SQL Server dependency. |
| File/JSON-backed projection stores | Transparency, Watcher, Result Portal — see `docs/Configuration-Reference.md` for storage path configuration. |

## Versioning note

This repository does not yet carry a release tag. A tagged/pinned commit corresponding to a specific external document revision will be established once a final companion repository state is produced.

# Contributing to Chain Ballot

Thanks for contributing.

## 1. Prerequisites

- .NET 9 SDK
- Docker Desktop
- PowerShell

## 2. Development Flow

1. Fork and create a feature branch.
2. Keep changes focused and scoped.
3. Update documentation in the same change when behavior changes.
4. Run validation before opening a pull request.

## 3. Required Validation Before PR

Run from repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test-all.ps1
```

For integration-impacting changes, also run:

```powershell
docker compose -f docker-compose.local.yml up --build -d
powershell -ExecutionPolicy Bypass -File .\docker\e2e-smoke.ps1 -SkipDockerUp
```

## 4. Documentation Requirements

If you touch architecture or flow behavior, update all impacted docs:

- `Architecture.md`
- `Prototype.md`
- `Test Plan and Validation.md`
- `docs/Traceability-Matrix.md`
- `docs/API-Reference.md`
- `docs/Configuration-Reference.md`
- `docs/Diagrams-Guide.md`

If UIS behavior changes, regenerate and replace the corresponding diagram in `DIAGRAMS/`.

## 5. Coding Guidelines

- Keep public APIs and contracts explicit and documented.
- Prefer additive changes over breaking changes.
- Keep comments meaningful; avoid redundant comments.
- Keep configuration names consistent with existing conventions.

## 6. Pull Request Checklist

- [ ] Tests pass locally.
- [ ] New behavior is covered by tests or justified.
- [ ] Documentation is updated.
- [ ] Breaking changes are clearly called out.

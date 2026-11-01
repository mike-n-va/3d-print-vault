# 3D Print Vault

Plex for STL files.

3D Print Vault is a desktop application for organizing, tracking, searching, and visualizing large collections of 3D printing models.

## Status

Early development. The solution structure, dependency injection bootstrap, and test foundations are in place. Feature work is beginning.

## Planned Features

- Artist tracking
- Collection management
- Thumbnail generation
- Interactive 3D viewer
- Print tracking
- Duplicate detection
- Advanced search

## Technology

- C# / .NET 10
- Avalonia UI
- SQLite
- xUnit v3 with ArchUnitNET architecture tests

## Building

Requires the .NET 10 SDK. The SDK version is pinned in `global.json`.

```bash
dotnet build DPV.sln
dotnet test DPV.sln
```

## Solution Layout

| Project | Purpose |
| --- | --- |
| `src/DPV.Domain` | Entities, value objects, and contracts |
| `src/DPV.Infrastructure` | Persistence, search, thumbnails, and import |
| `src/DPV.UI` | Avalonia views and view models |
| `src/DPV.App` | Composition root and host |
| `tests/DPV.Tests` | Unit, integration, and architecture tests |

## Documentation

Process and architecture documents live under `docs/`. `DEVELOPMENT_STANDARD.md` is the governing document for all development work.

## License

[MIT](LICENSE)

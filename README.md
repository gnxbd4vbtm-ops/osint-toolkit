# OsintToolkit

A modular Open Source Intelligence (OSINT) CLI and GUI toolkit built with C# and .NET 10. Supports scanning targets (usernames, domains, IPs, emails), extensible modules, SQLite storage, and multi-format export (JSON, CSV, Markdown, HTML).

## Quick Start

Prerequisites
- .NET 10 SDK
- On Linux (Wayland): install common Wayland/Skia dependencies (examples below)

Build
```bash
dotnet build OsintToolkit.slnx
```

CLI
- Run interactive CLI:
```bash
dotnet run --project src/OsintToolkit.CLI
```
- Run a scan (example):
```bash
dotnet run --project src/OsintToolkit.CLI -- scan --target example.com
```
- Export a session to HTML/JSON/CSV/Markdown:
```bash
dotnet run --project src/OsintToolkit.CLI -- export --session <SESSION_ID> --format html
```

HTML report details
- Exports include all module `RawDataJson` payloads; the HTML report contains structured findings and a styled dashboard.

Privacy & API usage
- IP geolocation uses `ip-api.com` (no API key) by default; it is free but rate-limited. For heavy use, configure a paid geolocation provider and add the API key into `config.json` and extend `IpInfoModule` to use it.

Development notes
- The `src/OsintToolkit.Modules/Implementations/IpInfoModule.cs` uses a public geolocation API to populate `GeoLocation` dynamically.
- The exporter `src/OsintToolkit.Services/Services/ExportService.cs` supports `json`, `csv`, `markdown`, and `html` formats.

Cleaning temporary tools
- Any temporary helper projects created during development have been removed from `src/`.

Contributing
- Add modules by implementing `IOsintModule` and registering them in `App.axaml.cs` (or via DI configuration).

License
- See [LICENSE](LICENSE)

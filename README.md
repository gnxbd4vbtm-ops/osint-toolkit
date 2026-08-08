# C# OSINT Toolkit CLI Framework

A modular, extensible Open Source Intelligence (OSINT) command-line framework built with C# and .NET 10. Designed with Clean Architecture principles to simplify adding new intelligence gathering tools, tracking targets, persisting scan session findings to SQLite, and exporting reports.

---

## 🌟 Key Features

* **Clean Architecture Layout**: Strict separation of concerns between Domain Core (`OsintToolkit.Core`), Data Layer (`OsintToolkit.Data`), Business Services (`OsintToolkit.Services`), Modules Engine (`OsintToolkit.Modules`), and Console CLI (`OsintToolkit.CLI`).
* **Target Management**: Supports tracking and auto-detecting multiple target types:
  * 👤 `Username`
  * 🌐 `Domain`
  * 📡 `IpAddress`
  * 📧 `Email`
  * 🧑 `Person`
* **Plugin / Module Architecture**: Standardized `IOsintModule` contract for self-registering OSINT gatherers.
* **SQLite Persistence**: Entity Framework Core SQLite database storing targets, scan execution history, and standardized findings.
* **Multi-Format Export Engine**: Generates formatted report output in `JSON`, `CSV`, and `Markdown` (`.md`) formats.
* **Rich Terminal UI**: Built with `Spectre.Console` featuring ASCII banner, interactive prompts, colorized status badges, progress indicators, and formatted tables.
* **Dual Operation Modes**:
  * **Interactive Mode**: Guided terminal navigation menu.
  * **Direct Command Mode**: Scriptable execution via command flags (`--target`, `--type`, `--session`, `--format`).
* **Configuration Management**: Persistent `config.json` supporting API key placeholders, log levels, and export defaults.

---

## 📁 Project Structure

```
OsintToolkit/
├── OsintToolkit.sln
├── src/
│   ├── OsintToolkit.Core/             # Models, Enums, Module & Service Interfaces
│   │   ├── Enums/ (TargetType, ScanStatus, ResultSeverity)
│   │   ├── Interfaces/ (IOsintModule, IModuleRegistry, ITargetService, etc.)
│   │   └── Models/ (Target, ScanSession, ScanResult, AppConfig, ModuleResult)
│   ├── OsintToolkit.Data/             # Entity Framework Core DbContext & SQLite Schema
│   │   └── Context/ (OsintDbContext)
│   ├── OsintToolkit.Services/         # Business Logic & Infrastructure
│   │   ├── Services/ (TargetService, ScanSessionService, ExportService, ConfigService, ModuleRegistry)
│   │   └── Utilities/ (TargetValidator)
│   ├── OsintToolkit.Modules/          # Extensible OSINT Tool Plugins
│   │   ├── Base/ (BaseOsintModule)
│   │   └── Implementations/
│   │       ├── UsernameLookupModule.cs
│   │       ├── DomainInfoModule.cs
│   │       ├── IpInfoModule.cs
│   │       └── EmailInfoModule.cs
│   └── OsintToolkit.CLI/              # Terminal Interface & Command Handlers
│       ├── Program.cs
│       ├── UI/ (Banner, ConsoleRenderer, InteractiveMenu)
│       ├── Commands/ (AppInfo, CommandHandler)
│       └── appsettings.json
├── exports/                           # Generated report output directory
├── config.json                        # Application settings file
└── README.md
```

---

## 🚀 Quick Setup & Execution Instructions

### Prerequisites
* [.NET 8, 9, or 10 SDK](https://dotnet.microsoft.com/download) installed.

### 1. Build the Solution
```bash
dotnet build
```

### 2. Run Interactive Mode
Launch the application with no parameters to open the Spectre.Console interactive menu:
```bash
dotnet run --project src/OsintToolkit.CLI
```

### Run the Linux GUI (KDE Wayland / CachyOS)
```bash
dotnet run --project src/OsintToolkit.WPF
```

The GUI uses Avalonia's stable Linux desktop backend by default (works on KDE
Wayland through XWayland). To opt in to its experimental native Wayland backend:
`OSINT_NATIVE_WAYLAND=1 dotnet run --project src/OsintToolkit.WPF`.

### 3. Run Direct Commands (Non-Interactive / Scripting)

* **Display Version & Info**:
  ```bash
  dotnet run --project src/OsintToolkit.CLI -- --version
  ```

* **Display Help**:
  ```bash
  dotnet run --project src/OsintToolkit.CLI -- --help
  ```

* **List Registered Modules**:
  ```bash
  dotnet run --project src/OsintToolkit.CLI -- modules
  ```

* **List Saved Targets**:
  ```bash
  dotnet run --project src/OsintToolkit.CLI -- targets
  ```

* **Run an OSINT Scan on a Domain**:
  ```bash
dotnet run --project src/OsintToolkit.CLI -- scan --target example.com --type Domain
```

* **Run an Authorized Nmap Scan** (requires local `nmap`):
```bash
# Host discovery only
dotnet run --project src/OsintToolkit.CLI -- scan --target 192.0.2.10 --type IpAddress --nmap-profile Discovery

# Service/version scan of the top 100 ports
dotnet run --project src/OsintToolkit.CLI -- scan --target example.com --type Domain --nmap-profile Quick
```

Available profiles are `Discovery`, `Quick`, `Standard` (top 1000 TCP ports), and
`FullTcp` (all TCP ports). Only scan systems you are authorized to assess.

* **Inspect hosts already present on the local network ARP table**:
```bash
dotnet run --project src/OsintToolkit.CLI -- arp hosts --localnet --resolve
```
This command does not scan a subnet; it lists IPv4 neighbors your Linux host has
already observed and optionally performs reverse DNS lookups.

* **Run an OSINT Scan with Target Type Auto-Detection**:
  ```bash
  dotnet run --project src/OsintToolkit.CLI -- scan --target user123
  dotnet run --project src/OsintToolkit.CLI -- scan --target 8.8.8.8
  dotnet run --project src/OsintToolkit.CLI -- scan --target user@domain.com
  ```

* **Export Scan Session Results**:
  ```bash
  dotnet run --project src/OsintToolkit.CLI -- export --session 1 --format json
  dotnet run --project src/OsintToolkit.CLI -- export --session 1 --format csv
  dotnet run --project src/OsintToolkit.CLI -- export --session 1 --format markdown
  ```

* **View Configuration Settings**:
  ```bash
  dotnet run --project src/OsintToolkit.CLI -- config
  ```

---

## 🧩 How to Create a New OSINT Module

To add a new tool to the toolkit (e.g. Shodan lookups, WHOIS scrapers, social media checkers), create a class in `OsintToolkit.Modules` that inherits from `BaseOsintModule`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using OsintToolkit.Core.Enums;
using OsintToolkit.Core.Models;
using OsintToolkit.Modules.Base;

public class CustomOsintModule : BaseOsintModule
{
    public override string Name => "CustomModule";
    public override string Description => "Brief description of findings gathered by this module.";
    public override string Category => "Custom Recon";
    public override TargetType[] SupportedTypes => new[] { TargetType.Domain, TargetType.IpAddress };

    protected override async Task<ModuleResult> ExecuteInternalAsync(string targetValue, TargetType targetType, CancellationToken cancellationToken)
    {
        // 1. Implement API request, DNS lookup, or scraping logic here
        await Task.Delay(100, cancellationToken);

        // 2. Return standardized ModuleResult
        return ModuleResult.Success(
            Name,
            $"Custom Scan Result for {targetValue}",
            $"Gathered intelligence for target {targetValue}.",
            new { CustomField = "Sample Intelligence Data" },
            ResultSeverity.Info
        );
    }
}
```

Then register the module in `Program.cs`:
```csharp
services.AddScoped<IOsintModule, CustomOsintModule>();
```

---

## 🛡️ Initial Placeholder Modules

1. **`UsernameLookupModule`** (Target: `Username`, `Person`): Checks presence across developer and social media platforms.
2. **`DomainInfoModule`** (Target: `Domain`): Gathers DNS records (A, AAAA, MX, TXT, NS), WHOIS registrar metadata, and SSL status.
3. **`IpInfoModule`** (Target: `IpAddress`): Resolves IP geolocation, ASN information, ISP infrastructure, and open port highlights.
4. **`EmailInfoModule`** (Target: `Email`): Performs email format validation, MX record reachability check, disposable domain detection, and breach flags.

---

## 📜 License
MIT License.

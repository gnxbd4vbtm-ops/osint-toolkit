# C# OSINT Toolkit CLI Framework

A modular and extensible Open Source Intelligence (OSINT) command-line framework built with C# and .NET 10. The toolkit is designed around Clean Architecture and provides a structured way to add intelligence-gathering modules, manage targets, store scan results in SQLite, and export reports.

## Key Features

* **Clean Architecture**: Separates the project into Domain Core (`OsintToolkit.Core`), Data (`OsintToolkit.Data`), Services (`OsintToolkit.Services`), Modules (`OsintToolkit.Modules`), and CLI (`OsintToolkit.CLI`).
* **Target Management**: Supports multiple target types:

  * `Username`
  * `Domain`
  * `IpAddress`
  * `Email`
  * `Person`
* **Module System**: Uses a standardized `IOsintModule` interface for adding new OSINT tools.
* **SQLite Persistence**: Uses Entity Framework Core with SQLite to store targets, scan sessions, and findings.
* **Report Export**: Export scan results as `JSON`, `CSV`, or `Markdown`.
* **Terminal UI**: Uses `Spectre.Console` for interactive menus, status output, progress indicators, and tables.
* **Two Operation Modes**:

  * **Interactive Mode**: Navigate the toolkit through a terminal menu.
  * **Direct Command Mode**: Run commands directly using options such as `--target`, `--type`, `--session`, and `--format`.
* **Configuration**: Stores application settings in `config.json`, including API key placeholders, logging settings, and export defaults.

## Project Structure

```text
OsintToolkit/
├── OsintToolkit.sln
├── src/
│   ├── OsintToolkit.Core/             # Models, enums, and interfaces
│   │   ├── Enums/                     # TargetType, ScanStatus, ResultSeverity
│   │   ├── Interfaces/                # Module and service interfaces
│   │   └── Models/                    # Target, ScanSession, ScanResult, etc.
│   │
│   ├── OsintToolkit.Data/             # EF Core and SQLite
│   │   └── Context/
│   │       └── OsintDbContext.cs
│   │
│   ├── OsintToolkit.Services/         # Business logic and infrastructure
│   │   ├── Services/                  # Target, scan, export, config, module services
│   │   └── Utilities/                 # Target validation
│   │
│   ├── OsintToolkit.Modules/          # OSINT modules
│   │   ├── Base/
│   │   │   └── BaseOsintModule.cs
│   │   └── Implementations/
│   │       ├── UsernameLookupModule.cs
│   │       ├── DomainInfoModule.cs
│   │       ├── IpInfoModule.cs
│   │       └── EmailInfoModule.cs
│   │
│   └── OsintToolkit.CLI/              # Command-line interface
│       ├── Program.cs
│       ├── UI/
│       ├── Commands/
│       └── appsettings.json
│
├── exports/                           # Generated reports
├── config.json                        # Application configuration
└── README.md
```

## Setup

### Prerequisites

* [.NET 8, 9, or 10 SDK](https://dotnet.microsoft.com/download)
* `nmap` is required if you want to use the Nmap integration.

### Build

Clone the repository, then build the solution:

```bash
dotnet build
```

### Run Interactive Mode

Run the CLI without any arguments to open the interactive menu:

```bash
dotnet run --project src/OsintToolkit.CLI
```

### Run the Linux GUI

The project also includes an Avalonia-based GUI:

```bash
dotnet run --project src/OsintToolkit.WPF
```

The GUI uses Avalonia's stable Linux desktop backend by default and works on KDE Wayland through XWayland.

To opt in to the experimental native Wayland backend:

```bash
OSINT_NATIVE_WAYLAND=1 dotnet run --project src/OsintToolkit.WPF
```

## CLI Usage

### Version

```bash
dotnet run --project src/OsintToolkit.CLI -- --version
```

### Help

```bash
dotnet run --project src/OsintToolkit.CLI -- --help
```

### List Registered Modules

```bash
dotnet run --project src/OsintToolkit.CLI -- modules
```

### List Saved Targets

```bash
dotnet run --project src/OsintToolkit.CLI -- targets
```

### Run an OSINT Scan

For example, to scan a domain:

```bash
dotnet run --project src/OsintToolkit.CLI -- scan --target example.com --type Domain
```

The target type can also be detected automatically:

```bash
dotnet run --project src/OsintToolkit.CLI -- scan --target user123
dotnet run --project src/OsintToolkit.CLI -- scan --target 8.8.8.8
dotnet run --project src/OsintToolkit.CLI -- scan --target user@domain.com
```

### Nmap Integration

Authorized Nmap scans can be run when `nmap` is installed locally.

Host discovery:

```bash
dotnet run --project src/OsintToolkit.CLI -- scan --target 192.0.2.10 --type IpAddress --nmap-profile Discovery
```

Service and version scan of the top 100 ports:

```bash
dotnet run --project src/OsintToolkit.CLI -- scan --target example.com --type Domain --nmap-profile Quick
```

Available profiles:

* `Discovery`
* `Quick`
* `Standard` - top 1000 TCP ports
* `FullTcp` - all TCP ports

Only use the Nmap functionality against systems you are authorized to assess.

### Inspect Local ARP Hosts

The toolkit can inspect IPv4 neighbors already present in the local ARP table:

```bash
dotnet run --project src/OsintToolkit.CLI -- arp hosts --localnet --resolve
```

This does not scan the subnet. It only lists hosts that the Linux system has already observed and can optionally perform reverse DNS lookups.

### Export Scan Results

Export a scan session as JSON:

```bash
dotnet run --project src/OsintToolkit.CLI -- export --session 1 --format json
```

Export as CSV:

```bash
dotnet run --project src/OsintToolkit.CLI -- export --session 1 --format csv
```

Export as Markdown:

```bash
dotnet run --project src/OsintToolkit.CLI -- export --session 1 --format markdown
```

### View Configuration

```bash
dotnet run --project src/OsintToolkit.CLI -- config
```

## Creating a New OSINT Module

The module system is designed so that additional tools can be added without changing the core application.

For example, a new module could handle Shodan lookups, WHOIS queries, DNS enumeration, or other OSINT sources.

Create a class in `OsintToolkit.Modules` that inherits from `BaseOsintModule`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using OsintToolkit.Core.Enums;
using OsintToolkit.Core.Models;
using OsintToolkit.Modules.Base;

public class CustomOsintModule : BaseOsintModule
{
    public override string Name => "CustomModule";
    public override string Description =>
        "Brief description of the information gathered by this module.";

    public override string Category => "Custom Recon";

    public override TargetType[] SupportedTypes =>
        new[] { TargetType.Domain, TargetType.IpAddress };

    protected override async Task<ModuleResult> ExecuteInternalAsync(
        string targetValue,
        TargetType targetType,
        CancellationToken cancellationToken)
    {
        // Implement API requests, DNS lookups, scraping, etc. here.
        await Task.Delay(100, cancellationToken);

        return ModuleResult.Success(
            Name,
            $"Custom Scan Result for {targetValue}",
            $"Gathered intelligence for target {targetValue}.",
            new
            {
                CustomField = "Sample Intelligence Data"
            },
            ResultSeverity.Info
        );
    }
}
```

Register the module in `Program.cs`:

```csharp
services.AddScoped<IOsintModule, CustomOsintModule>();
```

Once registered, the module becomes available to the toolkit through the module registry.

## Included Modules

### UsernameLookupModule

**Targets:** `Username`, `Person`

Checks for the presence of a username across supported developer and social media platforms.

### DomainInfoModule

**Target:** `Domain`

Collects domain information such as:

* A and AAAA records
* MX records
* TXT records
* NS records
* WHOIS registrar information
* SSL/TLS status

### IpInfoModule

**Target:** `IpAddress`

Collects information such as:

* IP geolocation
* ASN information
* ISP information
* Infrastructure details
* Open port highlights

### EmailInfoModule

**Target:** `Email`

Performs checks including:

* Email format validation
* MX record validation
* Disposable email domain detection
* Available breach indicators

## License

MIT License.

using System.Threading.Tasks;
using OsintToolkit.Core.Models;

namespace OsintToolkit.Core.Interfaces;

/// <summary>
/// Service for exporting scan results to various formats (JSON, CSV, Markdown).
/// </summary>
public interface IExportService
{
    Task<string> ExportScanSessionAsync(ScanSession session, string format, string outputDirectory = "exports");
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OsintToolkit.Core.Models;

namespace OsintToolkit.Core.Interfaces;

/// <summary>
/// Service for running scans and managing scan sessions and results.
/// </summary>
public interface IScanSessionService
{
    Task<ScanSession> CreateSessionAsync(int targetId, string notes = "");
    Task<ScanSession?> GetSessionByIdAsync(int id);
    Task<List<ScanSession>> GetAllSessionsAsync();
    Task<List<ScanSession>> GetSessionsByTargetIdAsync(int targetId);
    Task<ScanSession> ExecuteScanAsync(int targetId, IEnumerable<string>? selectedModuleNames = null, CancellationToken cancellationToken = default, IProgress<ScanProgress>? progress = null);
    Task<bool> DeleteSessionAsync(int id);
}

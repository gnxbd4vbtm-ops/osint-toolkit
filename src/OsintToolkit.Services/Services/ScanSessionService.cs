using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OsintToolkit.Core.Enums;
using OsintToolkit.Core.Interfaces;
using OsintToolkit.Core.Models;
using OsintToolkit.Data.Context;

namespace OsintToolkit.Services.Services;

/// <summary>
/// Executes OSINT scans, coordinates modules, and persists findings to SQLite.
/// </summary>
public class ScanSessionService : IScanSessionService
{
    private readonly OsintDbContext _dbContext;
    private readonly IModuleRegistry _moduleRegistry;
    private readonly ILogger<ScanSessionService> _logger;

    public ScanSessionService(
        OsintDbContext dbContext,
        IModuleRegistry moduleRegistry,
        ILogger<ScanSessionService> logger)
    {
        _dbContext = dbContext;
        _moduleRegistry = moduleRegistry;
        _logger = logger;
    }

    public async Task<ScanSession> CreateSessionAsync(int targetId, string notes = "")
    {
        var target = await _dbContext.Targets.FindAsync(targetId);
        if (target == null)
            throw new InvalidOperationException($"Target with ID {targetId} not found.");

        var session = new ScanSession
        {
            TargetId = targetId,
            Status = ScanStatus.Pending,
            StartedAt = DateTime.UtcNow,
            Notes = notes
        };

        _dbContext.ScanSessions.Add(session);
        await _dbContext.SaveChangesAsync();
        return session;
    }

    public async Task<ScanSession?> GetSessionByIdAsync(int id)
    {
        return await _dbContext.ScanSessions
            .Include(s => s.Target)
            .Include(s => s.Results)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<List<ScanSession>> GetAllSessionsAsync()
    {
        return await _dbContext.ScanSessions
            .Include(s => s.Target)
            .Include(s => s.Results)
            .OrderByDescending(s => s.StartedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<ScanSession>> GetSessionsByTargetIdAsync(int targetId)
    {
        return await _dbContext.ScanSessions
            .Include(s => s.Target)
            .Include(s => s.Results)
            .Where(s => s.TargetId == targetId)
            .OrderByDescending(s => s.StartedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<ScanSession> ExecuteScanAsync(int targetId, IEnumerable<string>? selectedModuleNames = null, CancellationToken cancellationToken = default, IProgress<ScanProgress>? progress = null)
    {
        var target = await _dbContext.Targets.FindAsync(targetId);
        if (target == null)
            throw new InvalidOperationException($"Target with ID {targetId} not found.");

        var session = await CreateSessionAsync(targetId, $"Scan for {target.Value} [{target.Type}]");
        session.Status = ScanStatus.Running;
        await _dbContext.SaveChangesAsync();

        // Select modules to run
        var availableModules = _moduleRegistry.GetModulesForTargetType(target.Type).ToList();

        if (selectedModuleNames != null && selectedModuleNames.Any())
        {
            var selectedSet = new HashSet<string>(selectedModuleNames, StringComparer.OrdinalIgnoreCase);
            availableModules = availableModules.Where(m => selectedSet.Contains(m.Name)).ToList();
        }

        _logger.LogInformation("Starting scan session #{SessionId} for '{TargetValue}' with {ModuleCount} modules", session.Id, target.Value, availableModules.Count);

        try
        {
            for (var index = 0; index < availableModules.Count; index++)
            {
                var module = availableModules[index];
                if (cancellationToken.IsCancellationRequested)
                {
                    session.Status = ScanStatus.Cancelled;
                    break;
                }

                progress?.Report(new ScanProgress
                {
                    CompletedModules = index,
                    TotalModules = availableModules.Count,
                    CurrentModule = module.Name,
                    Message = $"Running {module.Name}..."
                });
                _logger.LogInformation("Executing module '{ModuleName}'...", module.Name);
                var result = await module.ExecuteAsync(target.Value, target.Type, cancellationToken);

                if (result.IsSuccess)
                {
                    var scanResult = new ScanResult
                    {
                        ScanSessionId = session.Id,
                        ModuleName = result.ModuleName,
                        Title = result.Title,
                        Summary = result.Summary,
                        RawDataJson = JsonSerializer.Serialize(result.RawData, new JsonSerializerOptions { WriteIndented = true }),
                        Severity = result.Severity,
                        Timestamp = DateTime.UtcNow
                    };

                    session.Results.Add(scanResult);
                }
                else
                {
                    _logger.LogWarning("Module '{ModuleName}' returned error: {Error}", result.ModuleName, result.ErrorMessage);
                }

                progress?.Report(new ScanProgress
                {
                    CompletedModules = index + 1,
                    TotalModules = availableModules.Count,
                    CurrentModule = module.Name,
                    Message = $"Completed {module.Name}"
                });
            }

            if (session.Status != ScanStatus.Cancelled)
            {
                session.Status = ScanStatus.Completed;
            }

            session.CompletedAt = DateTime.UtcNow;
            target.LastScannedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            progress?.Report(new ScanProgress
            {
                CompletedModules = availableModules.Count,
                TotalModules = availableModules.Count,
                Message = $"Scan {session.Status.ToString().ToLowerInvariant()}."
            });
            _logger.LogInformation("Completed scan session #{SessionId} for '{TargetValue}'", session.Id, target.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan session #{SessionId} failed unexpectedly", session.Id);
            session.Status = ScanStatus.Failed;
            session.CompletedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }

        return session;
    }

    public async Task<bool> DeleteSessionAsync(int id)
    {
        var session = await _dbContext.ScanSessions.FindAsync(id);
        if (session == null) return false;

        _dbContext.ScanSessions.Remove(session);
        await _dbContext.SaveChangesAsync();
        return true;
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OsintToolkit.Core.Enums;
using OsintToolkit.Core.Interfaces;
using OsintToolkit.Core.Models;
using OsintToolkit.Data.Context;
using OsintToolkit.Services.Utilities;

namespace OsintToolkit.Services.Services;

/// <summary>
/// Service responsible for Target CRUD and management.
/// </summary>
public class TargetService : ITargetService
{
    private readonly OsintDbContext _dbContext;
    private readonly ILogger<TargetService> _logger;

    public TargetService(OsintDbContext dbContext, ILogger<TargetService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Target> CreateTargetAsync(string value, TargetType type, string description = "")
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Target value cannot be empty.", nameof(value));

        var existing = await GetByValueAsync(value.Trim());
        if (existing != null)
        {
            _logger.LogInformation("Target '{Value}' already exists with ID {Id}", existing.Value, existing.Id);
            return existing;
        }

        var target = new Target
        {
            Value = value.Trim(),
            Type = type,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Targets.Add(target);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Created new target '{Value}' [{Type}] with ID {Id}", target.Value, target.Type, target.Id);
        return target;
    }

    public async Task<Target?> GetByIdAsync(int id)
    {
        return await _dbContext.Targets
            .Include(t => t.ScanSessions)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Target?> GetByValueAsync(string value)
    {
        return await _dbContext.Targets
            .FirstOrDefaultAsync(t => t.Value.ToLower() == value.Trim().ToLower());
    }

    public async Task<List<Target>> GetAllTargetsAsync()
    {
        return await _dbContext.Targets
            .Include(t => t.ScanSessions)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<bool> DeleteTargetAsync(int id)
    {
        var target = await _dbContext.Targets.FindAsync(id);
        if (target == null) return false;

        _dbContext.Targets.Remove(target);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Deleted target ID {Id}", id);
        return true;
    }

    public TargetType DetectTargetType(string input)
    {
        return TargetValidator.DetectType(input);
    }
}

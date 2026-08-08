using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using OsintToolkit.Core.Interfaces;
using OsintToolkit.Core.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OsintToolkit.Gui;

public partial class MainWindow : Window
{
    private readonly ITargetService _targetService;
    private readonly IScanSessionService _scanSessionService;
    private readonly IExportService _exportService;

    public MainWindow()
    {
        InitializeComponent();

        // Resolve services from App host
        var services = App.Services ?? throw new InvalidOperationException("Services not initialized");
        _targetService = services.GetRequiredService<ITargetService>();
        _scanSessionService = services.GetRequiredService<IScanSessionService>();
        _exportService = services.GetRequiredService<IExportService>();

        ScanButton.Click += async (_, __) => await RunScanAsync();
        RefreshSessionsButton.Click += async (_, __) => await LoadSessionsAsync();
        ExportButton.Click += async (_, __) => await ExportSelectedAsync();

        // Load initial sessions
        _ = LoadSessionsAsync();
    }

    private async Task LoadSessionsAsync()
    {
        try
        {
            var sessions = await _scanSessionService.GetAllSessionsAsync();
            var recent = sessions.OrderByDescending(s => s.Id).Take(20).ToList();
            SessionsList.ItemsSource = recent.Select(s => new { Id = s.Id, Text = $"#{s.Id} - {s.Target?.Value ?? "?"} ({s.Results.Count} findings)" }).ToList();
        }
        catch (Exception ex)
        {
            OutputList.ItemsSource = new[] { $"Failed to load sessions: {ex.Message}" };
        }
    }

    private async Task RunScanAsync()
    {
        var targetText = TargetText.Text?.Trim();
        if (string.IsNullOrWhiteSpace(targetText))
        {
            OutputList.ItemsSource = new[] { "Please enter a target." };
            return;
        }

        try
        {
            OutputList.ItemsSource = new[] { "Starting scan..." };

            var detectedType = _targetService.DetectTargetType(targetText);
            var target = await _targetService.CreateTargetAsync(targetText, detectedType, "GUI scan");

            var session = await _scanSessionService.ExecuteScanAsync(target.Id);

            var lines = session.Results.Select(r => $"[{r.Severity}] {r.ModuleName}: {r.Summary}").ToArray();
            OutputList.ItemsSource = lines.Length > 0 ? lines : new[] { "Scan completed with no findings." };

            await LoadSessionsAsync();
        }
        catch (Exception ex)
        {
            OutputList.ItemsSource = new[] { $"Scan failed: {ex.Message}" };
        }
    }

    private async Task ExportSelectedAsync()
    {
        if (SessionsList.SelectedItem == null)
        {
            OutputList.ItemsSource = new[] { "Select a session to export." };
            return;
        }

        try
        {
            var sel = SessionsList.SelectedItem;
            // SelectedItem is an anonymous type with Id property
            var idProp = sel.GetType().GetProperty("Id");
            if (idProp == null) { OutputList.ItemsSource = new[] { "Could not determine session id." }; return; }
            var id = (int)idProp.GetValue(sel)!;

            var session = await _scanSessionService.GetSessionByIdAsync(id);
            if (session == null) { OutputList.ItemsSource = new[] { $"Session #{id} not found." }; return; }

            var path = await _exportService.ExportScanSessionAsync(session, "md");
            OutputList.ItemsSource = new[] { $"Exported to: {path}" };
        }
        catch (Exception ex)
        {
            OutputList.ItemsSource = new[] { $"Export failed: {ex.Message}" };
        }
    }
}

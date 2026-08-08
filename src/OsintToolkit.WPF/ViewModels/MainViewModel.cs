using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using OsintToolkit.Core.Enums;
using OsintToolkit.Core.Interfaces;
using OsintToolkit.Core.Models;
using OsintToolkit.WPF.Infrastructure;

namespace OsintToolkit.WPF.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfigService _config;
    private readonly INmapScanOptions _nmapOptions;
    private object _currentPage = null!;
    private string _statusMessage = "Ready";
    private string _newTargetValue = string.Empty;
    private TargetType _newTargetType = TargetType.Domain;
    private Target? _selectedTarget;
    private ScanSession? _selectedSession;
    private ScanResult? _selectedFinding;
    private string _findingSearch = string.Empty;
    private double _scanProgress;
    private string _currentModule = "No scan running";
    private bool _isScanning;
    private CancellationTokenSource? _scanCancellation;
    private string _databasePath = string.Empty;
    private string _logLevel = string.Empty;
    private string _defaultExportFormat = string.Empty;
    private string _theme = "Dark";
    private string _apiKeysText = string.Empty;
    private NmapScanProfile _selectedNmapProfile;

    public ObservableCollection<Target> Targets { get; } = new();
    public ObservableCollection<ScanSession> Sessions { get; } = new();
    public ObservableCollection<ScanResult> Findings { get; } = new();
    public Array TargetTypes { get; } = Enum.GetValues(typeof(TargetType));
    public Array ExportFormats { get; } = new[] { "json", "csv", "markdown", "html" };
    public Array Themes { get; } = new[] { "Dark", "Light", "System" };
    public Array NmapProfiles { get; } = Enum.GetValues(typeof(NmapScanProfile));
    public ObservableCollection<ScanResult> FilteredFindings { get; } = new();
    public DashboardPageViewModel Dashboard { get; }
    public TargetsPageViewModel TargetsPage { get; }
    public ScanPageViewModel ScanPage { get; }
    public ResultsPageViewModel ResultsPage { get; }
    public ReportsPageViewModel ReportsPage { get; }
    public SettingsPageViewModel SettingsPage { get; }
    public ICommand NavigateCommand { get; }
    public ICommand AddTargetCommand { get; }
    public ICommand StartScanCommand { get; }
    public ICommand CancelScanCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand RefreshCommand { get; }

    public MainViewModel(IServiceScopeFactory scopeFactory, IConfigService config, INmapScanOptions nmapOptions)
    {
        _scopeFactory = scopeFactory; _config = config; _nmapOptions = nmapOptions; _selectedNmapProfile = nmapOptions.Profile;
        Dashboard = new DashboardPageViewModel(this);
        TargetsPage = new TargetsPageViewModel(this);
        ScanPage = new ScanPageViewModel(this);
        ResultsPage = new ResultsPageViewModel(this);
        ReportsPage = new ReportsPageViewModel(this);
        SettingsPage = new SettingsPageViewModel(this);
        _currentPage = Dashboard;
        NavigateCommand = new RelayCommand(p => Navigate(p?.ToString()));
        AddTargetCommand = new AsyncRelayCommand(_ => AddTargetAsync());
        StartScanCommand = new AsyncRelayCommand(_ => StartScanAsync(), _ => SelectedTarget is not null && !IsScanning);
        CancelScanCommand = new RelayCommand(_ => _scanCancellation?.Cancel(), _ => IsScanning);
        ExportCommand = new AsyncRelayCommand(p => ExportAsync(p?.ToString() ?? DefaultExportFormat), _ => SelectedSession is not null);
        SaveSettingsCommand = new AsyncRelayCommand(_ => SaveSettingsAsync());
        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
    }

    public object CurrentPage { get => _currentPage; private set => SetProperty(ref _currentPage, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string NewTargetValue { get => _newTargetValue; set => SetProperty(ref _newTargetValue, value); }
    public TargetType NewTargetType { get => _newTargetType; set => SetProperty(ref _newTargetType, value); }
    public Target? SelectedTarget { get => _selectedTarget; set { if (SetProperty(ref _selectedTarget, value)) { ((AsyncRelayCommand)StartScanCommand).RaiseCanExecuteChanged(); } } }
    public ScanSession? SelectedSession { get => _selectedSession; set { if (SetProperty(ref _selectedSession, value)) { LoadFindings(value); ((AsyncRelayCommand)ExportCommand).RaiseCanExecuteChanged(); } } }
    public ScanResult? SelectedFinding { get => _selectedFinding; set { if (SetProperty(ref _selectedFinding, value)) OnPropertyChanged(nameof(FindingEvidence)); } }
    public string FindingSearch { get => _findingSearch; set { if (SetProperty(ref _findingSearch, value)) RefreshFilteredFindings(); } }
    public double ScanProgress { get => _scanProgress; private set => SetProperty(ref _scanProgress, value); }
    public string CurrentModule { get => _currentModule; private set => SetProperty(ref _currentModule, value); }
    public bool IsScanning { get => _isScanning; private set { if (SetProperty(ref _isScanning, value)) { ((AsyncRelayCommand)StartScanCommand).RaiseCanExecuteChanged(); ((RelayCommand)CancelScanCommand).RaiseCanExecuteChanged(); } } }
    public string DatabasePath { get => _databasePath; set => SetProperty(ref _databasePath, value); }
    public string LogLevel { get => _logLevel; set => SetProperty(ref _logLevel, value); }
    public string DefaultExportFormat { get => _defaultExportFormat; set => SetProperty(ref _defaultExportFormat, value); }
    public string Theme { get => _theme; set => SetProperty(ref _theme, value); }
    public string ApiKeysText { get => _apiKeysText; set => SetProperty(ref _apiKeysText, value); }
    public NmapScanProfile SelectedNmapProfile { get => _selectedNmapProfile; set => SetProperty(ref _selectedNmapProfile, value); }
    public string FindingEvidence => SelectedFinding?.RawDataJson ?? "Select a finding to inspect its collected evidence.";

    public async Task InitializeAsync()
    {
        DatabasePath = _config.Config.DatabasePath;
        LogLevel = _config.Config.LogLevel;
        DefaultExportFormat = _config.Config.DefaultExportFormat;
        Theme = _config.Config.Theme;
        ApiKeysText = string.Join(Environment.NewLine, _config.Config.ApiKeys.Select(pair => $"{pair.Key}={pair.Value}"));
        await RefreshAsync();
    }

    public void ReportStartupError(string message) => StatusMessage = $"Initialization error: {message}";

    private void Navigate(string? destination) => CurrentPage = destination switch
    {
        "Targets" => TargetsPage, "Scan" => ScanPage, "Results" => ResultsPage,
        "Reports" => ReportsPage, "Settings" => SettingsPage, _ => Dashboard
    };

    private async Task RefreshAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var targets = await scope.ServiceProvider.GetRequiredService<ITargetService>().GetAllTargetsAsync();
        var sessions = await scope.ServiceProvider.GetRequiredService<IScanSessionService>().GetAllSessionsAsync();
        Targets.Clear(); foreach (var target in targets.OrderByDescending(t => t.CreatedAt)) Targets.Add(target);
        Sessions.Clear(); foreach (var session in sessions) Sessions.Add(session);
        Dashboard.Update(targets, sessions);
        if (SelectedSession is null && Sessions.Count > 0) SelectedSession = Sessions[0];
        StatusMessage = $"Loaded {Targets.Count} targets and {Sessions.Count} scan sessions.";
    }

    private async Task AddTargetAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTargetValue)) { StatusMessage = "Enter a target value first."; return; }
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var target = await scope.ServiceProvider.GetRequiredService<ITargetService>().CreateTargetAsync(NewTargetValue, NewTargetType);
            NewTargetValue = string.Empty; SelectedTarget = target;
            await RefreshAsync(); Navigate("Targets"); StatusMessage = $"Target '{target.Value}' is ready to scan.";
        }
        catch (Exception ex) { StatusMessage = $"Could not add target: {ex.Message}"; }
    }

    private async Task StartScanAsync()
    {
        if (SelectedTarget is null) return;
        IsScanning = true; ScanProgress = 0; _scanCancellation = new CancellationTokenSource();
        _nmapOptions.Profile = SelectedNmapProfile;
        CurrentModule = "Preparing scan..."; StatusMessage = $"Starting scan for {SelectedTarget.Value}."; Navigate("Scan");
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var progress = new Progress<ScanProgress>(p => { ScanProgress = p.Percentage; CurrentModule = p.Message; });
            var session = await scope.ServiceProvider.GetRequiredService<IScanSessionService>().ExecuteScanAsync(SelectedTarget.Id, cancellationToken: _scanCancellation.Token, progress: progress);
            await RefreshAsync();
            SelectedSession = Sessions.FirstOrDefault(s => s.Id == session.Id);
            StatusMessage = $"Scan {session.Status.ToString().ToLowerInvariant()} with {session.Results.Count} findings.";
        }
        catch (Exception ex) { StatusMessage = $"Scan failed: {ex.Message}"; }
        finally { IsScanning = false; _scanCancellation?.Dispose(); _scanCancellation = null; }
    }

    private async Task ExportAsync(string format)
    {
        if (SelectedSession is null) return;
        using var scope = _scopeFactory.CreateScope();
        var fullSession = await scope.ServiceProvider.GetRequiredService<IScanSessionService>().GetSessionByIdAsync(SelectedSession.Id);
        if (fullSession is null) return;
        var path = await scope.ServiceProvider.GetRequiredService<IExportService>().ExportScanSessionAsync(fullSession, format);
        StatusMessage = $"Exported {format.ToUpperInvariant()} report to {path}.";
        Process.Start(new ProcessStartInfo { FileName = Path.GetFullPath(path), UseShellExecute = true });
    }

    private async Task SaveSettingsAsync()
    {
        var apiKeys = ApiKeysText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2)).Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);
        _config.UpdateConfig(new AppConfig { DatabasePath = DatabasePath, LogLevel = LogLevel, DefaultExportFormat = DefaultExportFormat, Theme = Theme, MaxConcurrentModules = _config.Config.MaxConcurrentModules, ApiKeys = apiKeys });
        await _config.SaveConfigAsync(); StatusMessage = "Settings saved. Database location applies on the next application start.";
    }

    private void LoadFindings(ScanSession? session)
    {
        Findings.Clear();
        if (session is not null) foreach (var finding in session.Results.OrderByDescending(r => r.Severity).ThenByDescending(r => r.Timestamp)) Findings.Add(finding);
        RefreshFilteredFindings();
        SelectedFinding = Findings.FirstOrDefault(); OnPropertyChanged(nameof(FindingEvidence));
    }
    private void RefreshFilteredFindings()
    {
        FilteredFindings.Clear();
        foreach (var finding in Findings.Where(MatchesFinding)) FilteredFindings.Add(finding);
    }
    private bool MatchesFinding(ScanResult finding) => string.IsNullOrWhiteSpace(FindingSearch) || $"{finding.Title} {finding.Summary} {finding.ModuleName} {finding.Severity}".Contains(FindingSearch, StringComparison.OrdinalIgnoreCase);
}

public sealed class DashboardPageViewModel(MainViewModel shell) : ObservableObject
{
    private int _targetCount; private int _findingCount; private int _highRiskCount; private string _recentActivity = "No scans yet.";
    public MainViewModel Shell { get; } = shell;
    public int TargetCount { get => _targetCount; private set => SetProperty(ref _targetCount, value); }
    public int FindingCount { get => _findingCount; private set => SetProperty(ref _findingCount, value); }
    public int HighRiskCount { get => _highRiskCount; private set => SetProperty(ref _highRiskCount, value); }
    public string RecentActivity { get => _recentActivity; private set => SetProperty(ref _recentActivity, value); }
    public void Update(IReadOnlyCollection<Target> targets, IReadOnlyCollection<ScanSession> sessions)
    { TargetCount = targets.Count; FindingCount = sessions.Sum(s => s.Results.Count); HighRiskCount = sessions.Sum(s => s.Results.Count(r => r.Severity is ResultSeverity.High or ResultSeverity.Critical)); RecentActivity = sessions.FirstOrDefault() is { } last ? $"Latest scan: {last.Target?.Value} · {last.Status} · {last.StartedAt.ToLocalTime():g}" : "No scans yet."; }
}
public sealed class TargetsPageViewModel(MainViewModel shell) { public MainViewModel Shell { get; } = shell; }
public sealed class ScanPageViewModel(MainViewModel shell) { public MainViewModel Shell { get; } = shell; }
public sealed class ResultsPageViewModel(MainViewModel shell) { public MainViewModel Shell { get; } = shell; }
public sealed class ReportsPageViewModel(MainViewModel shell) { public MainViewModel Shell { get; } = shell; }
public sealed class SettingsPageViewModel(MainViewModel shell) { public MainViewModel Shell { get; } = shell; }

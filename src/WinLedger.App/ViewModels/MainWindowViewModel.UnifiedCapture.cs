using WinLedger.Core.Sessions;
using WinLedger.Domain.Sessions;

namespace WinLedger.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private async Task CaptureUnifiedBaselineAsync()
    {
        ValidateUnifiedCaptureOptions();
        await registryStore.InitializeAsync(CancellationToken.None).ConfigureAwait(true);
        var session = CreateSession(string.IsNullOrWhiteSpace(UnifiedSessionTitle) ? "Tracking session" : UnifiedSessionTitle);
        await registryStore.SaveSessionAsync(session, CancellationToken.None).ConfigureAwait(true);
        unifiedTrackingSession = session;
        ApplySessionTitle(session.Title);

        await RunUnifiedCaptureAsync(session.Id, TrackingSnapshotStage.Baseline).ConfigureAwait(true);
    }

    private async Task CaptureUnifiedComparisonAsync()
    {
        ValidateUnifiedCaptureOptions();
        if (!CanCaptureUnifiedComparison())
        {
            Status = "Capture a unified baseline first.";
            return;
        }

        await RunUnifiedCaptureAsync(unifiedTrackingSession!.Id, TrackingSnapshotStage.Comparison).ConfigureAwait(true);
    }

    private void ValidateUnifiedCaptureOptions()
    {
        var subsystems = CreateUnifiedSubsystems();
        if (subsystems.Contains(TrackingSubsystemKind.Registry))
        {
            _ = CreateRegistryTargets();
        }

        if (subsystems.Contains(TrackingSubsystemKind.FileSystem))
        {
            _ = CreateFileSystemOptions();
        }
    }

    private bool CanCaptureUnifiedComparison()
    {
        return IsUnifiedCaptureIdle &&
               unifiedTrackingSession?.Status is TrackingSessionStatus.BaselineCaptured or TrackingSessionStatus.ComparisonCaptured;
    }

    private Task CancelUnifiedCaptureAsync()
    {
        unifiedCaptureCancellation?.Cancel();
        UnifiedCaptureProgressText = "Cancel requested.";
        Status = "Cancel requested.";
        return Task.CompletedTask;
    }

    private async Task RunUnifiedCaptureAsync(Guid sessionId, TrackingSnapshotStage stage)
    {
        using var cancellation = new CancellationTokenSource();
        unifiedCaptureCancellation = cancellation;
        IsUnifiedCaptureRunning = true;
        UnifiedCaptureProgressPercent = 0;
        UnifiedCaptureProgressText = $"{stage} capture starting.";
        RefreshCommands();

        try
        {
            var request = CreateUnifiedCaptureRequest(sessionId, stage);
            var progress = new Progress<TrackingSessionCaptureProgress>(UpdateUnifiedCaptureProgress);
            var result = await Task.Run(
                () => sessionCaptureOrchestrator.CaptureAsync(request, progress, cancellation.Token),
                cancellation.Token).ConfigureAwait(true);

            unifiedTrackingSession = result.Session;
            var loadedSession = await sessionReopenService.LoadAsync(result.Session.Id, CancellationToken.None)
                .ConfigureAwait(true);
            ApplyLoadedSession(loadedSession);
            UnifiedCaptureProgressPercent = 100;
            UnifiedCaptureProgressText = $"{stage} capture complete: {result.Snapshots.Count} snapshots captured.";
            Status = stage == TrackingSnapshotStage.Baseline
                ? $"Unified baseline captured: {result.Snapshots.Count} snapshots."
                : $"Unified comparison captured: {result.Snapshots.Count} snapshots.";
            await RefreshSessionHistoryAsync(result.Session.Id, updateStatus: false).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            UnifiedCaptureProgressText = "Capture canceled.";
            Status = "Capture canceled.";
        }
        finally
        {
            unifiedCaptureCancellation = null;
            IsUnifiedCaptureRunning = false;
            RefreshCommands();
        }
    }

    private TrackingSessionCaptureRequest CreateUnifiedCaptureRequest(Guid sessionId, TrackingSnapshotStage stage)
    {
        var subsystems = CreateUnifiedSubsystems();
        return new TrackingSessionCaptureRequest(
            sessionId,
            stage == TrackingSnapshotStage.Baseline ? "Baseline" : "Comparison",
            stage,
            subsystems,
            subsystems.Contains(TrackingSubsystemKind.Registry) ? CreateRegistryTargets() : null,
            subsystems.Contains(TrackingSubsystemKind.FileSystem) ? CreateFileSystemOptions() : null);
    }

    private IReadOnlyList<TrackingSubsystemKind> CreateUnifiedSubsystems()
    {
        return new TrackingSubsystemSelection(
            UnifiedIncludeRegistry,
            UnifiedIncludeServices,
            UnifiedIncludeScheduledTasks,
            UnifiedIncludeStartup,
            UnifiedIncludeEnvironment,
            UnifiedIncludeHostsFile,
            UnifiedIncludeFirewall,
            UnifiedIncludeInstalledApplications,
            UnifiedIncludeFileSystem).ToSubsystems();
    }

    private void UpdateUnifiedCaptureProgress(TrackingSessionCaptureProgress progress)
    {
        var total = Math.Max(progress.TotalSubsystems, 1);
        UnifiedCaptureProgressPercent = Math.Clamp(100d * progress.CompletedSubsystems / total, 0, 100);
        UnifiedCaptureProgressText = $"{progress.Stage}: {progress.Subsystem} {progress.Message} ({progress.CompletedSubsystems}/{progress.TotalSubsystems})";
    }
}

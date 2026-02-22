using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AllaganSync.Models;
using AllaganSync.Tracking;
using Dalamud.Plugin.Services;

namespace AllaganSync.Services;

public class SendHistoryEntry
{
    public DateTime Timestamp { get; init; }
    public int Count { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
    public List<TrackedEvent> Events { get; init; } = new();
}

public class EventTrackingService : IDisposable
{
    private readonly IPluginLog log;
    private readonly ConfigurationService configService;
    private readonly AllaganApiClient apiClient;
    private readonly ConcurrentQueue<TrackedEvent> buffer = new();
    private readonly List<IGameEventTracker> trackers = new();
    private readonly List<SendHistoryEntry> sendHistory = new();
    private readonly object sendHistoryLock = new();
    private Timer? flushTimer;
    private int bufferCount;
    private int isFlushing;
    private bool isRunning;

    private const string IngestEndpoint = "/api/v1/character/events/ingest";
    private const int MaxBufferSize = 100;
    private const int FlushThreshold = 50;
    private const int FlushIntervalSeconds = 30;
    private const int MaxSendHistory = 20;
    private const int InitialBackoffSeconds = 30;
    private const int MaxBackoffSeconds = 3600;
    private const int HttpTimeoutSeconds = 30;

    // Backoff state (in-memory only, resets on restart)
    private int currentBackoffSeconds;
    private DateTime? backoffUntil;
    private string? backoffReason;

    // Token abilities (loaded from /api/v1/character/me)
    private string[] tokenAbilities = [];

    public int PendingCount => bufferCount;
    public bool IsRunning => isRunning;
    public DateTime? LastSendTime { get; private set; }
    public string? LastError { get; private set; }
    public IReadOnlyList<SendHistoryEntry> SendHistory
    {
        get { lock (sendHistoryLock) return sendHistory.ToList(); }
    }
    public IReadOnlyList<IGameEventTracker> Trackers => trackers;

    public bool IsBackingOff => backoffUntil.HasValue && DateTime.Now < backoffUntil.Value;
    public string? BackoffReason => IsBackingOff ? backoffReason : null;
    public int BackoffSecondsRemaining => IsBackingOff ? Math.Max(0, (int)(backoffUntil!.Value - DateTime.Now).TotalSeconds) : 0;

    public EventTrackingService(IPluginLog log, ConfigurationService configService, AllaganApiClient apiClient)
    {
        this.log = log;
        this.configService = configService;
        this.apiClient = apiClient;
    }

    public void RegisterTracker(IGameEventTracker tracker)
    {
        trackers.Add(tracker);
        tracker.EventTracked += Enqueue;
    }

    public async Task LoadAbilitiesAsync()
    {
        try
        {
            tokenAbilities = await apiClient.GetMeAbilitiesAsync();
            log.Info($"Loaded {tokenAbilities.Length} token abilities.");
        }
        catch (Exception ex)
        {
            log.Warning($"Failed to load token abilities: {ex.Message}");
            tokenAbilities = [];
        }
    }

    public bool HasAbility(string ability)
    {
        return tokenAbilities.Contains("*") || tokenAbilities.Contains(ability);
    }

    public void UpdateTrackerStates()
    {
        var charConfig = configService.CurrentCharacter;
        if (charConfig == null)
            return;

        foreach (var tracker in trackers)
        {
            if (!tracker.IsAvailable)
            {
                tracker.IsEnabled = false;
                continue;
            }

            if (tracker.RequiredAbility != null && !HasAbility(tracker.RequiredAbility))
            {
                tracker.IsEnabled = false;
                continue;
            }

            var shouldEnable = charConfig.TrackingEnabled
                               && !charConfig.TrackingPaused
                               && charConfig.HasApiToken;

            var perEventEnabled = charConfig.IsEventEnabled(tracker.EventKey);

            tracker.IsEnabled = shouldEnable && perEventEnabled;
        }
    }

    public void Start()
    {
        if (isRunning)
            return;

        var charConfig = configService.CurrentCharacter;
        if (charConfig == null || !charConfig.HasApiToken || !charConfig.TrackingEnabled)
            return;

        isRunning = true;
        flushTimer = new Timer(_ => _ = FlushAsync(), null, TimeSpan.FromSeconds(FlushIntervalSeconds), TimeSpan.FromSeconds(FlushIntervalSeconds));
        UpdateTrackerStates();
        log.Info("Event tracking started.");
    }

    public void Stop()
    {
        if (!isRunning)
            return;

        isRunning = false;
        flushTimer?.Dispose();
        flushTimer = null;

        foreach (var tracker in trackers)
            tracker.IsEnabled = false;

        log.Info("Event tracking stopped.");
    }

    public void Enqueue(TrackedEvent trackedEvent)
    {
        if (!isRunning)
            return;

        var charConfig = configService.CurrentCharacter;
        if (charConfig is { TrackingPaused: true })
            return;

        // Hard cap: drop oldest events
        while (Interlocked.CompareExchange(ref bufferCount, 0, 0) >= MaxBufferSize)
        {
            if (buffer.TryDequeue(out _))
                Interlocked.Decrement(ref bufferCount);
            else
                break;
        }

        buffer.Enqueue(trackedEvent);
        Interlocked.Increment(ref bufferCount);
        log.Debug($"Event enqueued: {trackedEvent.EventType} (buffer: {bufferCount})");

        if (bufferCount >= FlushThreshold && !IsBackingOff)
            _ = FlushAsync();
    }

    public async Task FlushAsync()
    {
        if (bufferCount == 0)
            return;

        if (!apiClient.HasToken())
            return;

        if (IsBackingOff)
            return;

        if (Interlocked.CompareExchange(ref isFlushing, 1, 0) != 0)
            return;

        try
        {
            var events = new List<TrackedEvent>();
            while (buffer.TryDequeue(out var evt))
            {
                events.Add(evt);
                Interlocked.Decrement(ref bufferCount);
            }

            if (events.Count == 0)
                return;

            var request = new EventIngestRequest { Events = events };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(HttpTimeoutSeconds));

            try
            {
                var response = await apiClient.PostAsync(IngestEndpoint, request, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    ResetBackoff();
                    LastSendTime = DateTime.Now;
                    LastError = null;
                    AddSendHistory(events, true, null);
                    log.Info($"Sent {events.Count} events successfully.");
                }
                else
                {
                    var errorMsg = $"API error: {response.StatusCode}";
                    LastError = errorMsg;
                    AddSendHistory(events, false, errorMsg);
                    RequeueEvents(events);
                    ApplyBackoff(errorMsg);
                    log.Error($"Event ingest failed: {response.StatusCode}");
                }
            }
            catch (TaskCanceledException)
            {
                var errorMsg = $"Request timed out after {HttpTimeoutSeconds}s";
                LastError = errorMsg;
                AddSendHistory(events, false, errorMsg);
                RequeueEvents(events);
                ApplyBackoff(errorMsg);
                log.Error($"Event ingest timed out after {HttpTimeoutSeconds}s");
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error: {ex.Message}";
                LastError = errorMsg;
                AddSendHistory(events, false, errorMsg);
                RequeueEvents(events);
                ApplyBackoff(errorMsg);
                log.Error($"Event ingest exception: {ex}");
            }
        }
        finally
        {
            Interlocked.Exchange(ref isFlushing, 0);
        }
    }

    public void Clear()
    {
        while (buffer.TryDequeue(out _))
            Interlocked.Decrement(ref bufferCount);

        lock (sendHistoryLock)
            sendHistory.Clear();
        ResetBackoff();
        LastError = null;
        LastSendTime = null;

        log.Info("Event buffer and history cleared.");
    }

    public List<TrackedEvent> PeekBuffer()
    {
        return [.. buffer];
    }

    /// <summary>
    /// Best-effort requeue: events may be dropped if the buffer filled up
    /// between dequeue and requeue due to concurrent Enqueue() calls.
    /// </summary>
    private void RequeueEvents(List<TrackedEvent> events)
    {
        foreach (var evt in events)
        {
            if (Interlocked.CompareExchange(ref bufferCount, 0, 0) >= MaxBufferSize)
                break;

            buffer.Enqueue(evt);
            Interlocked.Increment(ref bufferCount);
        }
    }

    private void ApplyBackoff(string reason)
    {
        currentBackoffSeconds = currentBackoffSeconds == 0
            ? InitialBackoffSeconds
            : Math.Min(currentBackoffSeconds * 2, MaxBackoffSeconds);

        backoffUntil = DateTime.Now.AddSeconds(currentBackoffSeconds);
        backoffReason = reason;
        log.Warning($"Event sending backing off for {currentBackoffSeconds}s: {reason}");
    }

    private void ResetBackoff()
    {
        currentBackoffSeconds = 0;
        backoffUntil = null;
        backoffReason = null;
    }

    private void AddSendHistory(List<TrackedEvent> events, bool success, string? error)
    {
        lock (sendHistoryLock)
        {
            sendHistory.Insert(0, new SendHistoryEntry
            {
                Timestamp = DateTime.Now,
                Count = events.Count,
                Success = success,
                Error = error,
                Events = new List<TrackedEvent>(events),
            });

            while (sendHistory.Count > MaxSendHistory)
                sendHistory.RemoveAt(sendHistory.Count - 1);
        }
    }

    public void Dispose()
    {
        Stop();
        foreach (var tracker in trackers)
        {
            tracker.EventTracked -= Enqueue;
            tracker.Dispose();
        }
    }
}

using System;
using AllaganSync.Models;

namespace AllaganSync.Tracking;

public interface IGameEventTracker : IDisposable
{
    string EventKey { get; }
    string DisplayName { get; }
    bool IsAvailable { get; }
    bool IsEnabled { get; set; }
    string? RequiredAbility { get; }

    event Action<TrackedEvent>? EventTracked;
}

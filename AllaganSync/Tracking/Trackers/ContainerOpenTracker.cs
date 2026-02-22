using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AllaganSync.Models;
using AllaganSync.Services;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Plugin.Services;

namespace AllaganSync.Tracking.Trackers;

public class ContainerOpenTracker : IGameEventTracker
{
    private const string ContainerListEndpoint = "/api/v1/items?filter[is_random_reward_container]=true&fields[items]=xiv_id&page[size]=1000";
    private const int DelayMs = 300;

    private readonly IPluginLog log;
    private readonly IGameInventory gameInventory;
    private readonly IFramework framework;
    private readonly AllaganApiClient apiClient;

    private volatile HashSet<uint> containerIds = [];
    private readonly List<(uint ItemId, int Quantity)> pendingChanges = [];
    private readonly object pendingLock = new();
    private long delayStartTick;

    public string EventKey => "random_container_result";
    public string DisplayName => "Container Opens";
    public bool IsAvailable { get; }
    public bool IsEnabled { get; set; }
    public string? RequiredAbility => null;

    public event Action<TrackedEvent>? EventTracked;

    public ContainerOpenTracker(
        IPluginLog log,
        IGameInventory gameInventory,
        IFramework framework,
        AllaganApiClient apiClient)
    {
        this.log = log;
        this.gameInventory = gameInventory;
        this.framework = framework;
        this.apiClient = apiClient;

        try
        {
            gameInventory.InventoryChangedRaw += OnInventoryChanged;
            framework.Update += OnFrameworkUpdate;
            IsAvailable = true;
            log.Info("ContainerOpenTracker: Listening to inventory changes.");
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            log.Warning($"ContainerOpenTracker: Failed to subscribe to inventory events. {ex.Message}");
        }
    }

    public async Task LoadContainerListAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await apiClient.GetAsync(ContainerListEndpoint, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                log.Warning($"ContainerOpenTracker: Failed to load container list: {response.StatusCode}");
                return;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<ApiItemResponse>(json);
            if (result?.Data == null)
            {
                log.Warning("ContainerOpenTracker: Container list response was empty.");
                return;
            }

            var newIds = result.Data.Select(item => item.XivId).ToHashSet();
            Interlocked.Exchange(ref containerIds, newIds);
            log.Info($"ContainerOpenTracker: Loaded {newIds.Count} container IDs.");
        }
        catch (Exception ex)
        {
            log.Error($"ContainerOpenTracker: Error loading container list: {ex.Message}");
        }
    }

    private void OnInventoryChanged(IReadOnlyCollection<InventoryEventArgs> events)
    {
        if (!IsEnabled || containerIds.Count == 0)
            return;

        var changes = new Dictionary<uint, (int Added, int Removed)>();
        foreach (var evt in events)
        {
            if (evt.Item.ContainerType == GameInventoryType.DamagedGear)
                continue;

            switch (evt.Type)
            {
                case GameInventoryEvent.Added when evt is InventoryItemAddedArgs { Item: var item }:
                    if (!changes.TryAdd(item.ItemId, (item.Quantity, 0)))
                        changes[item.ItemId] = (changes[item.ItemId].Added + item.Quantity, changes[item.ItemId].Removed);
                    break;
                case GameInventoryEvent.Removed when evt is InventoryItemRemovedArgs { Item: var item }:
                    if (!changes.TryAdd(item.ItemId, (0, item.Quantity)))
                        changes[item.ItemId] = (changes[item.ItemId].Added, changes[item.ItemId].Removed + item.Quantity);
                    break;
                case GameInventoryEvent.Changed when evt is InventoryItemChangedArgs { OldItemState: var oldItem, Item: var newItem }:
                    changes.TryAdd(newItem.ItemId, (0, 0));
                    changes.TryAdd(oldItem.ItemId, (0, 0));
                    if (oldItem.ItemId == newItem.ItemId)
                    {
                        changes[newItem.ItemId] = (changes[newItem.ItemId].Added + newItem.Quantity, changes[newItem.ItemId].Removed + oldItem.Quantity);
                    }
                    else
                    {
                        changes[newItem.ItemId] = (changes[newItem.ItemId].Added + newItem.Quantity, changes[newItem.ItemId].Removed);
                        changes[oldItem.ItemId] = (changes[oldItem.ItemId].Added, changes[oldItem.ItemId].Removed + oldItem.Quantity);
                    }
                    break;
            }
        }

        if (changes.Count == 0)
            return;

        var processed = changes.Select(pair => (pair.Key, pair.Value.Added - pair.Value.Removed)).ToArray();

        lock (pendingLock)
        {
            if (delayStartTick == 0)
                delayStartTick = Environment.TickCount64;

            pendingChanges.AddRange(processed);
        }
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (delayStartTick == 0)
            return;

        if (Environment.TickCount64 < delayStartTick + DelayMs)
            return;

        (uint ItemId, int Quantity)[] snapshot;
        lock (pendingLock)
        {
            if (pendingChanges.Count == 0)
            {
                delayStartTick = 0;
                return;
            }

            snapshot = [.. pendingChanges];
            pendingChanges.Clear();
            delayStartTick = 0;
        }

        ProcessDelayedChanges(snapshot);
    }

    private void ProcessDelayedChanges((uint ItemId, int Quantity)[] changes)
    {
        var removed = changes.Where(c => c.Quantity < 0).ToArray();
        var added = changes.Where(c => c.Quantity > 0).ToArray();

        if (removed.Length != 1 || added.Length != 1)
            return;

        var (containerId, removedQty) = removed[0];
        if (!containerIds.Contains(containerId))
            return;

        if (removedQty * -1 > 1)
            return;

        var (itemId, amount) = added[0];

        var payload = new ContainerOpenResultPayload
        {
            ContainerItemId = containerId,
            ItemId = itemId,
            Amount = amount,
            IsHq = false,
        };

        var trackedEvent = new TrackedEvent
        {
            EventType = EventKey,
            Payload = payload,
            OccurredAt = DateTime.UtcNow.ToString("O"),
        };

        EventTracked?.Invoke(trackedEvent);
        log.Debug($"ContainerOpenTracker: Captured container open {containerId} -> item {itemId} x{amount}.");
    }

    public void FlushAndClear()
    {
        (uint ItemId, int Quantity)[] snapshot;
        lock (pendingLock)
        {
            snapshot = [.. pendingChanges];
            pendingChanges.Clear();
            delayStartTick = 0;
        }

        if (snapshot.Length > 0)
            ProcessDelayedChanges(snapshot);
    }

    public void Dispose()
    {
        gameInventory.InventoryChangedRaw -= OnInventoryChanged;
        framework.Update -= OnFrameworkUpdate;
    }
}

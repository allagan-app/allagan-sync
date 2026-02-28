using System;
using System.Collections.Generic;
using AllaganSync.Models;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Plugin.Services;

namespace AllaganSync.Tracking.Trackers;

public class DutyRewardTracker : IGameEventTracker
{
    private readonly IPluginLog log;
    private readonly IClientState clientState;
    private readonly IDutyState dutyState;
    private readonly IGameInventory gameInventory;
    private readonly IFramework framework;
    private readonly DutyRewardLogic logic = new();

    public string EventKey => "duty_reward";
    public string DisplayName => "Duty Rewards";
    public bool IsAvailable { get; }
    public bool IsEnabled { get; set; }
    public string? RequiredAbility => null;

    public event Action<TrackedEvent>? EventTracked;

    public DutyRewardTracker(
        IPluginLog log,
        IClientState clientState,
        IDutyState dutyState,
        IGameInventory gameInventory,
        IFramework framework)
    {
        this.log = log;
        this.clientState = clientState;
        this.dutyState = dutyState;
        this.gameInventory = gameInventory;
        this.framework = framework;

        try
        {
            dutyState.DutyCompleted += OnDutyCompleted;
            IsAvailable = true;
            log.Info("DutyRewardTracker: IDutyState subscription installed.");
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            log.Warning($"DutyRewardTracker: IDutyState subscription failed. {ex.Message}");
        }

        gameInventory.InventoryChangedRaw += OnInventoryChanged;
        framework.Update += OnFrameworkUpdate;
    }

    private void OnDutyCompleted(object? sender, ushort territoryId)
    {
        if (!IsEnabled)
            return;

        logic.ProcessDutyCompleted(territoryId, clientState.MapId);
        log.Debug($"DutyRewardTracker: Duty completed — TerritoryId={territoryId}");
    }

    private void OnInventoryChanged(IReadOnlyCollection<InventoryEventArgs> events)
    {
        if (!IsEnabled || !logic.IsCollecting)
            return;

        foreach (var evt in events)
        {
            // Only track player inventory containers, not equipped items or currency
            if (!IsPlayerInventory(evt.Item.ContainerType))
                continue;

            switch (evt.Type)
            {
                case GameInventoryEvent.Added when evt is InventoryItemAddedArgs { Item: var item }:
                    logic.ProcessInventoryAdd(item.ItemId, item.Quantity);
                    break;
                case GameInventoryEvent.Changed when evt is InventoryItemChangedArgs { OldItemState: var oldItem, Item: var newItem }:
                    logic.ProcessInventoryChange(oldItem.ItemId, oldItem.Quantity, newItem.ItemId, newItem.Quantity);
                    break;
            }
        }
    }

    private static bool IsPlayerInventory(GameInventoryType container)
    {
        return container is GameInventoryType.Inventory1
            or GameInventoryType.Inventory2
            or GameInventoryType.Inventory3
            or GameInventoryType.Inventory4;
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (!IsEnabled)
            return;

        var trackedEvent = logic.ProcessTick();
        if (trackedEvent == null)
            return;

        EventTracked?.Invoke(trackedEvent);

        if (trackedEvent.Payload is DutyRewardPayload payload)
            log.Debug($"DutyRewardTracker: Captured {payload.Items.Count} reward items.");
    }

    public void Dispose()
    {
        dutyState.DutyCompleted -= OnDutyCompleted;
        gameInventory.InventoryChangedRaw -= OnInventoryChanged;
        framework.Update -= OnFrameworkUpdate;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using AllaganSync.Models;

namespace AllaganSync.Tracking.Trackers;

internal class DutyRewardLogic
{
    internal const int CollectWindowMs = 150;

    private readonly Func<long> getTick;

    internal record struct ItemEntry(uint ItemId, int Quantity);

    private readonly List<ItemEntry> pendingItems = [];
    private readonly object pendingLock = new();
    private long windowStartTick;
    private ushort pendingTerritoryType;
    private uint pendingMapId;

    internal bool IsCollecting => windowStartTick != 0;

    internal DutyRewardLogic(Func<long>? tickProvider = null)
    {
        getTick = tickProvider ?? (() => Environment.TickCount64);
    }

    internal void ProcessDutyCompleted(ushort territory, uint map)
    {
        lock (pendingLock)
        {
            pendingItems.Clear();
            windowStartTick = getTick();
            pendingTerritoryType = territory;
            pendingMapId = map;
        }
    }

    internal void ProcessInventoryAdd(uint itemId, int quantity)
    {
        if (windowStartTick == 0)
            return;

        lock (pendingLock)
        {
            pendingItems.Add(new ItemEntry(itemId, quantity));
        }
    }

    internal void ProcessInventoryChange(uint oldItemId, int oldQuantity, uint newItemId, int newQuantity)
    {
        if (windowStartTick == 0)
            return;

        lock (pendingLock)
        {
            if (oldItemId == newItemId)
            {
                var diff = newQuantity - oldQuantity;
                if (diff > 0)
                    pendingItems.Add(new ItemEntry(newItemId, diff));
            }
            else
            {
                pendingItems.Add(new ItemEntry(newItemId, newQuantity));
            }
        }
    }

    internal TrackedEvent? ProcessTick()
    {
        if (windowStartTick == 0)
            return null;

        if (getTick() < windowStartTick + CollectWindowMs)
            return null;

        ItemEntry[] itemSnapshot;
        ushort territory;
        uint map;

        lock (pendingLock)
        {
            itemSnapshot = [.. pendingItems];
            pendingItems.Clear();
            territory = pendingTerritoryType;
            map = pendingMapId;
            windowStartTick = 0;
        }

        var payload = new DutyRewardPayload
        {
            TerritoryTypeId = territory,
            MapId = map,
            Items = itemSnapshot.Select(item => new DutyRewardItem
            {
                ItemId = item.ItemId,
                Count = item.Quantity,
            }).ToList(),
        };

        return new TrackedEvent
        {
            EventType = "duty_reward",
            Payload = payload,
            OccurredAt = DateTime.UtcNow.ToString("O"),
        };
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using AllaganSync.Models;

namespace AllaganSync.Tracking.Trackers;

internal class MonsterDropLogic
{
    internal const int CollectWindowMs = 1500;

    private readonly Func<long> getTick;

    internal record struct MobDeathEntry(long Tick, uint BnpcBaseId);

    internal record struct ItemEntry(long Tick, uint ItemId, int Quantity);

    private readonly List<MobDeathEntry> pendingDeaths = [];
    private readonly List<ItemEntry> pendingItems = [];
    private readonly object pendingLock = new();
    private long collectEndTick;
    private long windowStartTick;
    private ushort pendingTerritoryType;
    private uint pendingMapId;

    internal bool IsCollecting => collectEndTick != 0;

    internal MonsterDropLogic(Func<long>? tickProvider = null)
    {
        getTick = tickProvider ?? (() => Environment.TickCount64);
    }

    internal void RecordDeath(uint bnpcBaseId, ushort territory, uint map)
    {
        var now = getTick();

        lock (pendingLock)
        {
            if (pendingDeaths.Count == 0)
            {
                windowStartTick = now;
                pendingTerritoryType = territory;
                pendingMapId = map;
            }

            pendingDeaths.Add(new MobDeathEntry(now, bnpcBaseId));
            collectEndTick = now + CollectWindowMs;
        }
    }

    internal void ProcessInventoryAdd(uint itemId, int quantity)
    {
        if (collectEndTick == 0)
            return;

        var now = getTick();

        lock (pendingLock)
        {
            pendingItems.Add(new ItemEntry(now, itemId, quantity));
        }
    }

    internal void ProcessInventoryChange(uint oldItemId, int oldQuantity, uint newItemId, int newQuantity)
    {
        if (collectEndTick == 0)
            return;

        var now = getTick();

        lock (pendingLock)
        {
            if (oldItemId == newItemId)
            {
                var diff = newQuantity - oldQuantity;
                if (diff > 0)
                    pendingItems.Add(new ItemEntry(now, newItemId, diff));
            }
            else
            {
                pendingItems.Add(new ItemEntry(now, newItemId, newQuantity));
            }
        }
    }

    internal TrackedEvent? ProcessTick()
    {
        if (collectEndTick == 0)
            return null;

        if (getTick() < collectEndTick)
            return null;

        MobDeathEntry[] deathSnapshot;
        ItemEntry[] itemSnapshot;
        ushort territory;
        uint map;
        long baseT;

        lock (pendingLock)
        {
            deathSnapshot = [.. pendingDeaths];
            itemSnapshot = [.. pendingItems];
            pendingDeaths.Clear();
            pendingItems.Clear();
            collectEndTick = 0;
            territory = pendingTerritoryType;
            map = pendingMapId;
            baseT = windowStartTick;
        }

        if (itemSnapshot.Length == 0)
            return null;

        var payload = new MonsterDropPayload
        {
            TerritoryTypeId = territory,
            MapId = map,
            Deaths = deathSnapshot.Select(death => new MonsterDropDeath
            {
                BnpcBaseId = death.BnpcBaseId,
                OffsetMs = death.Tick - baseT,
            }).ToList(),
            Items = itemSnapshot.Select(item => new MonsterDropItem
            {
                ItemId = item.ItemId,
                Count = item.Quantity,
                OffsetMs = item.Tick - baseT,
            }).ToList(),
        };

        return new TrackedEvent
        {
            EventType = "monster_drop",
            Payload = payload,
            OccurredAt = DateTime.UtcNow.ToString("O"),
        };
    }
}

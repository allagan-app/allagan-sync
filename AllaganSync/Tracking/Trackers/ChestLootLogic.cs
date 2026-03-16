using System;
using System.Collections.Generic;
using System.Linq;
using AllaganSync.Models;

namespace AllaganSync.Tracking.Trackers;

internal class ChestLootLogic
{
    internal const int CollectWindowMs = 500;

    private readonly Func<long> getTick;

    private uint pendingChestBaseId;
    private uint pendingChestEntityId;
    private ushort pendingTerritoryType;
    private uint pendingMapId;
    private float pendingChestX;
    private float pendingChestY;
    private float pendingChestZ;
    private byte pendingCofferKind;
    private long collectStartTick;
    private readonly List<(uint ItemId, int Quantity)> pendingItems = [];
    private readonly List<uint> pendingChatItems = [];
    private readonly object pendingLock = new();

    private readonly Dictionary<uint, Dictionary<uint, (uint ItemId, ushort ItemCount)>> lootAddedByChest = [];

    internal bool IsCollecting => collectStartTick != 0;

    internal ChestLootLogic(Func<long>? tickProvider = null)
    {
        getTick = tickProvider ?? (() => Environment.TickCount64);
    }

    internal void ProcessChestOpen(
        uint baseId, uint entityId, byte cofferKind,
        float posX, float posY, float posZ,
        ushort territory, uint map)
    {
        pendingChestBaseId = baseId;
        pendingChestEntityId = entityId;
        pendingCofferKind = cofferKind;
        pendingChestX = posX;
        pendingChestY = posY;
        pendingChestZ = posZ;
        pendingTerritoryType = territory;
        pendingMapId = map;

        lock (pendingLock)
        {
            pendingItems.Clear();
            pendingChatItems.Clear();
            collectStartTick = getTick();
        }
    }

    internal void ProcessLootAdded(
        uint chestObjectId, uint chestItemIndex,
        uint itemId, ushort itemCount,
        float time, float maxTime)
    {
        if (time < maxTime)
            return;

        if (!lootAddedByChest.TryGetValue(chestObjectId, out var chestItems))
        {
            chestItems = [];
            lootAddedByChest[chestObjectId] = chestItems;
        }

        chestItems.TryAdd(chestItemIndex, (itemId, itemCount));
    }

    internal void ProcessInventoryAdd(uint itemId, int quantity)
    {
        if (collectStartTick == 0)
            return;

        lock (pendingLock)
        {
            pendingItems.Add((itemId, quantity));
        }
    }

    internal void ProcessInventoryChange(uint oldItemId, int oldQuantity, uint newItemId, int newQuantity)
    {
        if (collectStartTick == 0)
            return;

        lock (pendingLock)
        {
            if (oldItemId == newItemId)
            {
                var diff = newQuantity - oldQuantity;
                if (diff > 0)
                    pendingItems.Add((newItemId, diff));
            }
            else
            {
                pendingItems.Add((newItemId, newQuantity));
            }
        }
    }

    internal void ProcessChatItem(uint itemId)
    {
        if (collectStartTick == 0)
            return;

        lock (pendingLock)
        {
            pendingChatItems.Add(itemId);
        }
    }

    internal TrackedEvent? ProcessTick()
    {
        if (collectStartTick == 0)
            return null;

        if (getTick() < collectStartTick + CollectWindowMs)
            return null;

        (uint ItemId, int Quantity)[] inventorySnapshot;
        uint[] chatSnapshot;
        lock (pendingLock)
        {
            inventorySnapshot = [.. pendingItems];
            chatSnapshot = [.. pendingChatItems];
            pendingItems.Clear();
            pendingChatItems.Clear();
            collectStartTick = 0;
        }

        (uint ItemId, ushort ItemCount)[] lootAddedItems = [];
        if (lootAddedByChest.Remove(pendingChestEntityId, out var chestLootItems))
            lootAddedItems = [.. chestLootItems.Values];

        var items = lootAddedItems.Length > 0
            ? lootAddedItems.Select(item => new ChestLootItem { ItemId = item.ItemId, Count = item.ItemCount }).ToList()
            : chatSnapshot.Length > 0
                ? chatSnapshot.Select(itemId => new ChestLootItem { ItemId = itemId, Count = 1 }).ToList()
                : inventorySnapshot.Select(item => new ChestLootItem { ItemId = item.ItemId, Count = item.Quantity }).ToList();

        var payload = new ChestLootPayload
        {
            TerritoryTypeId = pendingTerritoryType,
            MapId = pendingMapId,
            ChestBaseId = pendingChestBaseId,
            CofferKind = pendingCofferKind,
            PositionX = pendingChestX,
            PositionY = pendingChestY,
            PositionZ = pendingChestZ,
            Items = items,
        };

        return new TrackedEvent
        {
            EventType = "chest_loot",
            Payload = payload,
            OccurredAt = DateTime.UtcNow.ToString("O"),
        };
    }
}

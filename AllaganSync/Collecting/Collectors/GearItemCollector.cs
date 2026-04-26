using System;
using System.Collections.Generic;
using System.Linq;
using AllaganSync.Services;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace AllaganSync.Collecting.Collectors;

public class GearItemCollector : ICollectionCollector, IDisposable
{
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;
    private readonly ConfigurationService configService;
    private readonly IFramework framework;
    private HashSet<uint>? collectableItemIds;
    private ulong lastScannedRetainerId;

    public GearItemCollector(
        IDataManager dataManager,
        IPluginLog log,
        ConfigurationService configService,
        IFramework framework)
    {
        this.dataManager = dataManager;
        this.log = log;
        this.configService = configService;
        this.framework = framework;

        framework.Update += OnFrameworkUpdate;
    }

    public string CollectionKey => "items";
    public string DisplayName => "Gear Items";
    public bool NeedsDataRequest => false;
    public bool IsDataReady => true;
    public void RequestData() { }

    public InventorySource[] Sources => InventorySource.All;

    public int GetTotalCount()
    {
        return GetCollectableItemIds().Count;
    }

    public unsafe List<uint> GetUnlockedIds()
    {
        var collectableIds = GetCollectableItemIds();
        var found = new HashSet<uint>();

        var manager = InventoryManager.Instance();
        if (manager == null)
        {
            log.Warning("[GearItemCollector] InventoryManager not available");
            return [];
        }

        // Scan live inventory sources
        foreach (var source in InventorySource.All)
        {
            if (!IsSourceEnabled(source.Key))
                continue;

            foreach (var inventoryType in source.Types)
            {
                var container = manager->GetInventoryContainer(inventoryType);
                if (container == null || !container->IsLoaded)
                    continue;

                for (var i = 0; i < container->Size; i++)
                {
                    var slot = container->GetInventorySlot(i);
                    if (slot == null || slot->ItemId == 0)
                        continue;

                    var baseItemId = StripHqFlag(slot->ItemId);

                    if (collectableIds.Contains(baseItemId))
                        found.Add(baseItemId);
                }
            }

        }

        // Include cached retainer items
        if (IsSourceEnabled("retainers"))
        {
            var charConfig = configService.CurrentCharacter;
            if (charConfig != null)
            {
                foreach (var (_, cache) in charConfig.RetainerItemCaches)
                {
                    foreach (var itemId in cache.ItemIds)
                    {
                        if (collectableIds.Contains(itemId))
                            found.Add(itemId);
                    }
                }
            }
        }

        return found.ToList();
    }

    public unsafe List<(InventorySource Source, int Found, bool Loaded)> GetSourceCounts()
    {
        var collectableIds = GetCollectableItemIds();
        var result = new List<(InventorySource, int, bool)>();

        var manager = InventoryManager.Instance();
        if (manager == null)
        {
            foreach (var source in InventorySource.All)
                result.Add((source, 0, false));
            result.AddRange(GetRetainerSourceCounts(collectableIds));
            return result;
        }

        foreach (var source in InventorySource.All)
        {
            var found = new HashSet<uint>();
            var allLoaded = true;

            foreach (var inventoryType in source.Types)
            {
                var container = manager->GetInventoryContainer(inventoryType);
                if (container == null || !container->IsLoaded)
                {
                    allLoaded = false;
                    continue;
                }

                for (var i = 0; i < container->Size; i++)
                {
                    var slot = container->GetInventorySlot(i);
                    if (slot == null || slot->ItemId == 0)
                        continue;

                    var baseItemId = StripHqFlag(slot->ItemId);

                    if (collectableIds.Contains(baseItemId))
                        found.Add(baseItemId);
                }
            }

            result.Add((source, found.Count, allLoaded));
        }

        // Add retainer sources from cache
        result.AddRange(GetRetainerSourceCounts(collectableIds));

        return result;
    }

    private List<(InventorySource Source, int Found, bool Loaded)> GetRetainerSourceCounts(HashSet<uint> collectableIds)
    {
        var result = new List<(InventorySource, int, bool)>();
        var charConfig = configService.CurrentCharacter;
        if (charConfig == null)
            return result;

        foreach (var (retainerId, cache) in charConfig.RetainerItemCaches)
        {
            var found = cache.ItemIds.Count(id => collectableIds.Contains(id));
            var source = new InventorySource($"retainer_{retainerId}", $"Retainer: {cache.Name}", []);
            result.Add((source, found, true));
        }

        return result;
    }

    public bool IsSourceEnabled(string key)
    {
        var charConfig = configService.CurrentCharacter;
        return charConfig == null || charConfig.IsItemSourceEnabled(key);
    }

    public void SetSourceEnabled(string key, bool enabled)
    {
        var charConfig = configService.CurrentCharacter;
        if (charConfig == null)
            return;

        charConfig.SetItemSourceEnabled(key, enabled);
        configService.Save();
    }

    private unsafe void OnFrameworkUpdate(IFramework _)
    {
        try
        {
            var retainerManager = RetainerManager.Instance();
            if (retainerManager == null)
                return;

            var activeRetainer = retainerManager->GetActiveRetainer();
            if (activeRetainer == null || activeRetainer->RetainerId == 0)
            {
                // Reset when retainer is closed so we can re-scan next time
                lastScannedRetainerId = 0;
                return;
            }

            var retainerId = activeRetainer->RetainerId;
            if (retainerId == lastScannedRetainerId)
                return;

            // Check if any retainer container is loaded
            var inventoryManager = InventoryManager.Instance();
            if (inventoryManager == null)
                return;

            var anyContainerLoaded = false;
            for (var page = InventoryType.RetainerPage1; page <= InventoryType.RetainerPage7; page++)
            {
                var container = inventoryManager->GetInventoryContainer(page);
                if (container != null && container->IsLoaded)
                {
                    anyContainerLoaded = true;
                    break;
                }
            }

            if (!anyContainerLoaded)
                return;

            // Scan retainer inventory
            var retainerName = activeRetainer->NameString;
            log.Info("[GearItemCollector] Scanning retainer: {Name} ({Id})", retainerName, retainerId);

            var collectableIds = GetCollectableItemIds();
            var foundItems = new HashSet<uint>();

            for (var page = InventoryType.RetainerPage1; page <= InventoryType.RetainerPage7; page++)
            {
                var container = inventoryManager->GetInventoryContainer(page);
                if (container == null || !container->IsLoaded)
                    continue;

                for (var i = 0; i < container->Size; i++)
                {
                    var slot = container->GetInventorySlot(i);
                    if (slot == null || slot->ItemId == 0)
                        continue;

                    var baseItemId = StripHqFlag(slot->ItemId);

                    if (collectableIds.Contains(baseItemId))
                        foundItems.Add(baseItemId);
                }
            }

            var charConfig = configService.CurrentCharacter;
            if (charConfig == null)
                return;

            charConfig.RetainerItemCaches[retainerId] = new RetainerItemCache
            {
                Name = retainerName,
                ItemIds = foundItems.ToList(),
                CachedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            configService.Save();
            lastScannedRetainerId = retainerId;

            log.Info("[GearItemCollector] Cached {Count} collectable items for retainer {Name}", foundItems.Count, retainerName);
        }
        catch (Exception ex)
        {
            log.Error("[GearItemCollector] Error scanning retainer: {Error}", ex.Message);
        }
    }

    private static uint StripHqFlag(uint itemId)
    {
        return itemId > 1000000 ? itemId - 1000000 : itemId;
    }

    private HashSet<uint> GetCollectableItemIds()
    {
        if (collectableItemIds != null)
            return collectableItemIds;

        collectableItemIds = new HashSet<uint>();

        var sheet = dataManager.GetExcelSheet<MirageStoreSetItem>();
        if (sheet == null)
        {
            log.Warning("[GearItemCollector] MirageStoreSetItem sheet not available");
            return collectableItemIds;
        }

        foreach (var row in sheet)
        {
            AddIfNonZero(row.MainHand.RowId);
            AddIfNonZero(row.OffHand.RowId);
            AddIfNonZero(row.Head.RowId);
            AddIfNonZero(row.Body.RowId);
            AddIfNonZero(row.Hands.RowId);
            AddIfNonZero(row.Legs.RowId);
            AddIfNonZero(row.Feet.RowId);
            AddIfNonZero(row.Earrings.RowId);
            AddIfNonZero(row.Necklace.RowId);
            AddIfNonZero(row.Bracelets.RowId);
            AddIfNonZero(row.Ring.RowId);
        }

        return collectableItemIds;
    }

    private void AddIfNonZero(uint rowId)
    {
        if (rowId != 0)
            collectableItemIds!.Add(rowId);
    }

    public void Dispose()
    {
        framework.Update -= OnFrameworkUpdate;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using AllaganSync.Services;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace AllaganSync.Collecting.Collectors;

public class GearItemCollector : ICollectionCollector, IDisposable
{
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;
    private readonly ConfigurationService configService;
    private readonly IFramework framework;
    private HashSet<uint>? collectableItemIds;
    private Dictionary<uint, List<uint>>? outfitItemMap; // MirageStoreSetItem RowId → list of item RowIds
    private ulong lastScannedRetainerId;
    private bool wasPrismBoxLoaded;
    private bool wasCabinetLoaded;

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

        var charConfig = configService.CurrentCharacter;
        if (charConfig != null)
        {
            // Include cached retainer items
            if (IsSourceEnabled("retainers"))
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

            // Include cached glamour dresser items
            if (IsSourceEnabled("glamour_dresser"))
            {
                foreach (var itemId in charConfig.GlamourDresserItemIds)
                {
                    if (collectableIds.Contains(itemId))
                        found.Add(itemId);
                }
            }

            // Include cached cabinet items
            if (IsSourceEnabled("cabinet"))
            {
                foreach (var itemId in charConfig.CabinetItemIds)
                {
                    if (collectableIds.Contains(itemId))
                        found.Add(itemId);
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
            result.AddRange(GetCachedSourceCounts(collectableIds));
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

        // Add cached sources
        result.AddRange(GetCachedSourceCounts(collectableIds));

        return result;
    }

    private List<(InventorySource Source, int Found, bool Loaded)> GetCachedSourceCounts(HashSet<uint> collectableIds)
    {
        var result = new List<(InventorySource, int, bool)>();
        var charConfig = configService.CurrentCharacter;
        if (charConfig == null)
            return result;

        // Glamour Dresser
        var glamourLoaded = charConfig.GlamourDresserItemIds.Count > 0;
        var glamourCount = charConfig.GlamourDresserItemIds.Count(id => collectableIds.Contains(id));
        result.Add((new InventorySource("glamour_dresser", "Glamour Dresser", []), glamourCount, glamourLoaded));

        // Cabinet / Armoire
        var cabinetLoaded = charConfig.CabinetItemIds.Count > 0;
        var cabinetCount = charConfig.CabinetItemIds.Count(id => collectableIds.Contains(id));
        result.Add((new InventorySource("cabinet", "Armoire", []), cabinetCount, cabinetLoaded));

        // Retainers
        foreach (var (retainerId, cache) in charConfig.RetainerItemCaches)
        {
            var found = cache.ItemIds.Count(id => collectableIds.Contains(id));
            var source = new InventorySource($"{InventorySource.RetainerKeyPrefix}{retainerId}", $"Retainer: {cache.Name}", []);
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
            ScanRetainerIfNeeded();
            ScanGlamourDresserIfNeeded();
            ScanCabinetIfNeeded();
        }
        catch (Exception ex)
        {
            log.Error("[GearItemCollector] Error in framework update: {Error}", ex.Message);
        }
    }

    private unsafe void ScanRetainerIfNeeded()
    {
        var retainerManager = RetainerManager.Instance();
        if (retainerManager == null)
            return;

        var activeRetainer = retainerManager->GetActiveRetainer();
        if (activeRetainer == null || activeRetainer->RetainerId == 0)
        {
            lastScannedRetainerId = 0;
            return;
        }

        var retainerId = activeRetainer->RetainerId;
        if (retainerId == lastScannedRetainerId)
            return;

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

    private unsafe void ScanGlamourDresserIfNeeded()
    {
        var mirageManager = MirageManager.Instance();
        var isLoaded = mirageManager != null && mirageManager->PrismBoxLoaded;

        // Edge detection: scan when PrismBoxLoaded transitions from false to true
        if (!isLoaded || wasPrismBoxLoaded)
        {
            wasPrismBoxLoaded = isLoaded;
            return;
        }

        wasPrismBoxLoaded = true;

        log.Info("[GearItemCollector] Scanning glamour dresser");

        var collectableIds = GetCollectableItemIds();
        var outfitMap = GetOutfitItemMap();
        var foundItems = new HashSet<uint>();

        // Scan individual items in the prism box
        var itemIds = mirageManager->PrismBoxItemIds;
        for (var i = 0; i < itemIds.Length; i++)
        {
            var itemId = itemIds[i];
            if (itemId == 0)
                continue;

            var baseItemId = StripHqFlag(itemId);

            // Check if it's a direct collectable item
            if (collectableIds.Contains(baseItemId))
                foundItems.Add(baseItemId);

            // Check if it's an outfit (MirageStoreSetItem) — expand to all set items
            if (outfitMap.TryGetValue(baseItemId, out var setItems))
            {
                foreach (var setItemId in setItems)
                    foundItems.Add(setItemId);
            }
        }

        var charConfig = configService.CurrentCharacter;
        if (charConfig == null)
            return;

        charConfig.GlamourDresserItemIds = foundItems.ToList();
        charConfig.GlamourDresserCachedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        configService.Save();

        log.Info("[GearItemCollector] Cached {Count} collectable items from glamour dresser", foundItems.Count);
    }

    private unsafe void ScanCabinetIfNeeded()
    {
        var uiState = UIState.Instance();
        var isLoaded = uiState != null && uiState->Cabinet.IsCabinetLoaded();

        if (!isLoaded || wasCabinetLoaded)
        {
            wasCabinetLoaded = isLoaded;
            return;
        }

        wasCabinetLoaded = true;

        log.Info("[GearItemCollector] Scanning armoire");

        var collectableIds = GetCollectableItemIds();
        var foundItems = new HashSet<uint>();

        var cabinetSheet = dataManager.GetExcelSheet<Lumina.Excel.Sheets.Cabinet>();
        if (cabinetSheet != null)
        {
            foreach (var row in cabinetSheet)
            {
                var itemId = row.Item.RowId;
                if (itemId == 0 || !collectableIds.Contains(itemId))
                    continue;

                if (uiState->Cabinet.IsItemInCabinet((int)row.RowId))
                    foundItems.Add(itemId);
            }
        }

        var charConfig = configService.CurrentCharacter;
        if (charConfig == null)
            return;

        charConfig.CabinetItemIds = foundItems.ToList();
        charConfig.CabinetCachedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        configService.Save();

        log.Info("[GearItemCollector] Cached {Count} collectable items from armoire", foundItems.Count);
    }

    /// <summary>
    /// Builds a map from outfit item ID (the MirageStoreSetItem's linked Item RowId)
    /// to all gear item IDs in that set.
    /// </summary>
    private Dictionary<uint, List<uint>> GetOutfitItemMap()
    {
        if (outfitItemMap != null)
            return outfitItemMap;

        outfitItemMap = new Dictionary<uint, List<uint>>();

        var sheet = dataManager.GetExcelSheet<MirageStoreSetItem>();
        if (sheet == null)
            return outfitItemMap;

        foreach (var row in sheet)
        {
            // The outfit item has the same RowId as the MirageStoreSetItem
            var outfitItemId = row.RowId;
            if (outfitItemId == 0)
                continue;

            var setItems = new List<uint>();
            AddIfNonZeroToList(setItems, row.MainHand.RowId);
            AddIfNonZeroToList(setItems, row.OffHand.RowId);
            AddIfNonZeroToList(setItems, row.Head.RowId);
            AddIfNonZeroToList(setItems, row.Body.RowId);
            AddIfNonZeroToList(setItems, row.Hands.RowId);
            AddIfNonZeroToList(setItems, row.Legs.RowId);
            AddIfNonZeroToList(setItems, row.Feet.RowId);
            AddIfNonZeroToList(setItems, row.Earrings.RowId);
            AddIfNonZeroToList(setItems, row.Necklace.RowId);
            AddIfNonZeroToList(setItems, row.Bracelets.RowId);
            AddIfNonZeroToList(setItems, row.Ring.RowId);

            if (setItems.Count > 0)
                outfitItemMap[outfitItemId] = setItems;
        }

        return outfitItemMap;
    }

    private static void AddIfNonZeroToList(List<uint> list, uint rowId)
    {
        if (rowId != 0)
            list.Add(rowId);
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

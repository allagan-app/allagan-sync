using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace AllaganSync.Collecting.Collectors;

public class FishCollector(IDataManager dataManager) : ICollectionCollector
{
    public string CollectionKey => "fish";
    public string DisplayName => "Fish";
    public bool NeedsDataRequest => false;
    public bool IsDataReady => true;
    public void RequestData() { }

    private bool IsValid(FishParameter fish)
    {
        var itemId = fish.Item.RowId;
        if (itemId == 0)
            return false;

        var itemSheet = dataManager.GetExcelSheet<Item>();
        if (itemSheet == null)
            return false;

        var item = itemSheet.GetRowOrDefault(itemId);
        return item.HasValue && !item.Value.Name.IsEmpty;
    }

    private static bool IsCollectable(FishParameter fish)
    {
        return fish.IsInLog;
    }

    public int GetTotalCount()
    {
        var sheet = dataManager.GetExcelSheet<FishParameter>();
        return sheet?.Count(f => IsValid(f) && IsCollectable(f)) ?? 0;
    }

    public unsafe List<uint> GetUnlockedIds()
    {
        var unlockedIds = new List<uint>();
        var fishSheet = dataManager.GetExcelSheet<FishParameter>();

        if (fishSheet == null)
            return unlockedIds;

        var playerState = PlayerState.Instance();
        if (playerState == null)
            return unlockedIds;

        foreach (var row in fishSheet)
        {
            if (!IsValid(row) || !IsCollectable(row))
                continue;

            if (playerState->IsFishCaught(row.RowId))
            {
                unlockedIds.Add(row.RowId);
            }
        }

        return unlockedIds;
    }
}

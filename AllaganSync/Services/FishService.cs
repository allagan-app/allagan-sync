using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace AllaganSync.Services;

public class FishService
{
    private readonly IDataManager dataManager;

    public FishService(IDataManager dataManager)
    {
        this.dataManager = dataManager;
    }

    // Validator: FishParameter is valid if it has a valid Item with a name
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

    // Collectable filter: IsFishCaught only works for FishParameter rows where IsInLog is true
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

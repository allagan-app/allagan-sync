using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace AllaganSync.Services;

public class BardingService
{
    private readonly IDataManager dataManager;

    public BardingService(IDataManager dataManager)
    {
        this.dataManager = dataManager;
    }

    private static bool IsValid(BuddyEquip barding)
    {
        return !barding.Name.IsEmpty;
    }

    public int GetTotalCount()
    {
        var sheet = dataManager.GetExcelSheet<BuddyEquip>();
        return sheet?.Count(IsValid) ?? 0;
    }

    public unsafe List<uint> GetUnlockedIds()
    {
        var unlockedIds = new List<uint>();
        var bardingSheet = dataManager.GetExcelSheet<BuddyEquip>();

        if (bardingSheet == null)
            return unlockedIds;

        var uiState = UIState.Instance();
        if (uiState == null)
            return unlockedIds;

        foreach (var row in bardingSheet)
        {
            if (!IsValid(row))
                continue;

            if (uiState->Buddy.CompanionInfo.IsBuddyEquipUnlocked(row.RowId))
            {
                unlockedIds.Add(row.RowId);
            }
        }

        return unlockedIds;
    }
}

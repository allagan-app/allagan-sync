using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace AllaganSync.Services;

public class MinionService
{
    private readonly IDataManager dataManager;

    public MinionService(IDataManager dataManager)
    {
        this.dataManager = dataManager;
    }

    private static bool IsValid(Companion minion)
    {
        return !minion.Singular.IsEmpty;
    }

    public int GetTotalCount()
    {
        var sheet = dataManager.GetExcelSheet<Companion>();
        return sheet?.Count(IsValid) ?? 0;
    }

    public unsafe List<uint> GetUnlockedIds()
    {
        var unlockedIds = new List<uint>();
        var companionSheet = dataManager.GetExcelSheet<Companion>();

        if (companionSheet == null)
            return unlockedIds;

        var uiState = UIState.Instance();
        if (uiState == null)
            return unlockedIds;

        foreach (var row in companionSheet)
        {
            if (!IsValid(row))
                continue;

            if (uiState->IsCompanionUnlocked(row.RowId))
            {
                unlockedIds.Add(row.RowId);
            }
        }

        return unlockedIds;
    }
}

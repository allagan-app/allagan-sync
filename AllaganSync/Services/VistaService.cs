using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace AllaganSync.Services;

public class VistaService
{
    private readonly IDataManager dataManager;

    public VistaService(IDataManager dataManager)
    {
        this.dataManager = dataManager;
    }

    private static bool IsValid(Adventure adventure)
    {
        return !adventure.Name.IsEmpty;
    }

    public int GetTotalCount()
    {
        var sheet = dataManager.GetExcelSheet<Adventure>();
        return sheet?.Count(IsValid) ?? 0;
    }

    public unsafe List<uint> GetUnlockedIds()
    {
        var unlockedIds = new List<uint>();
        var adventureSheet = dataManager.GetExcelSheet<Adventure>();

        if (adventureSheet == null)
            return unlockedIds;

        var playerState = PlayerState.Instance();
        if (playerState == null)
            return unlockedIds;

        foreach (var row in adventureSheet)
        {
            if (!IsValid(row))
                continue;

            // Adventure RowIds have an offset of 2162688
            var adventureIndex = row.RowId - 2162688;
            if (playerState->IsAdventureComplete(adventureIndex))
            {
                unlockedIds.Add(row.RowId);
            }
        }

        return unlockedIds;
    }
}

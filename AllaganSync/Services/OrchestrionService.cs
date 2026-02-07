using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace AllaganSync.Services;

public class OrchestrionService
{
    private readonly IDataManager dataManager;

    public OrchestrionService(IDataManager dataManager)
    {
        this.dataManager = dataManager;
    }

    private static bool IsValid(Orchestrion orchestrion)
    {
        return !orchestrion.Name.IsEmpty;
    }

    public int GetTotalCount()
    {
        var sheet = dataManager.GetExcelSheet<Orchestrion>();
        return sheet?.Count(IsValid) ?? 0;
    }

    public unsafe List<uint> GetUnlockedIds()
    {
        var unlockedIds = new List<uint>();
        var orchestrionSheet = dataManager.GetExcelSheet<Orchestrion>();

        if (orchestrionSheet == null)
            return unlockedIds;

        var playerState = PlayerState.Instance();
        if (playerState == null)
            return unlockedIds;

        foreach (var row in orchestrionSheet)
        {
            if (!IsValid(row))
                continue;

            if (playerState->IsOrchestrionRollUnlocked(row.RowId))
            {
                unlockedIds.Add(row.RowId);
            }
        }

        return unlockedIds;
    }
}

using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace AllaganSync.Services;

public class MountService
{
    private readonly IDataManager dataManager;

    public MountService(IDataManager dataManager)
    {
        this.dataManager = dataManager;
    }

    private static bool IsValid(Mount mount)
    {
        return !mount.Singular.IsEmpty && mount.Order >= 0;
    }

    public int GetTotalCount()
    {
        var sheet = dataManager.GetExcelSheet<Mount>();
        return sheet?.Count(IsValid) ?? 0;
    }

    public unsafe List<uint> GetUnlockedIds()
    {
        var unlockedIds = new List<uint>();
        var mountSheet = dataManager.GetExcelSheet<Mount>();

        if (mountSheet == null)
            return unlockedIds;

        var playerState = PlayerState.Instance();
        if (playerState == null)
            return unlockedIds;

        foreach (var row in mountSheet)
        {
            if (!IsValid(row))
                continue;

            if (playerState->IsMountUnlocked(row.RowId))
            {
                unlockedIds.Add(row.RowId);
            }
        }

        return unlockedIds;
    }
}

using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace AllaganSync.Services;

public class EmoteService
{
    private readonly IDataManager dataManager;

    public EmoteService(IDataManager dataManager)
    {
        this.dataManager = dataManager;
    }

    private static bool IsValid(Emote emote)
    {
        return !emote.Name.IsEmpty && emote.Order > 0;
    }

    public int GetTotalCount()
    {
        var sheet = dataManager.GetExcelSheet<Emote>();
        return sheet?.Count(IsValid) ?? 0;
    }

    public unsafe List<uint> GetUnlockedIds()
    {
        var unlockedIds = new List<uint>();
        var emoteSheet = dataManager.GetExcelSheet<Emote>();

        if (emoteSheet == null)
            return unlockedIds;

        var uiState = UIState.Instance();
        if (uiState == null)
            return unlockedIds;

        foreach (var row in emoteSheet)
        {
            if (!IsValid(row))
                continue;

            if (uiState->IsEmoteUnlocked((ushort)row.RowId))
            {
                unlockedIds.Add(row.RowId);
            }
        }

        return unlockedIds;
    }
}

using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace AllaganSync.Services;

public class TripleTriadCardService
{
    private readonly IDataManager dataManager;

    public TripleTriadCardService(IDataManager dataManager)
    {
        this.dataManager = dataManager;
    }

    private static bool IsValid(TripleTriadCard card)
    {
        return !card.Name.IsEmpty;
    }

    public int GetTotalCount()
    {
        var sheet = dataManager.GetExcelSheet<TripleTriadCard>();
        return sheet?.Count(IsValid) ?? 0;
    }

    public unsafe List<uint> GetUnlockedIds()
    {
        var unlockedIds = new List<uint>();
        var cardSheet = dataManager.GetExcelSheet<TripleTriadCard>();

        if (cardSheet == null)
            return unlockedIds;

        var uiState = UIState.Instance();
        if (uiState == null)
            return unlockedIds;

        foreach (var row in cardSheet)
        {
            if (!IsValid(row))
                continue;

            if (uiState->IsTripleTriadCardUnlocked((ushort)row.RowId))
            {
                unlockedIds.Add(row.RowId);
            }
        }

        return unlockedIds;
    }
}

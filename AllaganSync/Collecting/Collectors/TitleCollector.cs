using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace AllaganSync.Collecting.Collectors;

public class TitleCollector(IDataManager dataManager, IPluginLog log) : ICollectionCollector
{
    public string CollectionKey => "titles";
    public string DisplayName => "Titles";
    public bool NeedsDataRequest => true;

    public unsafe bool IsDataReady
    {
        get
        {
            var uiState = UIState.Instance();
            return uiState != null && uiState->TitleList.DataReceived;
        }
    }

    public unsafe void RequestData()
    {
        var uiState = UIState.Instance();
        if (uiState == null)
            return;

        if (!uiState->TitleList.DataReceived && !uiState->TitleList.DataPending)
        {
            log.Info("TitleCollector: Requesting title list from server...");
            uiState->TitleList.RequestTitleList();
        }
    }

    private static bool IsValid(Title title)
    {
        return !title.Masculine.IsEmpty;
    }

    public int GetTotalCount()
    {
        var sheet = dataManager.GetExcelSheet<Title>();
        return sheet?.Count(IsValid) ?? 0;
    }

    public unsafe List<uint> GetUnlockedIds()
    {
        var unlockedIds = new List<uint>();
        var titleSheet = dataManager.GetExcelSheet<Title>();

        if (titleSheet == null)
        {
            log.Error("TitleCollector: titleSheet is null");
            return unlockedIds;
        }

        var uiState = UIState.Instance();
        if (uiState == null)
        {
            log.Error("TitleCollector: uiState is null");
            return unlockedIds;
        }

        if (!uiState->TitleList.DataReceived)
        {
            log.Warning("TitleCollector: Title data not yet received, requesting...");
            uiState->TitleList.RequestTitleList();
            return unlockedIds;
        }

        foreach (var row in titleSheet)
        {
            if (!IsValid(row))
                continue;

            if (uiState->TitleList.IsTitleUnlocked((ushort)row.RowId))
            {
                unlockedIds.Add(row.RowId);
            }
        }

        return unlockedIds;
    }
}

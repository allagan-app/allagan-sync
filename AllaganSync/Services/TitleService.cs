using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace AllaganSync.Services;

public class TitleService
{
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;

    public TitleService(IDataManager dataManager, IPluginLog log)
    {
        this.dataManager = dataManager;
        this.log = log;
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

    public unsafe void RequestTitleData()
    {
        var uiState = UIState.Instance();
        if (uiState == null)
            return;

        if (!uiState->TitleList.DataReceived && !uiState->TitleList.DataPending)
        {
            log.Info("TitleService: Requesting title list from server...");
            uiState->TitleList.RequestTitleList();
        }
    }

    public unsafe List<uint> GetUnlockedIds()
    {
        var unlockedIds = new List<uint>();
        var titleSheet = dataManager.GetExcelSheet<Title>();

        if (titleSheet == null)
        {
            log.Error("TitleService: titleSheet is null");
            return unlockedIds;
        }

        var uiState = UIState.Instance();
        if (uiState == null)
        {
            log.Error("TitleService: uiState is null");
            return unlockedIds;
        }

        if (!uiState->TitleList.DataReceived)
        {
            log.Warning("TitleService: Title data not yet received, requesting...");
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

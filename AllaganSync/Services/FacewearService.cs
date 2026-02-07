using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace AllaganSync.Services;

public class FacewearService
{
    private readonly IDataManager dataManager;

    public FacewearService(IDataManager dataManager)
    {
        this.dataManager = dataManager;
    }

    private bool IsValid(GlassesStyle glassesStyle)
    {
        var firstGlassesId = glassesStyle.Glasses.FirstOrDefault().RowId;
        return IsValidGlasses(firstGlassesId);
    }

    private bool IsValidGlasses(uint glassesId)
    {
        if (glassesId == 0)
            return false;

        var glassesSheet = dataManager.GetExcelSheet<Glasses>();
        if (glassesSheet == null)
            return false;

        var glasses = glassesSheet.GetRowOrDefault(glassesId);
        return glasses != null && !glasses.Value.Name.IsEmpty;
    }

    public int GetTotalCount()
    {
        var sheet = dataManager.GetExcelSheet<GlassesStyle>();
        return sheet?.Count(IsValid) ?? 0;
    }

    public unsafe List<uint> GetUnlockedIds()
    {
        var unlockedIds = new List<uint>();
        var glassesStyleSheet = dataManager.GetExcelSheet<GlassesStyle>();

        if (glassesStyleSheet == null)
            return unlockedIds;

        var playerState = PlayerState.Instance();
        if (playerState == null)
            return unlockedIds;

        foreach (var row in glassesStyleSheet)
        {
            if (!IsValid(row))
                continue;

            // Use the Glasses ID (not GlassesStyle ID) for unlock check
            var glassesId = row.Glasses.FirstOrDefault().RowId;
            var isUnlocked = playerState->IsGlassesUnlocked((ushort)glassesId);

            if (isUnlocked)
            {
                unlockedIds.Add(row.RowId);
            }
        }

        return unlockedIds;
    }
}

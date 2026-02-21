using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace AllaganSync.Collecting.Collectors;

public class FacewearCollector(IDataManager dataManager, IUnlockState unlockState) : ICollectionCollector
{
    public string CollectionKey => "facewears";
    public string DisplayName => "Facewear";
    public bool NeedsDataRequest => false;
    public bool IsDataReady => true;
    public void RequestData() { }

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

    public List<uint> GetUnlockedIds()
    {
        var unlockedIds = new List<uint>();
        var glassesStyleSheet = dataManager.GetExcelSheet<GlassesStyle>();
        var glassesSheet = dataManager.GetExcelSheet<Glasses>();

        if (glassesStyleSheet == null || glassesSheet == null)
            return unlockedIds;

        foreach (var row in glassesStyleSheet)
        {
            if (!IsValid(row))
                continue;

            var glassesId = row.Glasses.FirstOrDefault().RowId;
            if (!glassesSheet.TryGetRow(glassesId, out var glassesRow))
                continue;

            if (unlockState.IsGlassesUnlocked(glassesRow))
            {
                unlockedIds.Add(row.RowId);
            }
        }

        return unlockedIds;
    }
}

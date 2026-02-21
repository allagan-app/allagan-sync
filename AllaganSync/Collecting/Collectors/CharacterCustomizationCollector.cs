using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace AllaganSync.Collecting.Collectors;

public class CharacterCustomizationCollector(IDataManager dataManager, IUnlockState unlockState) : ICollectionCollector
{
    public string CollectionKey => "character_customizations";
    public string DisplayName => "Hairstyles & Face Paints";
    public bool NeedsDataRequest => false;
    public bool IsDataReady => true;
    public void RequestData() { }

    private static bool IsFacePaint(CharaMakeCustomize row)
    {
        return row.RowId >= 2401;
    }

    private bool IsValidItem(uint itemId)
    {
        var itemSheet = dataManager.GetExcelSheet<Item>();
        if (itemSheet == null)
            return false;

        var item = itemSheet.GetRowOrDefault(itemId);
        return item.HasValue && !item.Value.Name.IsEmpty;
    }

    private bool IsValid(CharaMakeCustomize row)
    {
        if (row.FeatureID == 0)
            return false;

        var itemId = row.HintItem.RowId;
        return itemId == 0 || IsValidItem(itemId);
    }

    private static string GetGroupKey(CharaMakeCustomize row)
    {
        return $"{row.FeatureID}-{(IsFacePaint(row) ? "facepaint" : "hairstyle")}";
    }

    public int GetTotalCount()
    {
        var sheet = dataManager.GetExcelSheet<CharaMakeCustomize>();
        if (sheet == null)
            return 0;

        return sheet
            .Where(IsValid)
            .GroupBy(GetGroupKey)
            .Count();
    }

    public List<uint> GetUnlockedIds()
    {
        var unlockedIds = new List<uint>();
        var sheet = dataManager.GetExcelSheet<CharaMakeCustomize>();

        if (sheet == null)
            return unlockedIds;

        var groups = sheet
            .Where(IsValid)
            .GroupBy(GetGroupKey)
            .Select(g => new
            {
                MinRowId = g.Min(r => r.RowId),
                Rows = g.ToList()
            });

        foreach (var group in groups)
        {
            var isUnlocked = group.Rows.Any(row =>
                row.HintItem.RowId == 0 || unlockState.IsCharaMakeCustomizeUnlocked(row));

            if (isUnlocked)
            {
                unlockedIds.Add(group.MinRowId);
            }
        }

        return unlockedIds;
    }
}

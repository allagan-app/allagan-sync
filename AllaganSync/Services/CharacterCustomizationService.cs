using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace AllaganSync.Services;

public class CharacterCustomizationService
{
    private readonly IDataManager dataManager;
    private readonly IUnlockState unlockState;

    public CharacterCustomizationService(IDataManager dataManager, IUnlockState unlockState)
    {
        this.dataManager = dataManager;
        this.unlockState = unlockState;
    }

    // Face paint is RowId >= 2401
    private static bool IsFacePaint(CharaMakeCustomize row)
    {
        return row.RowId >= 2401;
    }

    // Check if item is valid (has a name)
    private bool IsValidItem(uint itemId)
    {
        var itemSheet = dataManager.GetExcelSheet<Item>();
        if (itemSheet == null)
            return false;

        var item = itemSheet.GetRowOrDefault(itemId);
        return item.HasValue && !item.Value.Name.IsEmpty;
    }

    // Validator: FeatureID > 0 and (Item == 0 or Item valid)
    private bool IsValid(CharaMakeCustomize row)
    {
        if (row.FeatureID == 0)
            return false;

        var itemId = row.HintItem.RowId;
        return itemId == 0 || IsValidItem(itemId);
    }

    // Group key: FeatureID + IsFacePaint
    private static string GetGroupKey(CharaMakeCustomize row)
    {
        return $"{row.FeatureID}-{(IsFacePaint(row) ? "facepaint" : "hairstyle")}";
    }

    public int GetTotalCount()
    {
        var sheet = dataManager.GetExcelSheet<CharaMakeCustomize>();
        if (sheet == null)
            return 0;

        var groups = sheet
            .Where(IsValid)
            .GroupBy(GetGroupKey)
            .ToList();

        return groups.Count;
    }

    public List<uint> GetUnlockedIds()
    {
        var unlockedIds = new List<uint>();
        var sheet = dataManager.GetExcelSheet<CharaMakeCustomize>();

        if (sheet == null)
            return unlockedIds;

        // Group by FeatureID + type, use minimum RowId as the canonical ID
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
            // A group is unlocked if ANY row in the group is unlocked
            // A row is unlocked if:
            // - HintItem == 0 (base hairstyle/facepaint that everyone has), OR
            // - IsCharaMakeCustomizeUnlocked returns true (purchasable and unlocked)
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

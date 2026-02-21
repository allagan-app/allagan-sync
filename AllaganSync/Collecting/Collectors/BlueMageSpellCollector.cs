using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace AllaganSync.Collecting.Collectors;

public class BlueMageSpellCollector(IDataManager dataManager, IUnlockState unlockState)
    : UnlockStateCollector<AozAction>(dataManager, unlockState)
{
    private ExcelSheet<Lumina.Excel.Sheets.Action>? cachedActionSheet;

    public override string CollectionKey => "blue_mage_spells";
    public override string DisplayName => "Blue Mage Spells";

    protected override bool IsValid(AozAction row)
    {
        var actionId = row.Action.RowId;
        if (actionId == 0)
            return false;

        cachedActionSheet ??= dataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
        if (cachedActionSheet == null)
            return false;

        var action = cachedActionSheet.GetRowOrDefault(actionId);
        if (action == null)
            return false;

        return !action.Value.Name.IsEmpty && action.Value.ClassJobLevel > 0;
    }

    protected override bool IsUnlocked(AozAction row)
    {
        return unlockState.IsAozActionUnlocked(row);
    }
}

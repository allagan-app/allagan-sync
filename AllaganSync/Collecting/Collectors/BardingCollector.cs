using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace AllaganSync.Collecting.Collectors;

public class BardingCollector(IDataManager dataManager, IUnlockState unlockState)
    : UnlockStateCollector<BuddyEquip>(dataManager, unlockState)
{
    public override string CollectionKey => "bardings";
    public override string DisplayName => "Bardings";

    protected override bool IsValid(BuddyEquip row)
    {
        return !row.Name.IsEmpty;
    }

    protected override bool IsUnlocked(BuddyEquip row)
    {
        return unlockState.IsBuddyEquipUnlocked(row);
    }
}

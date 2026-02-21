using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace AllaganSync.Collecting.Collectors;

public class MountCollector(IDataManager dataManager, IUnlockState unlockState)
    : UnlockStateCollector<Mount>(dataManager, unlockState)
{
    public override string CollectionKey => "mounts";
    public override string DisplayName => "Mounts";

    protected override bool IsValid(Mount row)
    {
        return !row.Singular.IsEmpty && row.Order >= 0;
    }

    protected override bool IsUnlocked(Mount row)
    {
        return unlockState.IsMountUnlocked(row);
    }
}

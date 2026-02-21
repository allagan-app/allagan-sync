using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace AllaganSync.Collecting.Collectors;

public class MinionCollector(IDataManager dataManager, IUnlockState unlockState)
    : UnlockStateCollector<Companion>(dataManager, unlockState)
{
    public override string CollectionKey => "minions";
    public override string DisplayName => "Minions";

    protected override bool IsValid(Companion row)
    {
        return !row.Singular.IsEmpty;
    }

    protected override bool IsUnlocked(Companion row)
    {
        return unlockState.IsCompanionUnlocked(row);
    }
}
